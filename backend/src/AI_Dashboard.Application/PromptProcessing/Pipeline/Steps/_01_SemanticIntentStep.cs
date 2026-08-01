using System.Text.Json;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Common.Helpers;
using AI_Dashboard.Application.Common.Schema;
using AI_Dashboard.Application.PromptProcessing.Models;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

public class SemanticIntentStep : IPipelineStep
{
    private readonly IOllamaClient _ollama;
    private readonly ISchemaIntrospectionService _schemaService;
    private readonly ISchemaChunkStore _chunkStore;
    private readonly ISchemaIntrospectionOptions _options;

    private static readonly string KpiCardIntent = VisualizationTypeMapper.ToIntentValue("KPI Card")!; //user-friendly names converted into one that is kpi card

    private static readonly System.Text.RegularExpressions.Regex AggregationLanguage = new(
        @"\b(count|how many|total|sum|average|avg|number of|percentage|per\s+\w+|group(ed)?\s+by|by\s+\w+)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public SemanticIntentStep(
        IOllamaClient ollama,
        ISchemaIntrospectionService schemaService,
        ISchemaChunkStore chunkStore,
        ISchemaIntrospectionOptions options)
    {
        _ollama = ollama;
        _schemaService = schemaService;
        _chunkStore = chunkStore;
        _options = options;
    }

    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        var seed = context.PreSeededIntent;

        var allTables = await _schemaService.GetTablesAsync(context.TenantId, ct);
        var relevantTables = await _chunkStore.RetrieveRelevantTablesAsync(
            context.TenantId, context.PromptText, allTables, topK: 2, ct: ct);

        if (relevantTables.Count == 0)
        {
            context.Clarification = new ClarificationRequest
            {
                Question = "No relevant schema found. Try asking about the data available in this dashboard " +
                            "(e.g. tickets, agents, organizations), not an unrelated topic.",
                Options = null
            };
            context.IsBlocked = true;
            return;
        }

        var schema = BuildSchemaDescription(relevantTables);
        var dimensionHints = BuildDimensionHints(relevantTables, _options.TenantColumn);

        var systemPrompt =
            "You are a dashboard intent parser. Output JSON only, no explanation, no markdown.\n" +
            "Visualization type rules:\n" +
            VisualizationTypeMapper.ToSystemPromptRules() +
            "- dashboard_style: 'grafana' if server/infra/monitoring, 'power_bi' if BI/KPI/business, 'custom' otherwise.\n" +
            "  Set dashboard_style_ambiguous=true ONLY if user says 'server monitoring' without specifying style.\n" +
            "- metric: EXACT column name for numeric aggregation. null for COUNT queries.\n" +
            "  Use null for 'total', 'count', 'how many', 'show X by Y' type requests.\n" +
            $"- dimension: EXACT column name for GROUP BY. null if not grouping.\n{dimensionHints}" +
            $"  If user says 'by X', dimension MUST be the column name matching X.\n" +
            $"  If user says 'show/list <column> of/for <table> where/whose <condition>' (e.g. \"show name " +
            $"of organizations where average satisfaction is below 4\"), the requested display column " +
            $"(here: name) IS the dimension — set it even though the phrasing isn't 'by X'. This applies " +
            $"whenever the request names a specific descriptive column to display alongside a filter, " +
            $"aggregate or otherwise.\n" +
            $"  If user asks for a single total ({KpiCardIntent.Replace('_', ' ')}), dimension MUST be null.\n" +
            "- is_aggregation: true if the user wants a computed/summarized result — a count, sum, " +
            "average, total, grouped breakdown, or trend over time. false if the user just wants to " +
            "see/list/browse raw records or specific fields as they are, with nothing being counted " +
            "or combined (e.g. 'show name of all agents', 'list open tickets', 'show all categories'). " +
            "Default to false when the request has no counting/summing language and no 'by X' grouping.\n" +
            "- grouping_ambiguous: true ONLY if user asks for a dashboard about an entity without specifying metric/grouping.\n" +
            "- filters: exact SQL conditions using column = 'value' syntax.\n" +
            "- time_period: extract if mentioned ('last 30 days', 'this month'), null otherwise.\n" +
            "- time_period_missing: true ONLY if user asks for trends/time-series but gives no time range.\n" +
            $"\nDatabase schema:\n{schema}";

        var userPrompt =
            $"User request: {context.PromptText}\n\n" +
            "{\"metric\": string|null, \"dimension\": string|null, \"filters\": string[], " +
            "\"visualization_type\": string|null, \"dashboard_style\": string, \"data_source\": string, " +
            "\"time_period\": string|null, \"is_aggregation\": bool, \"dashboard_style_ambiguous\": bool, " +
            "\"grouping_ambiguous\": bool, \"time_period_missing\": bool}";

        var raw  = await _ollama.GenerateAsync(systemPrompt, userPrompt, 512, ct);
        var json = ResponseJsonParser.ExtractJson(raw);

        try
        {
            context.Intent = JsonSerializer.Deserialize<IntentResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new IntentResult();
        }
        catch (JsonException)
        {
            context.Intent = new IntentResult();
        }

        if (context.Intent.VisualizationType is not null
            && VisualizationTypeMapper.ToWidgetType(context.Intent.VisualizationType) is null)
            context.Intent.VisualizationType = null;

        if (!AggregationLanguage.IsMatch(context.PromptText))
            context.Intent.IsAggregation = false;

        if (!context.Intent.IsAggregation && context.Intent.VisualizationType is null)
            context.Intent.VisualizationType = "table";

        if (context.PreSeededIntent is not null)
        {
            var s = context.PreSeededIntent;
            if (s.VisualizationType is not null) context.Intent.VisualizationType = s.VisualizationType;
            if (s.Dimension is not null)         context.Intent.Dimension         = s.Dimension;
            if (s.DashboardStyleConfirmed)       context.Intent.DashboardStyle    = s.DashboardStyle;
            if (s.TimePeriod is not null)        context.Intent.TimePeriod        = s.TimePeriod;

            foreach (var f in s.Filters)
                if (!context.Intent.Filters.Contains(f))
                    context.Intent.Filters.Add(f);

            if (s.VisualizationType == KpiCardIntent)
            {
                context.Intent.Dimension         = null;
                context.Intent.GroupingAmbiguous = false;
            }

            if (s.VisualizationType is not null) context.Intent.DashboardStyleAmbiguous = false;
            if (s.Dimension is not null)          context.Intent.GroupingAmbiguous       = false;
            if (s.TimePeriod is not null)         context.Intent.TimePeriodMissing       = false;
            if (s.DashboardStyleConfirmed)         context.Intent.DashboardStyleAmbiguous = false;

            context.Intent.TableColumnConfirmed = s.TableColumnConfirmed;
            context.Intent.TableColumnRejected  = s.TableColumnRejected;
            context.Intent.VisualizationCompatibilityConfirmed = s.VisualizationCompatibilityConfirmed;

            if (s.VisualizationTypeRejected) context.Intent.VisualizationType = null;
        }
    }

    private static string BuildSchemaDescription(List<TableMetadata> tables)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in tables)
        {
            sb.Append(t.TableName).Append('(');
            sb.Append(string.Join(", ", t.Columns.Select(c =>
                c.IsNullable ? $"{c.ColumnName} {c.DataType}?" : $"{c.ColumnName} {c.DataType}")));
            sb.Append(")\n");
        }
        return sb.ToString();
    }

    private static string BuildDimensionHints(List<TableMetadata> tables, string? tenantColumn)
    {
        var lines = new List<string>();
        foreach (var t in tables)
            foreach (var c in t.Columns)
                if (!IsNumeric(c.DataType) && !IsTimestamp(c.DataType)
                    && !c.ColumnName.Equals(tenantColumn ?? "tenant_id", StringComparison.OrdinalIgnoreCase))
                    lines.Add($"  'by {c.ColumnName.Replace('_', ' ')}' -> '{c.ColumnName}' (from {t.TableName})");
        return lines.Count > 0 ? string.Join("\n", lines) + "\n" : string.Empty;
    }

   /* private async Task<string> BuildFilterExamplesAsync(short tenantId, List<TableMetadata> tables, CancellationToken ct)
    {
        var excluded = new HashSet<string>(
            _options.ExcludedFilterColumns
                .Concat(_options.SensitiveColumns)
                .Concat(_options.TenantColumn is not null ? new[] { _options.TenantColumn } : Array.Empty<string>()),
            StringComparer.OrdinalIgnoreCase);

        var lines = new List<string>();
        foreach (var t in tables)
        {
            foreach (var c in t.Columns)
            {
                if (c.DataType != "character varying" && c.DataType != "varchar") continue;
                if (excluded.Contains(c.ColumnName)) continue;

                try
                {
                    var values = await _schemaService.GetDistinctValuesAsync(tenantId, t.TableName, c.ColumnName, ct);
                    if (values.Count > 0)
                        lines.Add($"  Known {c.ColumnName} values: {string.Join(", ", values.Select(v => $"'{v}'"))}");
                }
                catch { }
            }
        }
        return lines.Count > 0 ? string.Join("\n", lines) + "\n" : string.Empty;
    }

    private static string? ExtractDimensionFromPrompt(string promptText)
    {
        var lower = promptText.ToLowerInvariant();
        var match = System.Text.RegularExpressions.Regex.Match(lower, @"\bby\s+(\w+)");
        return match.Success ? match.Groups[1].Value : null;
    }*/

    private static bool IsNumeric(string dataType) =>
        dataType is "integer" or "bigint" or "numeric" or "decimal" or "real" or "double precision" or "smallint";

    private static bool IsTimestamp(string dataType) =>
        dataType.StartsWith("timestamp") || dataType == "date";
}