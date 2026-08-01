using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI_Dashboard.Api.Controllers;

[ApiController]
[Route("api/clarifications")]
[Authorize]
public class ClarificationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ClarificationController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost("{promptId}/answer")]
    public async Task<IActionResult> Answer(int promptId, [FromBody] AnswerClarificationRequest request)
    {
        // promptId here is the ID of the prompt that ASKED the clarification question.
        // Pass it as ParentPromptId so the handler chains it correctly.
        var result = await _mediator.Send(new ProcessPromptCommand(
            _currentUser.TenantId, _currentUser.UserId, request.DashboardId, request.AnswerText, promptId));
        return Ok(result);
    }
}

public record AnswerClarificationRequest(string AnswerText, int? DashboardId);