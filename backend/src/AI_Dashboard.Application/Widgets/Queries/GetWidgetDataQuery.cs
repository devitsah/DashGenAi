using AI_Dashboard.Application.Common.Interfaces;
using MediatR;

namespace AI_Dashboard.Application.Widgets.Queries;

public record GetWidgetDataQuery(short TenantId, int UserId, int WidgetId) : IRequest<List<Dictionary<string, object?>>>;

public class GetWidgetDataQueryHandler : IRequestHandler<GetWidgetDataQuery, List<Dictionary<string, object?>>>
{
    private readonly IWidgetRepository _widgetRepo;
    private readonly IQueryRepository _queryRepo;
    private readonly IQueryExecutor _executor;

    public GetWidgetDataQueryHandler(IWidgetRepository widgetRepo, IQueryRepository queryRepo, IQueryExecutor executor)
    {
        _widgetRepo = widgetRepo;
        _queryRepo = queryRepo;
        _executor = executor;
    }

    public async Task<List<Dictionary<string, object?>>> Handle(GetWidgetDataQuery request, CancellationToken ct)
    {
        var widget = await _widgetRepo.GetByIdAsync(request.TenantId, request.WidgetId, ct)
            ?? throw new KeyNotFoundException($"Widget {request.WidgetId} not found.");

        // Ownership check - a widget belongs to a dashboard, which belongs to a user.
        // Without this, any authenticated user could read any other user's widget data
        // simply by guessing widget IDs.
        if (widget.Dashboard.UserId != request.UserId)
            throw new UnauthorizedAccessException("You do not have access to this widget.");

        var query = await _queryRepo.GetByWidgetIdAsync(request.TenantId, request.WidgetId, ct)
            ?? throw new KeyNotFoundException($"No query found for widget {request.WidgetId}.");

        return await _executor.ExecuteAsync(query.SqlQuery, ct);
    }
}