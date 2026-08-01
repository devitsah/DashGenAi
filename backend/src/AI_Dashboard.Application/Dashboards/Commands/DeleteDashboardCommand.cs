using AI_Dashboard.Application.Common.Interfaces;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Commands;

public record DeleteDashboardCommand(short TenantId, int DashboardId) : IRequest;

public class DeleteDashboardCommandHandler : IRequestHandler<DeleteDashboardCommand>
{
    private readonly IDashboardRepository _repo;
    public DeleteDashboardCommandHandler(IDashboardRepository repo) => _repo = repo;

    public async Task Handle(DeleteDashboardCommand request, CancellationToken ct) =>
        await _repo.DeleteAsync(request.TenantId, request.DashboardId, ct);
}