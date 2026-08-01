using System.Text.Json;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Common.Schema;
using AI_Dashboard.Application.PromptProcessing.Models;
 
namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;
 
public class SchemaResolutionStep : IPipelineStep
{
    private readonly ISchemaIntrospectionService _schemaService;
    private readonly ISchemaChunkStore _chunkStore;
    private readonly ISchemaGraph _schemaGraph;
 
    public SchemaResolutionStep(
        ISchemaIntrospectionService schemaService,
        ISchemaChunkStore chunkStore,
        ISchemaGraph schemaGraph)
    {
        _schemaService = schemaService;
        _chunkStore = chunkStore;
        _schemaGraph = schemaGraph;
    }
 
    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        // Resolve the table as soon as we have *a* prompt to search against — this must
        // NOT wait for the full intent (viz type, metric, etc.) to be settled, because
        // table confirmation is meant to happen before those questions, not after.
        if (context.Intent is null) return;
 
        var allTables = await _schemaService.GetTablesAsync(context.TenantId, ct);
        var relevantTables = await _chunkStore.RetrieveRelevantTablesAsync(
            context.TenantId, context.PromptText, allTables, topK: 3, ct: ct);
 
        var primaryTable = PickPrimaryTable(relevantTables, context.PromptText);
        if (primaryTable is null) { context.SchemaMap = null; return; }
 
        // Only look for a join if the dimension ISN'T already sitting on the table we
        // already picked. Previously this always searched other tables first, so any
        // other nearby table with a same-named column (e.g. "name") triggered a join
        // that was never needed — this is what caused the unwanted "tickets" join
        // when asking for "categories by name" (categories.name already exists).
        var joins = new List<string>();
        string? dimensionColumnType = null;
        string? resolvedDimensionColumn = null;
        List<string>? dimensionCandidates = null;
        var dimensionResolved = true;
 
        if (context.Intent.Dimension is not null)
        {
            var dimension = context.Intent.Dimension;
            dimensionResolved = false;
 
            // Tier 1: foreign-key match on the primary table, checked FIRST — e.g.
            // dimension "category" -> FK column "category_id" pointing at a
            // "categories" table. Also matches when the intent parser already
            // extracted the FK id column name directly (dimension == "category_id"
            // itself, since that's a real column it saw in the schema) — checking
            // this before a plain exact-column match is exactly what makes "grouped
            // by category" resolve to the human-readable categories.name instead of
            // silently accepting the raw numeric category_id as the final answer.
            var fkMatches = primaryTable.Relationships.Where(r =>
                r.FromColumn.Equals(dimension, StringComparison.OrdinalIgnoreCase) ||
                r.FromColumn.Equals($"{dimension}_id", StringComparison.OrdinalIgnoreCase))
                .ToList();
 
            if (fkMatches.Count == 1)
            {
                var rel = fkMatches[0];
                var relatedTable = allTables.FirstOrDefault(t =>
                    t.TableName.Equals(rel.ToTable, StringComparison.OrdinalIgnoreCase));
                var displayColumn = relatedTable is not null ? FindDisplayColumn(relatedTable) : null;
 
                if (relatedTable is not null && displayColumn is not null)
                {
                    // Tier 2: prefer the related table's display column (e.g.
                    // categories.name) over the raw numeric FK id, matching the
                    // same preference SqlGenerationStep's join rule already states.
                    joins = _schemaGraph.FindJoinPath(primaryTable.TableName, relatedTable.TableName) ?? new();
                    resolvedDimensionColumn = $"{relatedTable.TableName}.{displayColumn.ColumnName}";
                    dimensionColumnType = displayColumn.DataType;
                }
                else
                {
                    // FK exists but the related table has no good display column
                    // (or its metadata wasn't found) — fall back to the FK column
                    // itself rather than failing outright.
                    var fkColumn = primaryTable.Columns.FirstOrDefault(c =>
                        c.ColumnName.Equals(rel.FromColumn, StringComparison.OrdinalIgnoreCase));
                    resolvedDimensionColumn = rel.FromColumn;
                    dimensionColumnType = fkColumn?.DataType;
                }
                dimensionResolved = true;
            }
            else if (fkMatches.Count > 1)
            {
                // Genuinely ambiguous — more than one FK plausibly matches (e.g.
                // "user" -> created_by_id or updated_by_id). Don't guess: surface
                // the real column names so TableColumnConfirmationStep can ask.
                dimensionCandidates = fkMatches.Select(r => r.FromColumn).ToList();
            }
            else
            {
                // Not a foreign key — Tier 3: plain exact column match on the
                // primary table itself (e.g. dimension "status" -> tickets.status).
                var ownColumn = primaryTable.Columns.FirstOrDefault(c =>
                    c.ColumnName.Equals(dimension, StringComparison.OrdinalIgnoreCase));
 
                if (ownColumn is not null)
                {
                    resolvedDimensionColumn = ownColumn.ColumnName;
                    dimensionColumnType = ownColumn.DataType;
                    dimensionResolved = true;
                }
                else
                {
                    // Tier 4 (last resort, existing behavior): the dimension might be
                    // an exact column name on a different table the vector search
                    // already pulled in, unrelated to any FK on the primary table.
                    var dimTable = relevantTables.FirstOrDefault(t =>
                        t.TableName != primaryTable.TableName &&
                        t.Columns.Any(c => c.ColumnName.Equals(dimension, StringComparison.OrdinalIgnoreCase)));
                    if (dimTable is not null)
                    {
                        joins = _schemaGraph.FindJoinPath(primaryTable.TableName, dimTable.TableName) ?? new();
                        resolvedDimensionColumn = dimension;
                        dimensionColumnType = dimTable.Columns
                            .FirstOrDefault(c => c.ColumnName.Equals(dimension, StringComparison.OrdinalIgnoreCase))
                            ?.DataType;
                        dimensionResolved = true;
                    }
                    // Else: nothing matched anywhere — dimensionResolved stays false,
                    // dimensionCandidates stays null. TableColumnConfirmationStep ends
                    // the pipeline with a "not found" response instead of asking a
                    // clarifying question the schema can't actually answer.
                }
            }
        }
 
       context.SchemaMap = new ResolvedSchemaMap
{
    Table = primaryTable.TableName,
    DimensionColumn = resolvedDimensionColumn,
    Confidence = joins.Count > 0 ? "medium" : "high",
    JoinClauses = joins,
    SchemaDdl = BuildSchemaJson(relevantTables),
    CandidateTables = relevantTables.Select(t => t.TableName).ToList(),
    DimensionColumnType = dimensionColumnType,
    DimensionResolved = dimensionResolved,
    DimensionCandidates = dimensionCandidates
};
    }
 
    private static ColumnMetadata? FindDisplayColumn(TableMetadata table)
    {
        // Prefer an obviously "human readable" column, in priority order.
        foreach (var preferred in new[] { "name", "title", "label", "description" })
        {
            var match = table.Columns.FirstOrDefault(c =>
                c.ColumnName.Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        // Fall back to the first non-primary-key text-like column.
        return table.Columns.FirstOrDefault(c => !c.IsPrimaryKey && IsTextLike(c.DataType));
    }
 
    private static bool IsTextLike(string dataType) =>
        dataType is "character varying" or "varchar" or "text" or "char" or "character";
 
    private static TableMetadata? PickPrimaryTable(List<TableMetadata> tables, string promptText)
    {
        var lower = promptText.ToLowerInvariant();
        // Match table name mentioned in prompt (handles plural: "tickets" -> "ticket").
        // Deliberately NO further fallback here (e.g. tables.FirstOrDefault()) — if the
        // prompt doesn't actually name a real table, guessing the first/closest one by
        // vector similarity is what let "show incidents by priority" (no incidents
        // table exists) silently return data from some unrelated table instead of
        // telling the user nothing matched. Returning null here is what lets
        // TableColumnConfirmationStep respond with "no data found" instead.
        return tables.FirstOrDefault(t => lower.Contains(t.TableName.ToLowerInvariant()))
            ?? tables.FirstOrDefault(t => lower.Contains(t.TableName.TrimEnd('s').ToLowerInvariant()));
    }
 
    private string BuildSchemaJson(List<TableMetadata> tables)
    {
        var schemaObj = new
        {
            tables = tables.Select(t => new
            {
                name = t.TableName,
                columns = t.Columns.Select(c => new
                {
                    name        = c.ColumnName,
                    type        = c.DataType,
                    primary_key = c.IsPrimaryKey
                }),
                relationships = t.Relationships.Select(r => new
                {
                    from_column = r.FromColumn,
                    to_table    = r.ToTable,
                    to_column   = r.ToColumn
                })
            })
        };
        return JsonSerializer.Serialize(schemaObj, new JsonSerializerOptions { WriteIndented = false });
    }
}