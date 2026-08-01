using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI_Dashboard.Infrastructure.Postgres.Repositories;

public class UserSessionRepository : IUserSessionRepository
{
    private readonly AppDbContext _db;
    public UserSessionRepository(AppDbContext db) => _db = db;

    public async Task<UserSession> AddAsync(UserSession session, CancellationToken ct = default)
    {
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public Task<UserSession?> GetByTokenHashAsync(short tenantId, string tokenHash, CancellationToken ct = default) =>
        _db.UserSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.TokenHash == tokenHash, ct);

    public async Task DeleteByTokenHashAsync(short tenantId, string tokenHash, CancellationToken ct = default)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.TokenHash == tokenHash, ct);
        if (session is null) return;
        _db.UserSessions.Remove(session);
        await _db.SaveChangesAsync(ct);
    }
}