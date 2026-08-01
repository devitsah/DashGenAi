using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Dashboards.Commands;
using AI_Dashboard.Application.Dashboards.Queries;
using AI_Dashboard.Shared.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI_Dashboard.Api.Controllers;

[ApiController]
[Route("api/dashboards")]
[Authorize]
public class DashboardsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public DashboardsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var dashboards = await _mediator.Send(new GetDashboardListQuery(_currentUser.TenantId, _currentUser.UserId));
        return Ok(dashboards.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetDashboardByIdQuery(_currentUser.TenantId, id));
        if (result is null) return NotFound();
        if (result.UserId != _currentUser.UserId) return Forbid();

        return Ok(MapToDto(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDashboardRequest request)
    {
        var id = await _mediator.Send(new CreateDashboardCommand(_currentUser.TenantId, _currentUser.UserId, request.Name, request.Description, request.Style));
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Edit(int id, [FromBody] EditDashboardRequest request)
    {
        var dashboard = await _mediator.Send(new GetDashboardByIdQuery(_currentUser.TenantId, id));
        if (dashboard is null) return NotFound();
        if (dashboard.UserId != _currentUser.UserId) return Forbid();

        await _mediator.Send(new RenameDashboardCommand(_currentUser.TenantId, id, request.Name, request.Description));
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var dashboard = await _mediator.Send(new GetDashboardByIdQuery(_currentUser.TenantId, id));
        if (dashboard is null) return NotFound();
        if (dashboard.UserId != _currentUser.UserId) return Forbid();

        await _mediator.Send(new DeleteDashboardCommand(_currentUser.TenantId, id));
        return NoContent();
    }

    [HttpGet("default")]
    public async Task<IActionResult> GetDefault()
    {
        var result = await _mediator.Send(new GetDefaultDashboardQuery(_currentUser.TenantId, _currentUser.UserId));
        if (result is null) return NotFound();

        return Ok(MapToDto(result));
    }

    [HttpPost("default/{id}")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var dashboard = await _mediator.Send(new GetDashboardByIdQuery(_currentUser.TenantId, id));
        if (dashboard is null) return NotFound();
        if (dashboard.UserId != _currentUser.UserId) return Forbid();

        await _mediator.Send(new SetDefaultDashboardCommand(_currentUser.TenantId, _currentUser.UserId, id));
        return NoContent();
    }

    [HttpPost("{id}/clone")]
    public async Task<IActionResult> Clone(int id)
    {
        var dashboard = await _mediator.Send(new GetDashboardByIdQuery(_currentUser.TenantId, id));
        if (dashboard is null) return NotFound();
        if (dashboard.UserId != _currentUser.UserId) return Forbid();

        var newId = await _mediator.Send(new CloneDashboardCommand(_currentUser.TenantId, _currentUser.UserId, id));
        return CreatedAtAction(nameof(GetById), new { id = newId }, new { id = newId });
    }

    // MUST stay inside the class - it's an instance method, not a free function
    private static DashboardDto MapToDto(Domain.Entities.Dashboard d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Description = d.Description,
        Visibility = d.Visibility,
        IsDefault = d.IsDefault,
        DashboardStyle = d.DashboardStyle,
        VersionNo = d.VersionNo,
        Widgets = d.Widgets.Select(w => new WidgetDto
        {
            Id = w.Id,
            DashboardId = w.DashboardId,
            Title = w.Title,
            WidgetType = w.WidgetType,
            Width = w.Width,
            Height = w.Height,
            PositionX = w.PositionX,
            PositionY = w.PositionY,
            RefreshInterval = w.RefreshInterval,
            DataSource = w.DataSource,
            GeneratedSql = w.Queries.FirstOrDefault()?.SqlQuery,
            BackgroundColor = ExtractBackgroundColor(w.ConfigJson),
ConfigJson = w.ConfigJson
}).ToList()
    };

    // ConfigJson is a free-form jsonb bucket on the widget - custom-dashboard
    // per-widget background color lives there as { "backgroundColor": "#RRGGBB" }
    // so it survives without a schema migration.
    private static string? ExtractBackgroundColor(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty("backgroundColor", out var el) &&
                el.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return el.GetString();
            }
        }
        catch
        {
            // malformed/legacy config json - just treat as "no custom color"
        }
        return null;
    }
}

// fine to live outside the class - it's a top-level record, not a member
public record CreateDashboardRequest(string Name, string? Description, string? Style);
public record EditDashboardRequest(string Name, string? Description);