using AI_Dashboard.Domain.Common;

namespace AI_Dashboard.Domain.Entities;

public class Prompt : IAuditableEntity
{
    public short TenantId { get; set; } = 1;
    public int Id { get; set; }

    public int UserId { get; set; }
    public int? DashboardId { get; set; }
    public int? ParentPromptId { get; set; }

    public string PromptText { get; set; } = default!;
    public string Status { get; set; } = "Pending";

    public int CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}