using AI_Dashboard.Domain.Common;


namespace AI_Dashboard.Domain.Entities;

public class Dashboard : IAuditableEntity
{
    public short TenantId { get; set; } = 1;
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Visibility { get; set; } = "Private";
public string DashboardStyle { get; set; } = "Custom";

   
    public bool IsDefault { get; set; }
   
    public short VersionNo { get; set; } = 1;

    // Dashboard-wide settings only (grid size, theme overrides, filter defaults).
    // Individual widgets live in the Widgets table, not here.
    public string DefinitionJson { get; set; } = "{}";

    public int CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Widget> Widgets { get; set; } = new List<Widget>();
    public ICollection<Prompt> Prompts { get; set; } = new List<Prompt>();
}