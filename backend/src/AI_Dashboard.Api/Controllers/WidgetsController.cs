using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Widgets.Commands;
using AI_Dashboard.Application.Widgets.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI_Dashboard.Api.Controllers;

[ApiController]
[Route("api/widgets")]
[Authorize]
public class WidgetsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly AI_Dashboard.Application.Common.Interfaces.IWidgetRepository _widgetRepo;
    private readonly AI_Dashboard.Application.Common.Interfaces.IDashboardRepository _dashboardRepo;

    public WidgetsController(
        IMediator mediator,
        ICurrentUserService currentUser,
        AI_Dashboard.Application.Common.Interfaces.IWidgetRepository widgetRepo,
        AI_Dashboard.Application.Common.Interfaces.IDashboardRepository dashboardRepo)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _widgetRepo = widgetRepo;
        _dashboardRepo = dashboardRepo;
    }

    // Add Widget dialog in the Designer. Every chart type funnels through here;
    // CreateWidgetCommand validates which fields are actually required for the
    // chosen type (e.g. HTML widgets skip table/column entirely).
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWidgetRequest request)
    {
        try
        {
            var id = await _mediator.Send(new CreateWidgetCommand(
                _currentUser.TenantId,
                _currentUser.UserId,
                request.DashboardId,
                request.Title,
                request.WidgetType,
                request.Table,
                request.GroupByColumn,
                request.MetricColumn,
                request.Aggregation,
                request.TimeColumn,
                request.Interval,
                request.HtmlContent,
                request.DisplayOptions));

            var widget = await _widgetRepo.GetByIdAsync(_currentUser.TenantId, id);

            return CreatedAtAction(nameof(GetData), new { id }, new
            {
                id,
                title = widget!.Title,
                widgetType = widget.WidgetType,
                width = widget.Width,
                height = widget.Height,
                positionX = widget.PositionX,
                positionY = widget.PositionY,
                configJson = widget.ConfigJson
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // This is the endpoint the frontend's kpi-card/table/bar-chart widget components call
    // to actually render data - closes the "Render dashboards" Phase 1 requirement.
    [HttpGet("{id}/data")]
    public async Task<IActionResult> GetData(int id)
    {
        try
        {
            var rows = await _mediator.Send(new GetWidgetDataQuery(_currentUser.TenantId, _currentUser.UserId, id));
            return Ok(rows);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/position")]
    public async Task<IActionResult> UpdatePosition(int id, [FromBody] UpdateWidgetPositionRequest request)
    {
        var widget = await _widgetRepo.GetByIdAsync(_currentUser.TenantId, id);
        if (widget is null) return NotFound();
        if (widget.Dashboard.UserId != _currentUser.UserId) return Forbid();

        widget.PositionX = (short)request.PositionX;
        widget.PositionY = (short)request.PositionY;
        widget.Width     = (short)request.Width;
        widget.Height    = (short)request.Height;
        widget.UpdatedBy = _currentUser.UserId;
        await _widgetRepo.UpdateAsync(widget);
        return NoContent();
    }

    // Called when the user hits "Reject" in the AI Builder chat. The prompt pipeline
    // already persisted the widget (+ its query) as soon as it was generated, so simply
    // removing it from the chat's in-memory list was never enough - it still lived on the
    // dashboard. This actually deletes it server-side.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var widget = await _widgetRepo.GetByIdAsync(_currentUser.TenantId, id);
        if (widget is null) return NotFound();
        if (widget.Dashboard.UserId != _currentUser.UserId) return Forbid();

        await _widgetRepo.DeleteAsync(_currentUser.TenantId, id);

        // Keep the dashboard's grid layout JSON in sync so no stale entry for this
        // widget lingers around.
        await RemoveWidgetFromLayoutAsync(widget.DashboardId, id);

        return NoContent();
    }

    [HttpPut("{id}/title")]
    public async Task<IActionResult> UpdateTitle(int id, [FromBody] UpdateWidgetTitleRequest request)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 100)
            return BadRequest(new { message = "Title must be 1–100 characters." });

        var widget = await _widgetRepo.GetByIdAsync(_currentUser.TenantId, id);
        if (widget is null) return NotFound();
        if (widget.Dashboard.UserId != _currentUser.UserId) return Forbid();

        widget.Title = title;
        widget.UpdatedBy = _currentUser.UserId;
        await _widgetRepo.UpdateAsync(widget);
        return Ok(new { title = widget.Title });
    }

    // Called from the Staging Preview step (Custom dashboard style only) when the user
    // opens a widget's color panel, picks a color, and hits Commit/Submit. Stored inside
    // ConfigJson (jsonb, already exists on the table) instead of a new column so no
    // migration is needed - the dashboard view reads it back the same way.
    [HttpPut("{id}/style")]
    public async Task<IActionResult> UpdateStyle(int id, [FromBody] UpdateWidgetStyleRequest request)
    {
        var color = request.BackgroundColor?.Trim();
        if (string.IsNullOrEmpty(color) || !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
            return BadRequest(new { message = "backgroundColor must be a hex color like #RRGGBB." });

        var widget = await _widgetRepo.GetByIdAsync(_currentUser.TenantId, id);
        if (widget is null) return NotFound();
        if (widget.Dashboard.UserId != _currentUser.UserId) return Forbid();

        Dictionary<string, object> config;
        try
        {
            config = string.IsNullOrWhiteSpace(widget.ConfigJson)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(widget.ConfigJson) ?? new();
        }
        catch
        {
            config = new Dictionary<string, object>();
        }

        config["backgroundColor"] = color;
        widget.ConfigJson = System.Text.Json.JsonSerializer.Serialize(config);
        widget.UpdatedBy = _currentUser.UserId;
        await _widgetRepo.UpdateAsync(widget);

        return Ok(new { backgroundColor = color });
    }

    private async Task RemoveWidgetFromLayoutAsync(int dashboardId, int widgetId)
    {
        var dashboard = await _dashboardRepo.GetByIdAsync(_currentUser.TenantId, dashboardId);
        if (dashboard is null) return;

        try
        {
            using var existing = System.Text.Json.JsonDocument.Parse(dashboard.DefinitionJson);
            if (!existing.RootElement.TryGetProperty("widgets", out var arr)) return;

            var style = existing.RootElement.TryGetProperty("style", out var s) ? s.GetString() : null;
            var gridCols = existing.RootElement.TryGetProperty("gridCols", out var g) ? g.GetInt32() : 12;

            var remaining = arr.EnumerateArray()
                .Where(w => w.GetProperty("id").GetInt32() != widgetId)
                .Select(w => (object)new
                {
                    id   = w.GetProperty("id").GetInt32(),
                    type = w.GetProperty("type").GetString(),
                    x    = w.GetProperty("x").GetInt16(),
                    y    = w.GetProperty("y").GetInt16(),
                    w    = w.GetProperty("w").GetInt16(),
                    h    = w.GetProperty("h").GetInt16()
                }).ToList();

            dashboard.DefinitionJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                style,
                gridCols,
                widgets = remaining
            });
            await _dashboardRepo.UpdateAsync(dashboard);
        }
        catch
        {
            // Malformed/legacy layout JSON - not worth failing the delete over.
        }
    }
}

public record UpdateWidgetPositionRequest(int PositionX, int PositionY, int Width, int Height);
public record UpdateWidgetTitleRequest(string Title);
public record UpdateWidgetStyleRequest(string BackgroundColor);

public record CreateWidgetRequest(
    int DashboardId,
    string Title,
    string WidgetType,
    string? Table,
    string? GroupByColumn,
    string? MetricColumn,
    string? Aggregation,
    string? TimeColumn,
    string? Interval,
    string? HtmlContent,
    Dictionary<string, object?>? DisplayOptions);