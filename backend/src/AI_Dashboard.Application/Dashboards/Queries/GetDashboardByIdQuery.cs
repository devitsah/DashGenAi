using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Queries;

public record GetDashboardByIdQuery(short TenantId, int Id) : IRequest<Dashboard?>;

public class GetDashboardByIdQueryHandler : IRequestHandler<GetDashboardByIdQuery, Dashboard?>
{
    private readonly IDashboardRepository _repo;
    public GetDashboardByIdQueryHandler(IDashboardRepository repo) => _repo = repo;

    public Task<Dashboard?> Handle(GetDashboardByIdQuery request, CancellationToken ct) =>
        _repo.GetByIdAsync(request.TenantId, request.Id, ct);
}