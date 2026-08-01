using AI_Dashboard.Application.PromptProcessing.Models;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline;

public class PromptPipelineContext
{
    public short TenantId { get; set; }
    public int UserId { get; set; }
    public int PromptId { get; set; }
    public string PromptText { get; set; } = default!;

    // LLM-normalized version of PromptText with typos/synonyms replaced by real
    // schema names. Used for vector search in SemanticIntentStep and
    // SchemaResolutionStep. PromptText is kept unchanged for SQL generation.
    public string NormalizedPromptText { get; set; } = default!;

    public IntentResult? PreSeededIntent { get; set; }
    public IntentResult? Intent { get; set; }
    public ResolvedSchemaMap? SchemaMap { get; set; }
    public ClarificationRequest? Clarification { get; set; }

    public string? GeneratedSql { get; set; }
    public int QueryResultRowCount { get; set; }
    public List<Dictionary<string, object?>>? QueryResultRows { get; set; }

    // Set by VisualizationRecommendationStep when the generated SQL passed EXPLAIN
    // validation (SqlRepairStep) but still failed at actual execution time (runtime-only
    // errors: division by zero, bad casts on real data, timeouts, etc). Previously this
    // failure was swallowed silently, leaving QueryResultRows null with no explanation —
    // the widget/query would still be created and the frontend would just render an
    // empty visualization matrix with no indication anything went wrong.
    public string? ExecutionError { get; set; }

    public GeneratedDashboardSpec? DashboardSpec { get; set; }
    public List<string>? Suggestions { get; set; }

    public bool NeedsClarification => Clarification is not null;

    // True only when PromptGuardStep rejected the prompt (injection / destructive
    // SQL / jailbreak attempt) as opposed to a normal "need more info" clarification.
    // Kept separate from Clarification so the handler/frontend can treat it as a
    // terminal, non-chainable result instead of something to answer.
    public bool IsBlocked { get; set; }
}