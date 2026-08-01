using AI_Dashboard.Application.PromptProcessing.Models;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

/// <summary>
/// Checks whether the visualization type the user asked for actually makes sense for
/// the resolved data shape — e.g. a line chart needs a real date/time column to plot
/// a trend against; a KPI card shows one number, so it can't also be grouped by a
/// dimension. Runs after ClarificationStep, once both the final viz type and the
/// real column type are known. No LLM call — plain rule checks, instant, same style
/// as TableColumnConfirmationStep.
/// </summary>
public class VisualizationCompatibilityStep : IPipelineStep
{
    public Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (context.SchemaMap is null || context.Intent is null || context.Intent.VisualizationType is null)
            return Task.CompletedTask;

        if (context.Intent.VisualizationCompatibilityConfirmed)
            return Task.CompletedTask;

        var viz            = context.Intent.VisualizationType;
        var dimension      = context.SchemaMap.DimensionColumn;
        var dimensionType  = context.SchemaMap.DimensionColumnType;
        var isAggregation  = context.Intent.IsAggregation;

        var label = VisualizationTypeMapper.AllLabels()
            .FirstOrDefault(l => VisualizationTypeMapper.ToIntentValue(l) == viz) ?? viz;

        string? reason = viz switch
        {
            _ when !isAggregation && viz != "table" =>
                $"{label} needs grouped or aggregated data, but this is a plain list of records with nothing being counted or combined.",
            "line_chart" when dimension is not null && !IsDateLike(dimensionType) =>
                $"Line charts work best with a date/time field to show a trend, but '{dimension}' isn't one.",
            "kpi_card" when dimension is not null =>
                $"A KPI card shows a single number, but grouping by '{dimension}' would produce several values.",
            "bar_chart" or "pie_chart" or "line_chart" when dimension is null =>
                $"{label} needs something to group by, but this is a single total with nothing to compare.",
            _ => null
        };

        if (reason is null) return Task.CompletedTask;

        context.Clarification = new ClarificationRequest
        {
            Question = $"{reason} Do you want to continue with {label} anyway, or pick a different chart type?",
            Options  = ["Continue anyway", "Pick a different chart"]
        };
        return Task.CompletedTask;
    }

    private static bool IsDateLike(string? dataType) =>
        dataType is not null &&
        (dataType.StartsWith("timestamp", StringComparison.OrdinalIgnoreCase) ||
         dataType.Equals("date", StringComparison.OrdinalIgnoreCase));
}