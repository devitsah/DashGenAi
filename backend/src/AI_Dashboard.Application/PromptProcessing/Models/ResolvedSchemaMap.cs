namespace AI_Dashboard.Application.PromptProcessing.Models;

public class ResolvedSchemaMap
{
    public string Table { get; set; } = default!;
    public string? DimensionColumn { get; set; }
    public string Confidence { get; set; } = "low";
    public string SchemaDdl { get; set; } = string.Empty;
    public List<string> JoinClauses { get; set; } = new();

    // Other tables the vector search considered relevant, for the "No, I meant a
    // different table" fallback in TableColumnConfirmationStep.
    public List<string> CandidateTables { get; set; } = new();

    // The real Postgres data type of DimensionColumn (e.g. "timestamp", "character
    // varying"), used by VisualizationCompatibilityStep to check things like
    // "does a line chart's dimension actually look like a date?"
    public string? DimensionColumnType { get; set; }

    // The table DimensionColumn actually lives on. Equal to Table when the
    // dimension is on the primary table itself; different when it was resolved
    // via a join (e.g. Table="tickets", DimensionColumn="name", DimensionTable=
    // "agents"). Used by TableColumnConfirmationStep so the confirmation message
    // doesn't wrongly claim a joined column lives on the primary table.
   public bool DimensionResolved { get; set; }=true;

    // table name -> best "human readable" column on that table (e.g. "agents" ->
    // "name", "tickets" -> "subject"). Computed deterministically in
    // SchemaResolutionStep for every relevant table (primary AND joined), so
    // SqlGenerationStep can tell the model to SELECT a readable value instead of
    // a bare numeric id — this is the fix for "shows id instead of name".
   public List<string>?DimensionCandidates { get; set; }
}