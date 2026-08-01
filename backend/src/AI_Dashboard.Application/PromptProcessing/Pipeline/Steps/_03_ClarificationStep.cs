using System.Text.Json;
using AI_Dashboard.Application.Common.Helpers;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing.Models;
 
namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;
 
public class ClarificationStep : IPipelineStep
{
    private readonly ISchemaIntrospectionService _schemaService;
    private readonly IOllamaClient _ollama;
 
    // Dashboard styles mirror the dashboard_styles DB lookup table — fixed set, not AI-generated.
    private static readonly List<string> DashboardStyleOptions =
        new() { "Power BI", "Grafana", "Custom" };
 
    public ClarificationStep(ISchemaIntrospectionService schemaService, IOllamaClient ollama)
    {
        _schemaService = schemaService;
        _ollama = ollama;
    }
 
    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (context.Intent is null) return;
        var intent = context.Intent;
        var missing = intent.MissingField;
        if (missing is null && context.SchemaMap is not null) return; // nothing to clarify
 
        var schema = await _schemaService.GetSchemaDescriptionAsync(context.TenantId, ct);
 
        switch (missing)
        {
            case "visualization_type":
                var vizOptions = BuildVizOptions(context.PromptText, intent.Dimension is not null, intent.IsAggregation);
                context.Clarification = new ClarificationRequest
                {
                    Question = "How would you like to visualize this?",
                    Options  = vizOptions
                };
                return;
 
            case "dashboard_style":
                context.Clarification = new ClarificationRequest
                {
                    Question = "Which dashboard style would you like?",
                    Options  = [.. DashboardStyleOptions]
                };
                return;
 
            case "time_period":
                context.Clarification = new ClarificationRequest
                {
                    Question = "Which time period would you like?",
                    Options  = await AskAiForOptionsAsync(
                        $"The user wants a time-series/trend dashboard. " +
                        $"Suggest 4-5 time period options as a JSON array of strings. " +
                        $"Examples: [\"Last 7 Days\", \"Last 30 Days\", \"Last 3 Months\", \"Last Year\", \"Custom\"]",
                        ["Last 7 Days", "Last 30 Days", "Last 3 Months", "Last Year", "Custom"], ct)
                };
                return;
 
            case "grouping":
                context.Clarification = new ClarificationRequest
                {
                    Question = "How would you like to group the data?",
                    Options  = await AskAiForOptionsAsync(
                        $"Schema:\n{schema}\n" +
                        $"User request: {context.PromptText}\n" +
                        $"Suggest 3-5 meaningful grouping dimension options as a JSON array of strings. " +
                        $"Use human-readable names (e.g. 'By Status', 'By Category', 'By Date'). " +
                        $"Only suggest groupings that make sense given the schema.",
                        ["By Status", "By Category", "By Date"], ct)
                };
                return;
 
            default:
                if (context.SchemaMap is null)
                {
                    var metricOptions = await AskAiForOptionsAsync(
                        $"Schema:\n{schema}\n" +
                        $"User request: {context.PromptText}\n" +
                        $"The user's request is ambiguous. Suggest 4-6 specific metric/analysis options " +
                        $"as a JSON array of strings. Use human-readable names. " +
                        $"Only suggest options that match columns in the schema.",
                        ["Row Count", "Total by Category", "Trend Over Time", "Distribution by Status"], ct);
 
                    context.Clarification = new ClarificationRequest
                    {
                        Question = "What would you like to analyze? Here are some suggestions:",
                        Options  = metricOptions
                    };
                }
                return;
        }
    }
 
    private static List<string> BuildVizOptions(string promptText, bool hasDimension, bool isAggregation)
    {
        var all = VisualizationTypeMapper.LabelsFor(hasDimension, isAggregation);
 
        var styleMatch = System.Text.RegularExpressions.Regex.Match(
            promptText,
            @"^Use\s+(\w[\w\s]*?)\s+dashboard style",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 
        if (!styleMatch.Success) return all;
 
        var style = styleMatch.Groups[1].Value.Trim().ToLowerInvariant();
 
        var preferred = style switch
        {
            var s when s.Contains("grafana")   => new[] { "Line Chart", "Bar Chart", "Table", "KPI Card", "Pie Chart" },
            var s when s.Contains("power_bi")
                    || s.Contains("powerbi")
                    || s.Contains("power bi") => new[] { "KPI Card", "Bar Chart", "Pie Chart", "Table", "Line Chart" },
            _                                  => null
        };
 
        if (preferred is null) return all;
 
        return preferred.Where(all.Contains).Concat(all.Except(preferred)).ToList();
    }
 
    private async Task<List<string>> AskAiForOptionsAsync(
        string prompt, IEnumerable<string> defaults, CancellationToken ct)
    {
        try
        {
            var systemPrompt =
                "You are a dashboard options generator. Output ONLY a JSON array of strings. " +
                "No explanation, no markdown, no extra text. Example: [\"Option 1\", \"Option 2\"]";
 
            var raw  = await _ollama.GenerateAsync(systemPrompt, prompt, 150, ct);
            var json = ResponseJsonParser.ExtractJson(raw);
 
            if (!json.TrimStart().StartsWith('['))
            {
                var start = raw.IndexOf('[');
                var end   = raw.LastIndexOf(']');
                if (start >= 0 && end > start)
                    json = raw[start..(end + 1)];
            }
 
            var options = JsonSerializer.Deserialize<List<string>>(json);
            if (options is { Count: > 0 })
                return options;
        }
        catch { /* fall through to defaults */ }
 
        return [.. defaults];
    }
}