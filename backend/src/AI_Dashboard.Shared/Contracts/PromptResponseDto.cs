namespace AI_Dashboard.Shared.Contracts;

public class PromptResponseDto
{
    public bool NeedsClarification { get; set; }
    public string? ClarificationQuestion { get; set; }
    public List<string>? ClarificationOptions { get; set; }
    public int? PromptId { get; set; }
    public int? DashboardId { get; set; }
    public int? WidgetId { get; set; }
    public string? GeneratedSql { get; set; }
}