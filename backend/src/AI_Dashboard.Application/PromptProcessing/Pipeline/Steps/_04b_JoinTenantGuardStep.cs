using System.Text;
using System.Text.RegularExpressions;
using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

/// Step 4b — Tenant Guard (deterministic, runs right after SqlGenerationStep,
/// before SqlRepairStep validates the final SQL).
///
/// SqlGenerationStep's system prompt ASKS the coder model to tenant-qualify the
/// primary table's WHERE clause AND every JOIN's ON clause — but that's a prompt
/// instruction, not a guarantee. Two distinct failure modes observed in practice:
///
/// 1. A JOIN's ON clause missing its tenant condition entirely — can silently
///    join rows across tenants that happen to share the same numeric id.
/// 2. The model moving the PRIMARY table's tenant condition into a LEFT JOIN's ON
///    clause instead of a top-level WHERE — which does NOT filter the primary
///    table's rows at all (LEFT JOIN preserves every left-side row regardless of
///    whether the ON condition matches), silently returning every tenant's data.
///
/// This step never trusts that the model complied with either: it deterministically
/// guarantees both a real WHERE condition on the primary table AND a tenant
/// condition on every JOIN's ON clause.
///
/// Known limitation: regex-based, not a real SQL AST. It handles the flat
/// single-statement JOIN chains this pipeline has produced so far (no CTEs, no
/// subqueries inside a JOIN's ON clause). If SqlGenerationStep ever starts
/// producing those shapes, this step needs a real parser instead.
public class JoinTenantGuardStep : IPipelineStep
{
    private readonly ISchemaIntrospectionOptions _options;

    public JoinTenantGuardStep(ISchemaIntrospectionOptions options) => _options = options;

    private static readonly Regex JoinPattern = new(
        @"\b(?:LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+OUTER\s+JOIN|FULL\s+JOIN|INNER\s+JOIN|JOIN)\s+" +
        @"""?(?<table>\w+)""?(?:\s+(?:AS\s+)?(?<alias>(?!ON\b)\w+))?\s+ON\s+(?<on>.*?)" +
        @"(?=\s+(?:LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+OUTER\s+JOIN|FULL\s+JOIN|INNER\s+JOIN|JOIN|WHERE|GROUP\s+BY|ORDER\s+BY|LIMIT|HAVING)\b|$)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ClauseBoundary = new(
        @"\b(GROUP\s+BY|HAVING|ORDER\s+BY|LIMIT)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.GeneratedSql)
            || context.NeedsClarification
            || _options.TenantColumn is null)
            return Task.CompletedTask;

        var sql = InjectMissingTenantConditions(context.GeneratedSql, _options.TenantColumn);

        // context.SchemaMap survives from SchemaResolutionStep — it tells us which
        // table is the primary/driving one, so we can guarantee its WHERE filter
        // independently of whatever the model did (or didn't) do.
        if (context.SchemaMap is not null)
            sql = EnsurePrimaryTableTenantFilter(sql, context.SchemaMap.Table, _options.TenantColumn);

        context.GeneratedSql = sql;
        return Task.CompletedTask;
    }

    internal static string InjectMissingTenantConditions(string sql, string tenantColumn)
    {
        var matches = JoinPattern.Matches(sql);
        if (matches.Count == 0) return sql;

        var sb = new StringBuilder();
        var cursor = 0;

        foreach (Match m in matches)
        {
            var table = m.Groups["table"].Value;
            // If the join uses an alias ("JOIN agents a ON ..."), the ON clause
            // references the alias, not the table name — use whichever the query
            // actually uses so the injected condition resolves correctly.
            var reference = m.Groups["alias"].Success ? m.Groups["alias"].Value : table;

            var onGroup     = m.Groups["on"];
            var onClauseRaw = onGroup.Value;

            // If this JOIN's ON clause is the last thing in the statement (no
            // WHERE/GROUP BY/etc. after it), the non-greedy capture above pulls
            // the trailing ";" (and whitespace) into the "on" group. Appending
            // naively would land the new condition AFTER that semicolon —
            // "...ON a.id=b.id; AND x.tenant_id = 1" — which is invalid SQL.
            // Split the trailing punctuation off and put it back after whatever
            // we append, not before.
            var coreClause = onClauseRaw.TrimEnd(';', ' ', '\t', '\r', '\n');
            var trailing   = onClauseRaw[coreClause.Length..];

            // Copy everything up to (not including) the ON clause unchanged.
            sb.Append(sql, cursor, onGroup.Index - cursor);
            sb.Append(coreClause);

            var alreadyQualified = Regex.IsMatch(
                coreClause, $@"\b{Regex.Escape(reference)}\.{Regex.Escape(tenantColumn)}\b",
                RegexOptions.IgnoreCase);

            if (!alreadyQualified)
                sb.Append($" AND {reference}.{tenantColumn} = :tenantId");

            sb.Append(trailing);

            cursor = onGroup.Index + onClauseRaw.Length;
        }

        sb.Append(sql, cursor, sql.Length - cursor);
        return sb.ToString();
    }

    // Guarantees the PRIMARY (FROM) table has a real tenant condition in the
    // top-level WHERE clause — independent of whatever InjectMissingTenantConditions
    // did to JOIN ON clauses above. This is the fix for failure mode #2 in the class
    // doc comment: a model that puts the tenant filter only inside a LEFT JOIN's ON
    // clause doesn't actually filter the primary table's rows at all.
    internal static string EnsurePrimaryTableTenantFilter(string sql, string primaryTable, string tenantColumn)
    {
        // Find how the primary table is referenced in THIS query — its alias if the
        // FROM clause declared one ("FROM agents a"), otherwise the table name itself.
        var fromMatch = Regex.Match(sql,
            $@"\bFROM\s+""?{Regex.Escape(primaryTable)}""?" +
            @"(?:\s+(?:AS\s+)?(?<alias>(?!WHERE\b|JOIN\b|INNER\b|LEFT\b|RIGHT\b|FULL\b|GROUP\b|ORDER\b|LIMIT\b|HAVING\b)\w+))?",
            RegexOptions.IgnoreCase);
        if (!fromMatch.Success) return sql; // primary table isn't in FROM — nothing to guarantee

        var reference = fromMatch.Groups["alias"].Success ? fromMatch.Groups["alias"].Value : primaryTable;

        // If there's no WHERE clause yet, the insertion point for a new one must be
        // AFTER any JOINs that follow FROM — not right after "FROM organizations"
        // itself, or "WHERE ..." would land BEFORE "LEFT JOIN ...", which is invalid
        // SQL syntax (WHERE must come after every JOIN).
        var searchStart = fromMatch.Index + fromMatch.Length;
        var joinMatches = JoinPattern.Matches(sql);
        if (joinMatches.Count > 0)
        {
            var lastJoinEnd = joinMatches[^1].Index + joinMatches[^1].Length;
            if (lastJoinEnd > searchStart) searchStart = lastJoinEnd;
        }

        var whereMatch = Regex.Match(sql,
            @"\bWHERE\b(?<body>.*?)(?=\b(?:GROUP\s+BY|HAVING|ORDER\s+BY|LIMIT)\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var alreadyFiltered = whereMatch.Success && Regex.IsMatch(
            whereMatch.Groups["body"].Value,
            $@"\b{Regex.Escape(reference)}\.{Regex.Escape(tenantColumn)}\b",
            RegexOptions.IgnoreCase);

        if (alreadyFiltered) return sql;

        var condition = $"{reference}.{tenantColumn} = :tenantId";

        if (whereMatch.Success)
        {
            // Existing WHERE clause, just missing the primary table's condition —
            // append via AND right at the end of the WHERE body, before GROUP
            // BY/ORDER BY/LIMIT/HAVING (or end of statement) if present.
            var insertAt = whereMatch.Index + whereMatch.Length;
            return sql.Insert(insertAt, $" AND {condition}");
        }

        // No WHERE clause at all — insert one right after the FROM/JOIN chain
        // (searchStart, computed above), before the first GROUP BY/ORDER BY/LIMIT/
        // HAVING if present, else at the end of the statement.
        var boundary = ClauseBoundary.Match(sql, searchStart);
        var insertPos = boundary.Success ? boundary.Index : sql.TrimEnd(';', ' ', '\n', '\r').Length;
        return sql.Insert(insertPos, $"WHERE {condition} ");
    }
}