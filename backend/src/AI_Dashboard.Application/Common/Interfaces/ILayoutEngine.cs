using AI_Dashboard.Application.PromptProcessing.Models;


namespace AI_Dashboard.Application.Common.Interfaces;

public interface ILayoutEngine
{
    GeneratedDashboardSpec BuildLayout(
    IntentResult intent,
    ResolvedSchemaMap schemaMap,
    string style,                 // was DashboardStyle style
    object? widgetHints    = null,
    string? preGeneratedSql = null);
}
