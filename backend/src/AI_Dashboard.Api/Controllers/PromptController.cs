using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI_Dashboard.Api.Controllers;

[ApiController]
[Route("api/prompts")]
[Authorize]
public class PromptController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public PromptController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitPromptRequest request)
    {
        var result = await _mediator.Send(new ProcessPromptCommand(
            _currentUser.TenantId, _currentUser.UserId, request.DashboardId, request.PromptText, request.ParentPromptId));
        return Ok(result);
    }
}

public record SubmitPromptRequest(string PromptText, int? DashboardId, int? ParentPromptId);