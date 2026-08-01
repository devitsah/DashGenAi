using AI_Dashboard.Domain.Entities;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface IQueryRepository
{
    Task<Query> AddAsync(Query query, CancellationToken ct = default);
    Task<Query?> GetByWidgetIdAsync(short tenantId, int widgetId, CancellationToken ct = default);
}