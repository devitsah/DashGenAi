namespace AI_Dashboard.Application.PromptProcessing.Models;

public class IntentResult
{
    public string? Metric { get; set; }
    public string? Dimension { get; set; }
    public List<string> Filters { get; set; } = new();
    public string? VisualizationType { get; set; }
    public string DashboardStyle { get; set; } = "custom";
    public string DataSource { get; set; } = "postgresql";
    public string? TimePeriod { get; set; }
    public bool IsAggregation { get; set; } = true;
    public bool DashboardStyleAmbiguous { get; set; }
    public bool GroupingAmbiguous { get; set; }
    public bool TimePeriodMissing { get; set; }
    public bool TableColumnConfirmed { get; set; }
    public bool TableColumnRejected { get; set; }
    public bool DashboardStyleConfirmed { get; set; }
    public bool VisualizationCompatibilityConfirmed { get; set; }
    public bool VisualizationTypeRejected { get; set; }

    public bool IsComplete => VisualizationType is not null
        && !DashboardStyleAmbiguous
        && !GroupingAmbiguous
        && !TimePeriodMissing;

    public string? MissingField =>
        VisualizationType is null ? "visualization_type" :
        DashboardStyleAmbiguous   ? "dashboard_style"     :
        GroupingAmbiguous         ? "grouping"             :
        TimePeriodMissing         ? "time_period"          :
        null;
}