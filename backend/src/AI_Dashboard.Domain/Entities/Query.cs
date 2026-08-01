using AI_Dashboard.Domain.Common;

namespace AI_Dashboard.Domain.Entities;

public class Query : IAuditableEntity
{
    public short TenantId { get; set; } = 1;
    public int Id { get; set; }

    public int DashboardId { get; set; }
    public int WidgetId { get; set; }
    public int PromptId { get; set; }

    public string SqlQuery { get; set; } = default!;

    public int CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}