using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Dashboards.Commands;

public record CreateDashboardCommand(short TenantId, int UserId, string Name, string? Description, string? Style)
    : IRequest<int>;

public class CreateDashboardCommandHandler : IRequestHandler<CreateDashboardCommand, int>
{
    private readonly IDashboardRepository _repo;
    public CreateDashboardCommandHandler(IDashboardRepository repo) => _repo = repo;

    public async Task<int> Handle(CreateDashboardCommand request, CancellationToken ct)
    {
        var style = request.Style?.ToLowerInvariant() switch
        {
            "powerbi" => "PowerBi",
            "grafana" => "Grafana",
            "custom"  => "Custom",
            _         => "PowerBi"
        };
        var dashboard = await _repo.AddAsync(new Dashboard
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            Name = request.Name,
            Description = request.Description,
            DashboardStyle = style,
            CreatedBy = request.UserId
           
        }, ct);
        return dashboard.Id;
    }
}