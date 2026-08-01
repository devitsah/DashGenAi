using AI_Dashboard.Application.Common.Helpers;
using AI_Dashboard.Application.Common.Interfaces;
using MediatR;

namespace AI_Dashboard.Application.Auth;

public record LogoutCommand(short TenantId, string RawToken) : IRequest;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IUserSessionRepository _sessionRepo;
    public LogoutCommandHandler(IUserSessionRepository sessionRepo) => _sessionRepo = sessionRepo;

    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Hash(request.RawToken);
        await _sessionRepo.DeleteByTokenHashAsync(request.TenantId, tokenHash, ct);
    }
}