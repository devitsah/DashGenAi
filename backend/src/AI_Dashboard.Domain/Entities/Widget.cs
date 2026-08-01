using AI_Dashboard.Domain.Common;


namespace AI_Dashboard.Domain.Entities;

public class Widget : IAuditableEntity
{
    public short TenantId { get; set; } = 1;
    public int Id { get; set; }

    public int DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = default!;

    public string Title { get; set; } = default!;
    // before: public WidgetType WidgetType { get; set; }
public string WidgetType { get; set; } = default!;
   

    public short Width { get; set; }
    public short Height { get; set; }
    public short PositionX { get; set; }
    public short PositionY { get; set; }

    public int? RefreshInterval { get; set; }
    public string DataSource { get; set; } = "postgresql";

    public string ConfigJson { get; set; } = "{}";

    public int CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Query> Queries { get; set; } = new List<Query>();
}