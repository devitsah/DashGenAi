namespace AI_Dashboard.Application.PromptProcessing.Pipeline;

public interface IPipelineStep
{
    Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default);
}