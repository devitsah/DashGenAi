using AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline;

public class PromptPipelineOrchestrator
{
    private readonly PromptGuardStep _guardStep;
    private readonly SemanticIntentStep _intentStep;
    private readonly SchemaResolutionStep _schemaStep;
    private readonly TableColumnConfirmationStep _tableColumnConfirmStep;
    private readonly ClarificationStep _clarificationStep;
    private readonly VisualizationCompatibilityStep _vizCompatStep;
    private readonly SqlGenerationStep _sqlGenStep;
    private readonly JoinTenantGuardStep _joinTenantGuardStep;
    private readonly SqlRepairStep _sqlRepairStep;
    private readonly VisualizationRecommendationStep _vizStep;
    private readonly LayoutGenerationStep _layoutStep;
    private readonly SuggestionsStep _suggestionsStep;

    public PromptPipelineOrchestrator(
        PromptGuardStep guardStep, SemanticIntentStep intentStep, SchemaResolutionStep schemaStep,
        TableColumnConfirmationStep tableColumnConfirmStep,
        ClarificationStep clarificationStep, VisualizationCompatibilityStep vizCompatStep,
        SqlGenerationStep sqlGenStep, JoinTenantGuardStep joinTenantGuardStep, SqlRepairStep sqlRepairStep,
        VisualizationRecommendationStep vizStep, LayoutGenerationStep layoutStep, SuggestionsStep suggestionsStep)
    {
        _guardStep = guardStep; _intentStep = intentStep; _schemaStep = schemaStep;
        _tableColumnConfirmStep = tableColumnConfirmStep;
        _clarificationStep = clarificationStep; _vizCompatStep = vizCompatStep;
        _sqlGenStep = sqlGenStep; _joinTenantGuardStep = joinTenantGuardStep; _sqlRepairStep = sqlRepairStep;
        _vizStep = vizStep; _layoutStep = layoutStep; _suggestionsStep = suggestionsStep;
    }

    public async Task<PromptPipelineContext> RunAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        await _guardStep.ExecuteAsync(context, ct);
        if (context.NeedsClarification) return context;

        await _intentStep.ExecuteAsync(context, ct);
        await _schemaStep.ExecuteAsync(context, ct);

        await _tableColumnConfirmStep.ExecuteAsync(context, ct);
        if (context.NeedsClarification) return context;

        await _clarificationStep.ExecuteAsync(context, ct);
        if (context.NeedsClarification) return context;

        await _vizCompatStep.ExecuteAsync(context, ct);
        if (context.NeedsClarification) return context;

        await _sqlGenStep.ExecuteAsync(context, ct);
        await _joinTenantGuardStep.ExecuteAsync(context, ct);
        await _sqlRepairStep.ExecuteAsync(context, ct);
        // SqlRepairStep can fully regenerate the SQL via a fresh LLM call when the
        // first attempt fails validation — that regenerated SQL never passed through
        // the tenant guard above. Re-run it here so a repaired query can't silently
        // reintroduce a missing tenant condition. Idempotent/cheap (regex only, no
        // LLM call) so this is a no-op if repair didn't touch the SQL.
        await _joinTenantGuardStep.ExecuteAsync(context, ct);
        await _vizStep.ExecuteAsync(context, ct);
        await _layoutStep.ExecuteAsync(context, ct);
        await _suggestionsStep.ExecuteAsync(context, ct);

        return context;
    }
}