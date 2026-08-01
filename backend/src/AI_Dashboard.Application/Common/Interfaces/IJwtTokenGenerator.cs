using AI_Dashboard.Domain.Entities;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}