using AI_Dashboard.Domain.Entities;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface IWidgetRepository
{
    Task<Widget> AddAsync(Widget widget, CancellationToken ct = default);
    Task<List<Widget>> GetByDashboardIdAsync(short tenantId, int dashboardId, CancellationToken ct = default);
    Task<Widget?> GetByIdAsync(short tenantId, int id, CancellationToken ct = default);
    Task UpdateAsync(Widget widget, CancellationToken ct = default);
    Task DeleteAsync(short tenantId, int id, CancellationToken ct = default);
}