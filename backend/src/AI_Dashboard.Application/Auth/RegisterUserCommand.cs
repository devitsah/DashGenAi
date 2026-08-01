using AI_Dashboard.Application.Common.Helpers;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Auth;

public record RegisterUserCommand(string Name, string Email, string Password) : IRequest<string>;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, string>
{
    private readonly IUserRepository _userRepo;
    private readonly IUserSessionRepository _sessionRepo;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public RegisterUserCommandHandler(IUserRepository userRepo, IUserSessionRepository sessionRepo, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _userRepo = userRepo;
        _sessionRepo = sessionRepo;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<string> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        const short tenantId = 1;

        var existing = await _userRepo.GetByEmailAsync(tenantId, request.Email, ct);
        if (existing is not null)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = await _userRepo.AddAsync(new User
        {
            TenantId = tenantId,
            Name = request.Name,
            Email = request.Email,
            PasswordHash = _hasher.Hash(request.Password)
        }, ct);

        var token = _jwt.GenerateToken(user);

        await _sessionRepo.AddAsync(new UserSession
        {
            TenantId = tenantId,
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(token),
            ExpiresAt = DateTime.UtcNow.AddMinutes(120)
        }, ct);

        return token;
    }
}