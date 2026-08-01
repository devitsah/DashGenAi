using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI_Dashboard.Infrastructure.Postgres.Repositories;

public class QueryRepository : IQueryRepository
{
    private readonly AppDbContext _db;
    public QueryRepository(AppDbContext db) => _db = db;

    public async Task<Query> AddAsync(Query query, CancellationToken ct = default)
    {
        _db.Queries.Add(query);
        await _db.SaveChangesAsync(ct);
        return query;
    }

    public Task<Query?> GetByWidgetIdAsync(short tenantId, int widgetId, CancellationToken ct = default) =>
        _db.Queries.FirstOrDefaultAsync(q => q.TenantId == tenantId && q.WidgetId == widgetId, ct);
}