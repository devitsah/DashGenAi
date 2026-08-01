using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing.Models;
using AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;

namespace AI_Dashboard.Infrastructure.LayoutEngine;

public class GridLayoutEngine : ILayoutEngine
{
    private readonly ISchemaIntrospectionOptions _options;

    public GridLayoutEngine(ISchemaIntrospectionOptions options) => _options = options;

    public GeneratedDashboardSpec BuildLayout(
        IntentResult intent,
        ResolvedSchemaMap schemaMap,
        string style,
        object? widgetHints     = null,
        string? preGeneratedSql = null)
    {
        var hints = widgetHints as LayoutGenerationStep.WidgetHints;

        var widgetType = VisualizationTypeMapper.ToWidgetType(intent.VisualizationType)
            ?? (hints?.WidgetType is not null ? ParseWidgetType(hints.WidgetType) : "Table");

        var (width, height) = ResolveSize(widgetType, style, hints);

        var title = hints?.WidgetTitle
            ?? (schemaMap.DimensionColumn is not null
                ? $"by {schemaMap.DimensionColumn}"
                : schemaMap.Table);

        var sql = preGeneratedSql ?? BuildFallbackSql(widgetType, schemaMap, _options.TenantColumn);

        return new GeneratedDashboardSpec
        {
            WidgetTitle = title,
            WidgetType  = widgetType,
            Width       = (short)width,
            Height      = (short)height,
            PositionX   = 0,
            PositionY   = 0,
            SqlQuery    = sql
        };
    }

    private static (int w, int h) ResolveSize(string type, string style, LayoutGenerationStep.WidgetHints? hints)
    {
        if (hints is { Width: > 0, Height: > 0 })
            return (Math.Clamp(hints.Width, 2, 12), Math.Clamp(hints.Height, 1, 6));

        return type switch
        {
            "KpiCard"   => (3, 2),
            "Table"     => style == "PowerBi" ? (12, 4) : (6, 4),
            "BarChart"  => (6, 4),
            "PieChart"  => (4, 4),
            "LineChart" => (8, 4),
            _           => (6, 4)
        };
    }

    private static string BuildFallbackSql(string type, ResolvedSchemaMap schemaMap, string? tenantColumn)
    {
        var where = tenantColumn is not null ? $" WHERE {schemaMap.Table}.{tenantColumn} = :tenantId" : "";
        var groupBy = schemaMap.DimensionColumn is not null
            ? $", {schemaMap.DimensionColumn} GROUP BY {schemaMap.DimensionColumn} ORDER BY {schemaMap.DimensionColumn}"
            : "";

        return type == "KpiCard"
            ? $"SELECT COUNT(*) AS total FROM {schemaMap.Table}{where}"
            : $"SELECT *{groupBy} FROM {schemaMap.Table}{where} LIMIT 500";
    }

    private static string ParseWidgetType(string s) => s.ToLowerInvariant() switch
    {
        "kpicard"  or "kpi_card"   => "KpiCard",
        "table"                    => "Table",
        "barchart" or "bar_chart"  => "BarChart",
        "piechart" or "pie_chart"  => "PieChart",
        "linechart"or "line_chart" => "LineChart",
        _                          => "Table"
    };
}
