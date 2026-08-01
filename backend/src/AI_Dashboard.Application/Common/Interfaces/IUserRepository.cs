using AI_Dashboard.Domain.Entities;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(short tenantId, string email, CancellationToken ct = default);
    Task<User> AddAsync(User user, CancellationToken ct = default);
}