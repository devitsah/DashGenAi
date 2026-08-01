using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Commands;

// Creates a brand-new dashboard (own Id, own row) that is a deep copy of an
// existing one: same style/definition + every widget + each widget's query.
// The clone always starts as non-default and Private, regardless of the
// source dashboard's flags - the user explicitly promotes it later if wanted.
public record CloneDashboardCommand(short TenantId, int UserId, int SourceDashboardId) : IRequest<int>;

public class CloneDashboardCommandHandler : IRequestHandler<CloneDashboardCommand, int>
{
    private readonly IDashboardRepository _dashboardRepo;
    private readonly IWidgetRepository _widgetRepo;
    private readonly IQueryRepository _queryRepo;

    public CloneDashboardCommandHandler(
        IDashboardRepository dashboardRepo,
        IWidgetRepository widgetRepo,
        IQueryRepository queryRepo)
    {
        _dashboardRepo = dashboardRepo;
        _widgetRepo = widgetRepo;
        _queryRepo = queryRepo;
    }

    public async Task<int> Handle(CloneDashboardCommand request, CancellationToken ct)
    {
        var source = await _dashboardRepo.GetByIdAsync(request.TenantId, request.SourceDashboardId, ct)
            ?? throw new KeyNotFoundException($"Dashboard {request.SourceDashboardId} not found.");

        if (source.UserId != request.UserId)
            throw new UnauthorizedAccessException("You do not have access to this dashboard.");

        var clone = await _dashboardRepo.AddAsync(new Dashboard
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            Visibility = "Private",
            DashboardStyle = source.DashboardStyle,
            IsDefault = false,
            DefinitionJson = source.DefinitionJson,
            CreatedBy = request.UserId
        }, ct);

        foreach (var widget in source.Widgets)
        {
            var newWidget = await _widgetRepo.AddAsync(new Widget
            {
                TenantId = request.TenantId,
                DashboardId = clone.Id,
                Title = widget.Title,
                WidgetType = widget.WidgetType,
                Width = widget.Width,
                Height = widget.Height,
                PositionX = widget.PositionX,
                PositionY = widget.PositionY,
                RefreshInterval = widget.RefreshInterval,
                DataSource = widget.DataSource,
                ConfigJson = widget.ConfigJson,
                CreatedBy = request.UserId
            }, ct);

            var sourceQuery = widget.Queries.FirstOrDefault();
            if (sourceQuery is not null)
            {
                await _queryRepo.AddAsync(new Query
                {
                    TenantId = request.TenantId,
                    DashboardId = clone.Id,
                    WidgetId = newWidget.Id,
                    PromptId = sourceQuery.PromptId,
                    SqlQuery = sourceQuery.SqlQuery,
                    CreatedBy = request.UserId
                }, ct);
            }
        }

        return clone.Id;
    }
}