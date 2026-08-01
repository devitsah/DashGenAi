using System.Text.Json;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Common.Helpers;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

public class LayoutGenerationStep : IPipelineStep
{
    private readonly ILayoutEngine _layoutEngine;
    private readonly IOllamaClient _ollama;
    private readonly string _coderModel;

    private const string SystemPrompt =
        "You are a dashboard layout expert. Output JSON only, no explanation, no markdown.\n" +
        "Output schema: {\"widget_title\": string, \"widget_type\": \"KpiCard\"|\"Table\"|\"BarChart\"|\"PieChart\"|\"LineChart\", \"width\": 3-12, \"height\": 2-6}";

    public LayoutGenerationStep(ILayoutEngine layoutEngine, IOllamaClient ollama, IOllamaModelOptions modelOpts)
    {
        _layoutEngine = layoutEngine;
        _ollama       = ollama;
        _coderModel   = modelOpts.CoderModel;
    }

    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (context.Intent is null || context.SchemaMap is null || context.NeedsClarification
            || string.IsNullOrWhiteSpace(context.GeneratedSql))
            return;

        var style = context.Intent.DashboardStyle switch
        {
            "power_bi" => "PowerBi",
            "grafana"  => "Grafana",
            _          => "Custom"
        };

        WidgetHints? hints = null;
        try
        {
            var userPrompt =
                $"Dashboard style: {context.Intent.DashboardStyle}\n" +
                $"Visualization: {context.Intent.VisualizationType}\n" +
                $"Metric: {context.Intent.Metric}\n" +
                $"Dimension: {context.Intent.Dimension ?? "none"}\n" +
                $"User request: {context.PromptText}\n\nWidget JSON:";

            var raw  = await _ollama.GenerateWithModelAsync(_coderModel, SystemPrompt, userPrompt, 256, ct);
            var json = ResponseJsonParser.ExtractJson(raw);
            hints = JsonSerializer.Deserialize<WidgetHints>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* fall through to layout engine heuristics */ }

        context.DashboardSpec = _layoutEngine.BuildLayout(
            context.Intent, context.SchemaMap, style, hints, context.GeneratedSql);
    }

    public record WidgetHints(string? WidgetTitle, string? WidgetType, int Width, int Height);
}
