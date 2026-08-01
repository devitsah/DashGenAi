using System.Text.Json;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.Common.Schema;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.Widgets.Commands;

// Manual "Add Widget" from the Designer (as opposed to the AI Builder prompt
// pipeline). Builds a simple aggregate/table SQL query from schema-validated
// table/column names picked in the dialog's dropdowns. HTML widgets have no
// query at all - the markup lives in ConfigJson.
public record CreateWidgetCommand(
    short TenantId,
    int UserId,
    int DashboardId,
    string Title,
    string WidgetType,
    string? Table,
    string? GroupByColumn,
    string? MetricColumn,
    string? Aggregation,
    string? TimeColumn,
    string? Interval,
    string? HtmlContent,
    Dictionary<string, object?>? DisplayOptions
) : IRequest<int>;

public class CreateWidgetCommandHandler : IRequestHandler<CreateWidgetCommand, int>
{
    private static readonly HashSet<string> ValidAggregations =
        new(StringComparer.OrdinalIgnoreCase) { "COUNT", "SUM", "AVG", "MIN", "MAX" };

    private readonly IDashboardRepository _dashboardRepo;
    private readonly IWidgetRepository _widgetRepo;
    private readonly IQueryRepository _queryRepo;
    private readonly IPromptRepository _promptRepo;
    private readonly ISchemaIntrospectionService _schema;
    private readonly ISchemaIntrospectionOptions _schemaOptions;

    public CreateWidgetCommandHandler(
        IDashboardRepository dashboardRepo,
        IWidgetRepository widgetRepo,
        IQueryRepository queryRepo,
        IPromptRepository promptRepo,
        ISchemaIntrospectionService schema,
        ISchemaIntrospectionOptions schemaOptions)
    {
        _dashboardRepo = dashboardRepo;
        _widgetRepo = widgetRepo;
        _queryRepo = queryRepo;
        _promptRepo = promptRepo;
        _schema = schema;
        _schemaOptions = schemaOptions;
    }

    public async Task<int> Handle(CreateWidgetCommand request, CancellationToken ct)
    {
        var dashboard = await _dashboardRepo.GetByIdAsync(request.TenantId, request.DashboardId, ct)
            ?? throw new KeyNotFoundException($"Dashboard {request.DashboardId} not found.");

        if (dashboard.UserId != request.UserId)
            throw new UnauthorizedAccessException("You do not have access to this dashboard.");

        var widgetType = Normalize(request.WidgetType);
        var config = new Dictionary<string, object?>();
        string? sql = null;

        if (widgetType == "HtmlWidget")
        {
            config["html"] = request.HtmlContent ?? "";
        }
        else
        {
            var tables = await _schema.GetTablesAsync(request.TenantId, ct);
            var table = tables.FirstOrDefault(t =>
                    string.Equals(t.TableName, request.Table, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Unknown or inaccessible data source '{request.Table}'.");

            sql = BuildSql(widgetType, table, request, request.TenantId, _schemaOptions.TenantColumn);

            config["source"] = new Dictionary<string, object?>
            {
                ["table"] = table.TableName,
                ["groupBy"] = request.GroupByColumn,
                ["metric"] = request.MetricColumn,
                ["aggregation"] = request.Aggregation,
                ["timeColumn"] = request.TimeColumn,
                ["interval"] = request.Interval
            };
        }

        if (request.DisplayOptions is not null)
            config["displayOptions"] = request.DisplayOptions;

        var siblings = await _widgetRepo.GetByDashboardIdAsync(request.TenantId, dashboard.Id, ct);
        var (width, height) = ResolveSize(widgetType);
        short nextY = siblings.Count == 0
            ? (short)0
            : siblings.Select(w => (short)(w.PositionY + w.Height)).Max();

        var widget = await _widgetRepo.AddAsync(new Widget
        {
            TenantId = request.TenantId,
            DashboardId = dashboard.Id,
            Title = request.Title,
            WidgetType = widgetType,
            Width = width,
            Height = height,
            PositionX = 0,
            PositionY = nextY,
            DataSource = "postgresql",
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedBy = request.UserId,
            UpdatedBy = request.UserId
        }, ct);

        if (sql is not null)
        {
            var prompt = await _promptRepo.AddAsync(new Prompt
            {
                TenantId = request.TenantId,
                UserId = request.UserId,
                DashboardId = dashboard.Id,
                PromptText = $"(manually added widget) {request.Title}",
                Status = "Completed",
                CreatedBy = request.UserId,
                UpdatedBy = request.UserId
            }, ct);

            await _queryRepo.AddAsync(new Query
            {
                TenantId = request.TenantId,
                DashboardId = dashboard.Id,
                WidgetId = widget.Id,
                PromptId = prompt.Id,
                SqlQuery = sql,
                CreatedBy = request.UserId,
                UpdatedBy = request.UserId
            }, ct);
        }

        await SyncLayoutAsync(dashboard, widget.Id, widgetType, width, height, nextY, ct);

        return widget.Id;
    }

    // Keeps DefinitionJson's grid layout in sync, mirroring what the AI prompt
    // pipeline and WidgetsController.Delete already do for this same field.
    private async Task SyncLayoutAsync(
        Dashboard dashboard, int widgetId, string widgetType, short width, short height, short y, CancellationToken ct)
    {
        var existingWidgets = new List<object>();
        try
        {
            using var existing = JsonDocument.Parse(dashboard.DefinitionJson);
            if (existing.RootElement.TryGetProperty("widgets", out var arr))
            {
                existingWidgets = arr.EnumerateArray()
                    .Select(w => (object)new
                    {
                        id = w.GetProperty("id").GetInt32(),
                        type = w.GetProperty("type").GetString(),
                        x = w.GetProperty("x").GetInt16(),
                        y = w.GetProperty("y").GetInt16(),
                        w = w.GetProperty("w").GetInt16(),
                        h = w.GetProperty("h").GetInt16()
                    }).ToList();
            }
        }
        catch
        {
            // Malformed/legacy layout JSON - start fresh rather than fail the create.
        }

        existingWidgets.Add(new { id = widgetId, type = widgetType, x = (short)0, y, w = width, h = height });

        dashboard.DefinitionJson = JsonSerializer.Serialize(new
        {
            style = dashboard.DashboardStyle,
            gridCols = 12,
            widgets = existingWidgets
        });
        await _dashboardRepo.UpdateAsync(dashboard, ct);
    }

    public static string Normalize(string type) => type?.Replace("_", "").Replace(" ", "").ToLowerInvariant() switch
    {
        "kpicard" or "kpi" => "KpiCard",
        "piechart" or "pie" => "PieChart",
        "barchart" or "bar" => "BarChart",
        "linechart" or "line" => "LineChart",
        "areachart" or "area" => "AreaChart",
        "table" => "Table",
        "gauge" => "Gauge",
        "timeseries" => "TimeSeries",
        "htmlwidget" or "html" => "HtmlWidget",
        _ => throw new InvalidOperationException($"Unsupported widget type '{type}'.")
    };

    private static (short w, short h) ResolveSize(string type) => type switch
    {
        "KpiCard" => (3, 2),
        "Gauge" => (3, 3),
        "Table" => (6, 4),
        "BarChart" => (6, 4),
        "AreaChart" => (6, 4),
        "PieChart" => (4, 4),
        "LineChart" => (8, 4),
        "TimeSeries" => (8, 4),
        "HtmlWidget" => (6, 3),
        _ => (6, 4)
    };

    private static string BuildSql(
        string widgetType, TableMetadata table, CreateWidgetCommand request, short tenantId, string? tenantColumn)
    {
        var t = Quote(table.TableName);
        var where = BuildTenantWhere(table, tenantColumn, tenantId);

        if (widgetType is "KpiCard" or "Gauge")
        {
            var agg = ResolveAggregation(request.Aggregation);
            var col = ResolveMetricExpression(table, request.MetricColumn, agg);
            return $"SELECT {agg}({col}) AS value FROM {t}{where}";
        }

        if (widgetType == "Table")
        {
            return $"SELECT * FROM {t}{where} LIMIT 500";
        }

        // Bar / Pie / Line / Area / TimeSeries: one dimension + one aggregated metric.
        var dimensionSource = widgetType == "TimeSeries" ? request.TimeColumn : request.GroupByColumn;
        var dimension = ResolveColumn(table, dimensionSource)
            ?? throw new InvalidOperationException("A Group By / Time column is required for this chart type.");
        var aggFn = ResolveAggregation(request.Aggregation);
        var metricExpr = ResolveMetricExpression(table, request.MetricColumn, aggFn);
        var dimQ = Quote(dimension);

        return $"SELECT {dimQ}, {aggFn}({metricExpr}) AS value FROM {t}{where} " +
               $"GROUP BY {dimQ} ORDER BY {dimQ} LIMIT 500";
    }

    private static string BuildTenantWhere(TableMetadata table, string? tenantColumn, short tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantColumn)) return "";
        var hasColumn = table.Columns.Any(c => string.Equals(c.ColumnName, tenantColumn, StringComparison.OrdinalIgnoreCase));
        return hasColumn ? $" WHERE {Quote(tenantColumn)} = {tenantId}" : "";
    }

    private static string ResolveAggregation(string? aggregation)
    {
        var agg = (aggregation ?? "COUNT").ToUpperInvariant();
        return ValidAggregations.Contains(agg) ? agg : "COUNT";
    }

    private static string ResolveMetricExpression(TableMetadata table, string? metric, string aggregation)
    {
        if (aggregation == "COUNT" && string.IsNullOrWhiteSpace(metric)) return "*";
        var col = ResolveColumn(table, metric)
            ?? throw new InvalidOperationException("A metric column is required for this chart type.");
        return Quote(col);
    }

    // Whitelists against the actual introspected schema - table/column names are
    // never interpolated unless they match a real column, so this can't be used
    // for SQL injection even though it builds the query string by hand.
    private static string? ResolveColumn(TableMetadata table, string? column)
    {
        if (string.IsNullOrWhiteSpace(column)) return null;
        return table.Columns
            .FirstOrDefault(c => string.Equals(c.ColumnName, column, StringComparison.OrdinalIgnoreCase))
            ?.ColumnName;
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "")}\"";
}