

namespace AI_Dashboard.Application.PromptProcessing.Models;

public class GeneratedDashboardSpec
{
    public string WidgetTitle { get; set; } = default!;
    public string WidgetType { get; set; } = default!;
    public short Width { get; set; }
    public short Height { get; set; }
    public short PositionX { get; set; }
    public short PositionY { get; set; }
    public string SqlQuery { get; set; } = default!;
}