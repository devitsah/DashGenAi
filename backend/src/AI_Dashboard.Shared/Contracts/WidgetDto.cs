namespace AI_Dashboard.Shared.Contracts;

public class WidgetDto
{
    public int Id { get; set; }
    public int DashboardId { get; set; }
    public string Title { get; set; } = default!;
    public string WidgetType { get; set; } = default!;
    public short Width { get; set; }
    public short Height { get; set; }
    public short PositionX { get; set; }
    public short PositionY { get; set; }
    public int? RefreshInterval { get; set; }
    public string DataSource { get; set; } = default!;
    public string? GeneratedSql { get; set; }
    public string? BackgroundColor { get; set; }
    public string? ConfigJson { get; set; }
}