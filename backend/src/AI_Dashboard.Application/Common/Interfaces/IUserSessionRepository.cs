using AI_Dashboard.Domain.Entities;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface IUserSessionRepository
{
    Task<UserSession> AddAsync(UserSession session, CancellationToken ct = default);
    Task<UserSession?> GetByTokenHashAsync(short tenantId, string tokenHash, CancellationToken ct = default);
    Task DeleteByTokenHashAsync(short tenantId, string tokenHash, CancellationToken ct = default);
}