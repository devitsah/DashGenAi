using System.Text.Json;
using AI_Dashboard.Application.Common.Interfaces;
 
namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;
 
public class SuggestionsStep : IPipelineStep
{
    private readonly IOllamaClient _ollama;
    private readonly ISchemaIntrospectionService _schemaService;
 
    public SuggestionsStep(IOllamaClient ollama, ISchemaIntrospectionService schemaService)
    {
        _ollama = ollama;
        _schemaService = schemaService;
    }
 
    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (context.Intent is null || context.DashboardSpec is null || context.NeedsClarification) return;
 
        try
        {
            var schema = await _schemaService.GetSchemaDescriptionAsync(context.TenantId, ct);
            var widgetType = context.DashboardSpec.WidgetType.ToString();
            var system = "You are a dashboard suggestion engine. Output ONLY a JSON array of strings. No explanation, no markdown.";
            var user =
                $"Schema:\n{schema}\n" +
                $"The user just created a {widgetType} showing {context.Intent.Metric ?? "row count"} " +
                $"{(context.Intent.Dimension is not null ? $"by {context.Intent.Dimension}" : string.Empty)}.\n" +
                $"Suggest 5 additional widgets they may want to add next. " +
                $"Output a JSON array of SHORT human-readable names only (max 5 words each). " +
                $"Do not suggest the same widget they just created.";
 
            var raw   = await _ollama.GenerateAsync(system, user, 200, ct);
            var start = raw.IndexOf('[');
            var end   = raw.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(raw[start..(end + 1)]);
                if (parsed is { Count: > 0 }) context.Suggestions = parsed;
            }
        }
        catch { /* non-fatal */ }
    }
}