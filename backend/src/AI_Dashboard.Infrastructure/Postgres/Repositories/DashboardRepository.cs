using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI_Dashboard.Infrastructure.Postgres.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;
    public DashboardRepository(AppDbContext db) => _db = db;

    public Task<Dashboard?> GetByIdAsync(short tenantId, int id, CancellationToken ct = default) =>
        _db.Dashboards
            .Include(d => d.Widgets)
                .ThenInclude(w => w.Queries)
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, ct);

    public Task<List<Dashboard>> GetListByUserAsync(short tenantId, int userId, CancellationToken ct = default) =>
        _db.Dashboards
            .Where(d => d.TenantId == tenantId && d.UserId == userId)
            .Include(d => d.Widgets)
                .ThenInclude(w => w.Queries)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

    public async Task<Dashboard> AddAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        _db.Dashboards.Add(dashboard);
        await _db.SaveChangesAsync(ct);
        return dashboard;
    }

    public async Task UpdateAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        
        _db.Dashboards.Update(dashboard);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(short tenantId, int id, CancellationToken ct = default)
    {
        // Delete in FK-safe order: queries → widgets → prompts → dashboard
        await _db.Database.ExecuteSqlRawAsync(
            """DELETE FROM queries WHERE tenant_id = {0} AND dashboard_id = {1}""", tenantId, id); 
        await _db.Database.ExecuteSqlRawAsync(
            """DELETE FROM widgets WHERE tenant_id = {0} AND dashboard_id = {1}""", tenantId, id);
        await _db.Database.ExecuteSqlRawAsync(
            """DELETE FROM prompts WHERE tenant_id = {0} AND dashboard_id = {1}""", tenantId, id);
        await _db.Database.ExecuteSqlRawAsync(
            """DELETE FROM dashboards WHERE tenant_id = {0} AND id = {1}""", tenantId, id);
    }

    public Task<Dashboard?> GetDefaultByUserAsync(short tenantId, int userId, CancellationToken ct = default) =>
        _db.Dashboards
            .Where(d => d.TenantId == tenantId && d.UserId == userId && d.IsDefault)
            .Include(d => d.Widgets)
                .ThenInclude(w => w.Queries)
            .FirstOrDefaultAsync(ct);

    // Scoped to (tenant, user) so each user keeps their own default independently -
    // clearing the old default and setting the new one happens in one transaction
    // so there is never a moment with zero or two defaults for the same user.
    public async Task SetDefaultAsync(short tenantId, int userId, int dashboardId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlRawAsync(
            """UPDATE dashboards SET is_default = false WHERE tenant_id = {0} AND user_id = {1} AND is_default = true""",
            tenantId, userId);
        await _db.Database.ExecuteSqlRawAsync(
            """UPDATE dashboards SET is_default = true WHERE tenant_id = {0} AND id = {1}""",
            tenantId, dashboardId);

        await tx.CommitAsync(ct);
    }
}