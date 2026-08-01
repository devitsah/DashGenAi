using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI_Dashboard.Infrastructure.Postgres.Repositories;

public class WidgetRepository : IWidgetRepository
{
    private readonly AppDbContext _db;
    public WidgetRepository(AppDbContext db) => _db = db;

    public async Task<Widget> AddAsync(Widget widget, CancellationToken ct = default)
    {
        _db.Widgets.Add(widget);
        await _db.SaveChangesAsync(ct);
        return widget;
    }

    public Task<List<Widget>> GetByDashboardIdAsync(short tenantId, int dashboardId, CancellationToken ct = default) =>
        _db.Widgets
            .Where(w => w.TenantId == tenantId && w.DashboardId == dashboardId)
            .ToListAsync(ct);

    public Task<Widget?> GetByIdAsync(short tenantId, int id, CancellationToken ct = default) =>
        _db.Widgets
            .Include(w => w.Dashboard)
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == id, ct);

    public async Task UpdateAsync(Widget widget, CancellationToken ct = default)
    {
        widget.UpdatedAt = DateTime.UtcNow;
        _db.Widgets.Update(widget);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(short tenantId, int id, CancellationToken ct = default)
    {
        // queries.widget_id has ON DELETE CASCADE (see 09_cascade_delete.sql),
        // so removing the widget row cleans up its queries automatically.
        await _db.Database.ExecuteSqlRawAsync(
            """DELETE FROM widgets WHERE tenant_id = {0} AND id = {1}""", tenantId, id);
    }
}