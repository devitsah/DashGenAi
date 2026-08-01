using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

public class VisualizationRecommendationStep : IPipelineStep
{
    private readonly IQueryExecutor _executor;

    public VisualizationRecommendationStep(IQueryExecutor executor) => _executor = executor;

    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (context.GeneratedSql is null || context.SchemaMap is null) return;

        var executableSql = context.GeneratedSql.Replace(
            ":tenantId", context.TenantId.ToString(), StringComparison.OrdinalIgnoreCase);

        List<Dictionary<string, object?>> rows;
        try { rows = await _executor.ExecuteAsync(executableSql, ct); }
        catch (Exception ex)
        {
            // Runtime-only failure (SQL passed EXPLAIN validation in SqlRepairStep but
            // errored against real data). Surface it via ExecutionError instead of
            // silently leaving QueryResultRows null — otherwise the widget/query still
            // gets created and the frontend renders an empty visualization matrix with
            // no indication anything went wrong.
            context.ExecutionError = FriendlyMessage(ex);
            return;
        }

        context.QueryResultRowCount = rows.Count;
        context.QueryResultRows = rows;

        // By this point ClarificationStep has already guaranteed VisualizationType is
        // resolved (that's what unblocks SqlGenerationStep in the first place) — so a
        // confirmed choice like "KPI Card" must never be overridden here.
        if (context.Intent?.VisualizationType is not null)
            return;

        var hasDimension = context.SchemaMap.DimensionColumn is not null;
        var recommended = rows.Count switch
        {
            0 or 1 when !hasDimension => "kpi_card",
            <= 6  when hasDimension    => "pie_chart",
            <= 30 when hasDimension    => "bar_chart",
            _     when hasDimension    => "table",
            _                          => "kpi_card"
        };

        if (context.Intent is not null)
            context.Intent.VisualizationType = recommended;
    }

    // Npgsql/driver messages are technical (column type, cast, timeout, etc). Keep
    // the raw message for logging elsewhere, but give the frontend something a
    // non-technical user can read in the Visualization Matrix empty-state banner.
    private static string FriendlyMessage(Exception ex) =>
        $"The query couldn't be run against the data: {ex.Message}";
}