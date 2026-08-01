using AI_Dashboard.Domain.Common;

namespace AI_Dashboard.Domain.Entities;

public class AuditLog : IAuditableEntity
{
    public short TenantId { get; set; } = 1;
    public int Id { get; set; }

    public int UserId { get; set; }
    public string EntityType { get; set; } = default!;
    public int EntityId { get; set; }
    public string Action { get; set; } = default!;

    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }

    public int CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}