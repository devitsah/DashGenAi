

namespace AI_Dashboard.Application.PromptProcessing.Models;

/// Single source of truth for visualization label <-> WidgetType <-> intent string.
/// Add a new entry here when a new WidgetType is added to the enum - nowhere else.
public static class VisualizationTypeMapper
{
   private static readonly List<(string Label, string IntentValue, string WidgetType, string[] Keywords)> Entries =
[
    ("KPI Card",   "kpi_card",   "KpiCard",   ["card", "kpi", "kpi card"]),
    ("Table",      "table",      "Table",     ["table"]),
    ("Bar Chart",  "bar_chart",  "BarChart",  ["bar", "bar chart"]),
    ("Pie Chart",  "pie_chart",  "PieChart",  ["pie", "pie chart"]),
    ("Line Chart", "line_chart", "LineChart", ["line", "line chart", "trend"]),
];

    // Labels shown to the user in clarification options e.g. ["KPI Card", "Bar Chart", ...]
    public static List<string> AllLabels() =>
        Entries.Select(e => e.Label).ToList();

    // Labels that actually make sense given the resolved shape of the request:
    // - Plain listing/browse request (isAggregation = false, e.g. "show name of all
    //   agents"): nothing is being counted/summed/averaged, so a chart or a single
    //   KPI number would misrepresent a list of individual rows. Only Table fits.
    // - Aggregation with a grouping dimension (e.g. "tickets by priority"): anything
    //   except KPI Card, since KPI Card shows one number, not several groups.
    // - Aggregation without a dimension (e.g. "total tickets"): only KPI Card or
    //   Table, since there's nothing to plot per-category.
    // Purely rule-based on these two booleans — no table/column names involved, so
    // it works the same for any schema.
    public static List<string> LabelsFor(bool hasDimension, bool isAggregation)
    {
        if (!isAggregation)
            return Entries.Where(e => e.IntentValue == "table").Select(e => e.Label).ToList();

        return hasDimension
            ? Entries.Where(e => e.IntentValue != "kpi_card").Select(e => e.Label).ToList()
            : Entries.Where(e => e.IntentValue is "kpi_card" or "table").Select(e => e.Label).ToList();
    }

    // Converts a user-facing label, keyword, or intent string to the intent value
    // e.g. "Bar Chart" -> "bar_chart", "bar" -> "bar_chart", "bar_chart" -> "bar_chart"
    public static string? ToIntentValue(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();
        return Entries.FirstOrDefault(e =>
            e.Label.ToLowerInvariant() == normalized ||
            e.IntentValue == normalized ||
            e.Keywords.Contains(normalized))
            .IntentValue;
    }

    // Converts an intent string to WidgetType enum
    // before: public static WidgetType? ToWidgetType(string? intentValue)
public static string? ToWidgetType(string? intentValue) =>
    intentValue is null ? null :
    Entries.FirstOrDefault(e => e.IntentValue == intentValue) is var entry && entry.IntentValue is not null
        ? entry.WidgetType : null;

    // Scans free-form text (e.g. the user's original prompt sentence) for any
    // visualization keyword. Contains-based, unlike ToIntentValue which requires an
    // exact match — used as a deterministic backup when re-extracting intent from a
    // growing combined prompt causes the model to drop a detail it already knew.
    public static string? FindInText(string text)
    {
        var normalized = text.ToLowerInvariant();
        foreach (var entry in Entries)
            foreach (var keyword in entry.Keywords)
                if (normalized.Contains(keyword))
                    return entry.IntentValue;
        return null;
    }

    // Generates AI system prompt rules from the entries - no hardcoded strings in prompt steps.
    public static string ToSystemPromptRules() =>
        string.Concat(Entries.Select(e =>
            $"- '{string.Join("' or '", e.Keywords)}' means visualization_type = '{e.IntentValue}'\n"));
}