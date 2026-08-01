using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing.Models;

namespace AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

public class PromptGuardStep : IPipelineStep
{
    private readonly IPromptGuard _guard;

    public PromptGuardStep(IPromptGuard guard) => _guard = guard;

    public Task ExecuteAsync(PromptPipelineContext context, CancellationToken ct = default)
    {
        var rejection = _guard.Check(context.PromptText);
        if (rejection is not null)
        {
            context.Clarification = new ClarificationRequest
            {
                Question = rejection switch
                {
                    "prompt_empty"      => "Please enter a prompt.",
                    "prompt_whitespace" => "Prompt cannot be empty.",
                    "prompt_too_long"   => "Prompt too long.",
                    _ => "I can't process that request. Please rephrase your question about the dashboard data."
                },
                Options = null
            };
            context.IsBlocked = true;
        }
        return Task.CompletedTask;
    }
}