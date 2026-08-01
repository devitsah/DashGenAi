using AI_Dashboard.Application.Common.Helpers;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Auth;

public record LoginCommand(string Email, string Password) : IRequest<string>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, string>
{
    private readonly IUserRepository _userRepo;
    private readonly IUserSessionRepository _sessionRepo;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public LoginCommandHandler(IUserRepository userRepo, IUserSessionRepository sessionRepo, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _userRepo = userRepo;
        _sessionRepo = sessionRepo;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<string> Handle(LoginCommand request, CancellationToken ct)
    {
        const short tenantId = 1;

        var user = await _userRepo.GetByEmailAsync(tenantId, request.Email, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

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