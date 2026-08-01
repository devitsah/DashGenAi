using AI_Dashboard.Domain.Common;

namespace AI_Dashboard.Domain.Entities;

public class User : ITimestamped
{
    public short TenantId { get; set; } = 1;
    public int Id { get; set; }

    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Dashboard> Dashboards { get; set; } = new List<Dashboard>();
}