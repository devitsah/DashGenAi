using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

public class SqlRepairStep : IPipelineStep
{
    private readonly IOllamaClient _ollama;
    private readonly ISqlValidator _validator;
    private readonly string _coderModel;

    private const string RepairSystemPrompt =
        "You are a PostgreSQL expert. The SQL below failed validation. " +
        "Fix it and output ONLY the corrected SQL, no explanation, no markdown.";

    public SqlRepairStep(IOllamaClient ollama, ISqlValidator validator, IOllamaModelOptions modelOpts)
    {
        _ollama     = ollama;
        _validator  = validator;
        _coderModel = modelOpts.CoderModel;
    }

    public async Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.GeneratedSql) || context.NeedsClarification) return;

        var error = await _validator.ValidateAsync(context.GeneratedSql, ct);
        if (error is null) return; // happy path

        var repaired = await _ollama.GenerateWithModelAsync(
            _coderModel,
            RepairSystemPrompt,
            $"Error: {error}\n\nSQL:\n{context.GeneratedSql}\n\nFixed SQL:",
            512, ct);

        repaired = StripFences(repaired.Trim());
        if (string.IsNullOrWhiteSpace(repaired)) { context.GeneratedSql = null; return; }

        if (await _validator.ValidateAsync(repaired, ct) is null)
            context.GeneratedSql = repaired;
        else
            context.GeneratedSql = null;
    }

    private static string StripFences(string raw)
    {
        var m = System.Text.RegularExpressions.Regex.Match(raw, @"```(?:\w+)?\s*([\s\S]*?)```");
        return m.Success ? m.Groups[1].Value.Trim() : raw;
    }
}