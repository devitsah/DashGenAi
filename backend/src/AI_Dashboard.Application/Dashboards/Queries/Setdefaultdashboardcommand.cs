using AI_Dashboard.Application.Common.Interfaces;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Commands;

// Ownership of DashboardId is validated by the caller (controller) before this
// runs - by the time we get here it's just "make this one the default and
// un-default whatever this user's previous default was".
public record SetDefaultDashboardCommand(short TenantId, int UserId, int DashboardId) : IRequest;

public class SetDefaultDashboardCommandHandler : IRequestHandler<SetDefaultDashboardCommand>
{
    private readonly IDashboardRepository _repo;
    public SetDefaultDashboardCommandHandler(IDashboardRepository repo) => _repo = repo;

    public async Task Handle(SetDefaultDashboardCommand request, CancellationToken ct) =>
        await _repo.SetDefaultAsync(request.TenantId, request.UserId, request.DashboardId, ct);
}