using AI_Dashboard.Application.Common.Interfaces;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Commands;

public record RenameDashboardCommand(short TenantId, int DashboardId, string Name, string? Description) : IRequest;

public class RenameDashboardCommandHandler : IRequestHandler<RenameDashboardCommand>
{
    private readonly IDashboardRepository _repo;
    public RenameDashboardCommandHandler(IDashboardRepository repo) => _repo = repo;

    public async Task Handle(RenameDashboardCommand request, CancellationToken ct)
    {
        var dashboard = await _repo.GetByIdAsync(request.TenantId, request.DashboardId, ct)
            ?? throw new KeyNotFoundException($"Dashboard {request.DashboardId} not found.");

        dashboard.Name = request.Name;
        dashboard.Description = request.Description;
        await _repo.UpdateAsync(dashboard, ct);
    }
}
