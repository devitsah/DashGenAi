using MediatR;

namespace AI_Dashboard.Application.PromptProcessing;

public record ProcessPromptCommand(short TenantId, int UserId, int? DashboardId, string PromptText, int? ParentPromptId)
    : IRequest<ProcessPromptResult>;

public class ProcessPromptResult
{
    public int PromptId { get; set; }
    public bool NeedsClarification { get; set; }
    public string? ClarificationQuestion { get; set; }
    public List<string>? ClarificationOptions { get; set; }

    // True when NeedsClarification is only true because PromptGuardStep rejected the
    // prompt (injection / destructive SQL attempt). The frontend must NOT treat this
    // as an answerable clarification — i.e. must not chain the next message to it via
    // ParentPromptId — or every following message gets dragged back through the guard
    // together with the original rejected text and keeps failing forever.
    public bool Blocked { get; set; }
    public int? DashboardId { get; set; }
    public int? WidgetId { get; set; }
    public string? GeneratedSql { get; set; }

    // The rows VisualizationRecommendationStep already executed against Postgres
    // to pick a chart type — previously computed and discarded. Carrying them here
    // lets the AI Builder chat render the widget immediately, without a second
    // round-trip through /api/widgets/{id}/data or a click into the dashboard.
    public List<Dictionary<string, object?>>? WidgetData { get; set; }

    // Set when GeneratedSql is present but execution failed at runtime (passed EXPLAIN
    // validation, still errored against real data — e.g. bad cast, division by zero,
    // timeout). WidgetData will be null in that case. The frontend should show this
    // message instead of silently rendering an empty visualization matrix.
    public string? WidgetDataError { get; set; }

    // What the AI understood — shown to user as confirmation before dashboard appears
    public IntentSummary? IntentSummary { get; set; }

    // AI-generated suggestions for widgets to add next
    public List<string>? Suggestions { get; set; }
    public string? WidgetTitle { get; set; }
}

public class IntentSummary
{
    public string? DashboardStyle { get; set; }
    public string? WidgetType { get; set; }
    public string? Metric { get; set; }
    public string? Dimension { get; set; }
    public List<string> Filters { get; set; } = new();
    public string? TimePeriod { get; set; }
}