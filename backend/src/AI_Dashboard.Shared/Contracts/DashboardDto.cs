namespace AI_Dashboard.Shared.Contracts;

public class DashboardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Visibility { get; set; } = default!;
    public bool IsDefault { get; set; }
    public string DashboardStyle { get; set; } = default!;
    public string? BackgroundColor { get; set; }
    public short VersionNo { get; set; }
    public List<WidgetDto> Widgets { get; set; } = new();
}