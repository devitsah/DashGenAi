using AI_Dashboard.Domain.Entities;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface IDashboardRepository
{
    Task<Dashboard?> GetByIdAsync(short tenantId, int id, CancellationToken ct = default);
    Task<List<Dashboard>> GetListByUserAsync(short tenantId, int userId, CancellationToken ct = default);
    Task<Dashboard> AddAsync(Dashboard dashboard, CancellationToken ct = default);
    Task UpdateAsync(Dashboard dashboard, CancellationToken ct = default);
    Task DeleteAsync(short tenantId, int id, CancellationToken ct = default);
    Task<Dashboard?> GetDefaultByUserAsync(short tenantId, int userId, CancellationToken ct = default);
    Task SetDefaultAsync(short tenantId, int userId, int dashboardId, CancellationToken ct = default);
}