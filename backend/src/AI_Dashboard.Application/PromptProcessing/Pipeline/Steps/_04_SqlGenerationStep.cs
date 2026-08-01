using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing.Models;
using System.Text.Json;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

public class SqlGenerationStep : IPipelineStep
{
    private readonly IOllamaClient _ollama;
    private readonly ISchemaIntrospectionOptions _options;
    private readonly ISchemaChunkStore _chunkStore;
    private readonly string _coderModel;

    public SqlGenerationStep(
        IOllamaClient ollama,
        IOllamaModelOptions modelOpts,
        ISchemaIntrospectionOptions options,
        ISchemaChunkStore chunkStore)
    {
        _ollama = ollama;
        _coderModel = modelOpts.CoderModel;
        _options = options;
        _chunkStore = chunkStore;
    }

    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (context.Intent is null || context.SchemaMap is null || context.NeedsClarification) return;

        var map    = context.SchemaMap;
        var tenant = _options.TenantColumn;

        var schemaDoc   = JsonDocument.Parse(map.SchemaDdl);
        var allTables   = schemaDoc.RootElement.GetProperty("tables").EnumerateArray().ToList();

        var schemaLines = allTables.Select(t =>
        {
            var tName = t.GetProperty("name").GetString();
            var cols  = t.GetProperty("columns").EnumerateArray()
                .Select(c => c.GetProperty("name").GetString());
            var rels  = t.GetProperty("relationships").EnumerateArray()
                .Select(r => $"{tName}.{r.GetProperty("from_column").GetString()} -> {r.GetProperty("to_table").GetString()}.{r.GetProperty("to_column").GetString()}");
            var relStr = rels.Any() ? $" | FK: {string.Join(", ", rels)}" : string.Empty;
            return $"{tName}({string.Join(", ", cols)}){relStr}";
        });

        var tenantRule = tenant is not null
            ? $"Always filter the PRIMARY table: WHERE {map.Table}.{tenant} = :tenantId\n" +
              $"CRITICAL: every table you JOIN also has a '{tenant}' column and MUST be tenant-scoped " +
              $"too — add \"AND <joined_table>.{tenant} = :tenantId\" to that table's JOIN ON clause, " +
              $"for EVERY join, not just the primary table. A join missing this can leak rows across " +
              $"tenants that happen to share the same numeric id. This applies whether the join was " +
              $"already given to you below or you're constructing one yourself from the FK list.\n" +
              "JOIN TYPE: default to LEFT JOIN for any table needed to compute an aggregate/metric " +
              "(COUNT, AVG, MAX, etc.) unless the request is specifically about rows that already have " +
              "a match. An INNER JOIN silently drops rows with no match at all, which is wrong whenever " +
              "\"zero\" is a meaningful answer — e.g. \"average comments per ticket\" must include tickets " +
              "with zero comments in the denominator (LEFT JOIN ticket_comments), and \"longest without a " +
              "resolved ticket\" must include organizations with zero tickets at all, not just organizations " +
              "that happen to have unresolved ones (LEFT JOIN tickets). Only use INNER JOIN when a missing " +
              "match makes the row genuinely irrelevant to the question (e.g. grouping by agent only makes " +
              "sense for tickets that actually have an agent assigned).\n"
            : string.Empty;

        var joinRule = map.JoinClauses.Count > 0
            ? "This join has ALREADY been resolved — use it exactly as given, do not invent " +
              "a different join path or add any other join:\n" +
              string.Join("\n", map.JoinClauses.Select(j => $"  {j}")) + "\n" +
              "Because more than one table is involved, qualify EVERY column reference with its " +
              "table name (e.g. tickets.id, agents.name) — an unqualified column when multiple " +
              "tables are joined causes a \"column reference is ambiguous\" error.\n"
            : string.Empty;

        var nullsOrderingRule = System.Text.RegularExpressions.Regex.IsMatch(
            context.PromptText, @"\b(longest without|never|hasn'?t|has not|no .*\byet\b)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            ? "This request is about entities that may have NEVER had a matching event — those produce " +
              "NULL in an aggregate like MAX(date_column). If you ORDER BY that aggregate ASC to find the " +
              "most overdue/longest-without case, add NULLS FIRST so those never-happened rows surface " +
              "first (Postgres sorts NULL last by default in ASC, which is backwards here): " +
              "ORDER BY <expr> ASC NULLS FIRST.\n"
            : string.Empty;

        var filterRule = context.Intent.Filters.Count > 0
            ? "The filters below were ALREADY extracted from the user's request in a prior step — " +
              "apply them exactly, don't invent different filters or drop any of them. If a filter's " +
              "literal value doesn't exactly match the casing/spelling shown in the Known actual values " +
              "section further below, correct it to match — but keep the same column and comparison:\n" +
              string.Join("\n", context.Intent.Filters.Select(f => $"  {f}")) + "\n"
            : string.Empty;

        var havingFilterMatch = context.Intent.Filters
            .Select(f => System.Text.RegularExpressions.Regex.Match(
                f, @"\b(AVG|COUNT|SUM|MIN|MAX)\s*\(([^)]+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .FirstOrDefault(m => m.Success);
        var hasHavingFilter = havingFilterMatch is { Success: true };

        var havingRule = hasHavingFilter
            ? $"IMPORTANT: the filter \"{havingFilterMatch!.Value}\" is an aggregate condition — it MUST " +
              $"become a HAVING clause (not WHERE), using EXACTLY that function and column " +
              $"({havingFilterMatch.Groups[1].Value.ToUpperInvariant()}({havingFilterMatch.Groups[2].Value.Trim()})). " +
              "Do NOT invent a different aggregate (e.g. COUNT(id)) for HAVING or for the SELECT list — " +
              "if the resolved Metric above refers to the same column, they must use the identical " +
              "expression. Only add that aggregate as a visible SELECT column if the user's request " +
              "explicitly asks to see/display the computed number — if the request only asks to see " +
              "descriptive column(s) (e.g. \"show name of...\"), do NOT add the aggregate to SELECT at " +
              "all; it's only needed in HAVING. A HAVING clause requires GROUP BY on whatever descriptive " +
              "column(s) you DO select, even if no chart grouping/dimension was otherwise specified.\n"
            : string.Empty;

        var resolvedRule = context.Intent.IsAggregation
            ? $"This request has already been resolved to:\n" +
              $"  Table: {map.Table}\n" +
              $"  Group-by column: {(map.DimensionColumn ?? (hasHavingFilter ? "none given, but see HAVING rule below — one is still required" : "none — do NOT use GROUP BY at all"))}\n" +
              $"  Metric: {(context.Intent.Metric ?? "none specified — use COUNT(*), unless the HAVING rule below already fixes the aggregate")}\n" +
              filterRule +
              havingRule +
              "Rules:\n" +
              "- If a group-by column is given: SELECT that column plus exactly ONE aggregate " +
              "expression (e.g. COUNT(*), SUM(x)) aliased with AS, and GROUP BY that same column only — " +
              "UNLESS the HAVING rule above says not to show the aggregate, in which case SELECT only the " +
              "group-by column.\n" +
              "- If no group-by column is given AND there is no HAVING rule above: SELECT a single " +
              "aggregate expression only, no GROUP BY, no extra columns.\n" +
              "Never add any other column to SELECT or GROUP BY — especially not a primary key such as id, " +
              "and never an aggregate the user didn't ask for (e.g. a row count nobody requested).\n"
            : $"This is a plain listing/browse request — the user wants to see raw records, NOT a count, " +
              $"sum, average, or any other aggregate.\n" +
              $"  Table: {map.Table}\n" +
              $"  Requested column: {(map.DimensionColumn ?? "use the column(s) named in the request text")}\n" +
              filterRule +
              "Rules:\n" +
              "- Do NOT use COUNT, SUM, AVG, or any other aggregate function.\n" +
              "- Do NOT use GROUP BY.\n" +
              "- SELECT exactly the column(s) the user asked to see. Add LIMIT 200 so large tables " +
              "don't return an unbounded result set.\n";


        var systemPrompt =
            "You are a PostgreSQL expert. Output ONLY the raw SQL SELECT statement.\n" +
            "No explanation. No markdown. No comments. Start with SELECT.\n" +
            tenantRule +
            joinRule +
            nullsOrderingRule +
            resolvedRule +
            "Schema:\n" + string.Join("\n", schemaLines);

        var knownValuesRule = string.Empty;
        try
        {
            var metadata = await _chunkStore.RetrieveRelevantMetadataAsync(context.TenantId, context.PromptText, topK: 8, ct);
            if (metadata.Count > 0)
            {
                var lines = metadata
                    .Where(m => m.SampleValues.Count > 0)
                    .Select(m => $"  {m.TableName}.{m.ColumnName}: {string.Join(", ", m.SampleValues.Select(v => $"'{v}'"))}");
                var lineStr = string.Join("\n", lines);
                if (!string.IsNullOrEmpty(lineStr))
                {
                    knownValuesRule =
                        "Known actual values for these columns (use this EXACT spelling/casing in any " +
                        "filter — e.g. WHERE clause — never lowercase or otherwise alter it):\n" +
                        lineStr + "\n";
                }
            }
        }
        catch { /* metadata lookup is a best-effort hint, never block generation on it */ }

        systemPrompt += knownValuesRule;

        var userPrompt = $"Request: {context.PromptText}\nSQL:";

        var raw = await _ollama.GenerateWithModelAsync(_coderModel, systemPrompt, userPrompt, 256, ct);
        context.GeneratedSql = StripFences(raw);
    }

    private static string StripFences(string raw)
    {
        var m = System.Text.RegularExpressions.Regex.Match(raw, @"```(?:\w+)?\s*([\s\S]*?)```");
        return m.Success ? m.Groups[1].Value.Trim() : raw.Trim();
    }
}