using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Queries;

public record GetDashboardListQuery(short TenantId, int UserId) : IRequest<List<Dashboard>>;

public class GetDashboardListQueryHandler : IRequestHandler<GetDashboardListQuery, List<Dashboard>>
{
    private readonly IDashboardRepository _repo;
    public GetDashboardListQueryHandler(IDashboardRepository repo) => _repo = repo;

    public Task<List<Dashboard>> Handle(GetDashboardListQuery request, CancellationToken ct) =>
        _repo.GetListByUserAsync(request.TenantId, request.UserId, ct);
}