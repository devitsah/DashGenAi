using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Queries;

public record GetDefaultDashboardQuery(short TenantId, int UserId) : IRequest<Dashboard?>;

public class GetDefaultDashboardQueryHandler : IRequestHandler<GetDefaultDashboardQuery, Dashboard?>
{
    private readonly IDashboardRepository _repo;
    public GetDefaultDashboardQueryHandler(IDashboardRepository repo) => _repo = repo;

    public Task<Dashboard?> Handle(GetDefaultDashboardQuery request, CancellationToken ct) =>
        _repo.GetDefaultByUserAsync(request.TenantId, request.UserId, ct);
}