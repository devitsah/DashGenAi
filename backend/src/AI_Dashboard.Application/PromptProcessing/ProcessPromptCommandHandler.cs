using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing.Models;
using AI_Dashboard.Application.PromptProcessing.Pipeline;
using AI_Dashboard.Domain.Entities;
using MediatR;

namespace AI_Dashboard.Application.PromptProcessing;

public class ProcessPromptCommandHandler : IRequestHandler<ProcessPromptCommand, ProcessPromptResult>
{
    private readonly PromptPipelineOrchestrator _orchestrator;
    private readonly IPromptRepository _promptRepo;
    private readonly IDashboardRepository _dashboardRepo;
    private readonly IWidgetRepository _widgetRepo;
    private readonly IQueryRepository _queryRepo;

    public ProcessPromptCommandHandler(
        PromptPipelineOrchestrator orchestrator,
        IPromptRepository promptRepo,
        IDashboardRepository dashboardRepo,
        IWidgetRepository widgetRepo,
        IQueryRepository queryRepo)
    {
        _orchestrator = orchestrator;
        _promptRepo = promptRepo;
        _dashboardRepo = dashboardRepo;
        _widgetRepo = widgetRepo;
        _queryRepo = queryRepo;
    }

    public async Task<ProcessPromptResult> Handle(ProcessPromptCommand request, CancellationToken ct)
    {
        var promptText = request.PromptText;
        IntentResult? preSeeded = null;

        if (request.ParentPromptId.HasValue)
        {
            var chain = await _promptRepo.GetChainAsync(request.TenantId, request.ParentPromptId.Value, ct);
            if (chain.Count > 0)
            {
                var history = string.Join(". ", chain.Select(p => p.PromptText));
                promptText = $"{history}. {request.PromptText}";
                preSeeded = BuildPreSeededIntent(chain, request.PromptText);
            }
        }

        var prompt = await _promptRepo.AddAsync(new Prompt
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            DashboardId = request.DashboardId,
            ParentPromptId = request.ParentPromptId,
            PromptText = request.PromptText,
            Status = "Processing"
        }, ct);

        var context = new PromptPipelineContext
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            PromptId = prompt.Id,
            PromptText = promptText,
            PreSeededIntent = preSeeded
        };

        try
        {
            await _orchestrator.RunAsync(context, ct);
        }
        catch (OperationCanceledException)
        {
            prompt.Status = "Failed";
            await _promptRepo.UpdateAsync(prompt, ct);
            return new ProcessPromptResult
            {
                PromptId = prompt.Id,
                NeedsClarification = true,
                ClarificationQuestion = "The AI took too long to respond. Please try a simpler request."
            };
        }
        catch (Exception ex)
        {
            prompt.Status = "Failed";
            await _promptRepo.UpdateAsync(prompt, ct);
            throw new InvalidOperationException(
                $"Pipeline failed for prompt '{request.PromptText}': {ex.Message}", ex);
        }

        if (context.NeedsClarification)
        {
            prompt.Status = context.IsBlocked ? "Failed" : "PendingClarification";
            await _promptRepo.UpdateAsync(prompt, ct);

            return new ProcessPromptResult
            {
                PromptId = prompt.Id,
                NeedsClarification = true,
                ClarificationQuestion = context.Clarification!.Question,
                ClarificationOptions = context.Clarification.Options,
                Blocked = context.IsBlocked
            };
        }

        if (context.DashboardSpec is null || context.Intent is null)
        {
            prompt.Status = "Failed";
            await _promptRepo.UpdateAsync(prompt, ct);
            return new ProcessPromptResult
            {
                PromptId = prompt.Id,
                NeedsClarification = true,
                ClarificationQuestion =
                    "I understood your request but couldn't generate a query for it. " +
                    "Could you be more specific about what data you want to see?"
            };
        }

        if (!request.DashboardId.HasValue)
        {
            prompt.Status = "Failed";
            await _promptRepo.UpdateAsync(prompt, ct);
            return new ProcessPromptResult
            {
                PromptId = prompt.Id,
                NeedsClarification = true,
                ClarificationQuestion = "Please create a dashboard first before adding widgets."
            };
        }

        var dashboard = await _dashboardRepo.GetByIdAsync(request.TenantId, request.DashboardId.Value, ct);
        if (dashboard is null)
        {
            prompt.Status = "Failed";
            await _promptRepo.UpdateAsync(prompt, ct);
            return new ProcessPromptResult
            {
                PromptId = prompt.Id,
                NeedsClarification = true,
                ClarificationQuestion = "Dashboard not found. Please go back and create a dashboard first."
            };
        }

        var spec = context.DashboardSpec;
        Widget widget;
        try
        {
            widget = await _widgetRepo.AddAsync(new Widget
{
    TenantId = request.TenantId,
    DashboardId = dashboard.Id,
    Title = $"Widget {dashboard.Widgets.Count + 1}",
    WidgetType = spec.WidgetType,
    Width = spec.Width,
    Height = spec.Height,
    PositionX = spec.PositionX,
    PositionY = spec.PositionY,
    CreatedBy = request.UserId,
    UpdatedBy = request.UserId
}, ct);

            var finalSql = spec.SqlQuery.Replace(":tenantId", request.TenantId.ToString(),
                StringComparison.OrdinalIgnoreCase);

            await _queryRepo.AddAsync(new Query
            {
                TenantId = request.TenantId,
                DashboardId = dashboard.Id,
                WidgetId = widget.Id,
                PromptId = prompt.Id,
                SqlQuery = finalSql,
                CreatedBy = request.UserId,
                UpdatedBy = request.UserId
            }, ct);

            var existingWidgets = new List<object>();
            short nextY = 0;
            try
            {
                using var existing = System.Text.Json.JsonDocument.Parse(dashboard.DefinitionJson);
                if (existing.RootElement.TryGetProperty("widgets", out var arr))
                {
                    existingWidgets = arr.EnumerateArray()
                        .Select(w => (object)new {
                            id   = w.GetProperty("id").GetInt32(),
                            type = w.GetProperty("type").GetString(),
                            x    = w.GetProperty("x").GetInt16(),
                            y    = w.GetProperty("y").GetInt16(),
                            w    = w.GetProperty("w").GetInt16(),
                            h    = w.GetProperty("h").GetInt16()
                        }).ToList();
                    nextY = arr.EnumerateArray()
                        .Select(w => (short)(w.GetProperty("y").GetInt16() + w.GetProperty("h").GetInt16()))
                        .DefaultIfEmpty((short)0)
                        .Max();
                }
            }
            catch { }

            existingWidgets.Add(new { id = widget.Id, type = spec.WidgetType, x = (short)0, y = nextY, w = spec.Width, h = spec.Height });

            dashboard.DefinitionJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                style    = context.Intent!.DashboardStyle,
                gridCols = 12,
                widgets  = existingWidgets
            });
            await _dashboardRepo.UpdateAsync(dashboard, ct);

            prompt.Status = "Completed";
            prompt.DashboardId = dashboard.Id;
            await _promptRepo.UpdateAsync(prompt, ct);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.InnerException?.Message
                     ?? ex.InnerException?.Message
                     ?? ex.Message;
            throw new InvalidOperationException($"Save failed: {inner}", ex);
        }

        return new ProcessPromptResult
        {
            PromptId = prompt.Id,
            NeedsClarification = false,
            DashboardId = dashboard.Id,
            WidgetId = widget.Id,
            GeneratedSql = spec.SqlQuery.Replace(":tenantId", request.TenantId.ToString(), StringComparison.OrdinalIgnoreCase),
            WidgetData = context.QueryResultRows,
            WidgetDataError = context.ExecutionError,
            IntentSummary = new IntentSummary
            {
                DashboardStyle = ExtractStyleFromChain(context.PromptText) ?? context.Intent!.DashboardStyle,
                WidgetType     = spec.WidgetType,
                Metric         = context.Intent!.Metric ?? DerivMetricLabel(spec.SqlQuery),
                Dimension      = ResolveDisplayDimension(context),
                Filters        = context.Intent.Filters,
                TimePeriod     = context.Intent.TimePeriod
            },
            Suggestions = context.Suggestions ?? []
        };
    }

    private static string? ResolveDisplayDimension(PromptPipelineContext context)
    {
        var dimension = context.Intent?.Dimension;
        var map = context.SchemaMap;

        if (dimension is null || map is null)
            return dimension;

        return !string.IsNullOrWhiteSpace(map.DimensionColumn)
            ? map.DimensionColumn
            : dimension;
    }

    private static string TruncateTitle(string text) =>
        text.Length <= 60 ? text : text[..57] + "...";

    private static string ParseStyle(string style) => style switch
    {
        "power_bi" => "PowerBi",
        "grafana"  => "Grafana",
        _          => "Custom"
    };

    private static string? ExtractStyleFromChain(string promptText)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            promptText,
            @"^Use\s+(\S+)\s+dashboard style",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
    }

    private static string? DerivMetricLabel(string sql)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            sql,
            @"\b(COUNT|SUM|AVG|MIN|MAX)\s*\(([^)]+)\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var fn  = m.Groups[1].Value.ToUpperInvariant();
        var col = m.Groups[2].Value.Trim().TrimStart('*');
        col = System.Text.RegularExpressions.Regex.Replace(col, @"\w+\.", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(col) || col == "*" ? fn : $"{fn}({col})";
    }

    private static IntentResult BuildPreSeededIntent(List<Domain.Entities.Prompt> chain, string currentAnswer)
    {
        var seed = new IntentResult();

        var rootPrompt = chain.FirstOrDefault(p => !p.ParentPromptId.HasValue);
        if (rootPrompt is not null)
        {
            var impliedViz = VisualizationTypeMapper.FindInText(rootPrompt.PromptText);
            if (impliedViz is not null) seed.VisualizationType = impliedViz;
        }

        var answers = chain.Where(p => p.ParentPromptId.HasValue).Select(p => p.PromptText).ToList();
        answers.Add(currentAnswer);

        var awaitingTableChoice = false;

        var styleLabelToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Grafana"] = "grafana",
            ["Power BI"] = "power_bi",
            ["Custom"] = "custom"
        };

        foreach (var answer in answers)
        {
            var trimmed = answer.Trim();

            if (trimmed.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            {
                seed.TableColumnConfirmed = true;
                seed.TableColumnRejected  = false;
                awaitingTableChoice = false;
                continue;
            }
            if (trimmed.Equals("No", StringComparison.OrdinalIgnoreCase))
            {
                seed.TableColumnConfirmed = false;
                seed.TableColumnRejected  = true;
                awaitingTableChoice = true;
                continue;
            }
            if (awaitingTableChoice)
            {
                seed.TableColumnConfirmed = true;
                seed.TableColumnRejected  = false;
                awaitingTableChoice = false;
                continue;
            }

            if (styleLabelToValue.TryGetValue(trimmed, out var styleValue))
            {
                seed.DashboardStyle = styleValue;
                seed.DashboardStyleConfirmed = true;
                continue;
            }

            if (trimmed.Equals("Continue anyway", StringComparison.OrdinalIgnoreCase))
            {
                seed.VisualizationCompatibilityConfirmed = true;
                continue;
            }
            if (trimmed.Equals("Pick a different chart", StringComparison.OrdinalIgnoreCase))
            {
                seed.VisualizationTypeRejected = true;
                seed.VisualizationCompatibilityConfirmed = false;
                continue;
            }

            var vizValue = VisualizationTypeMapper.ToIntentValue(trimmed);
            if (vizValue is not null)
            {
                seed.VisualizationType = vizValue;
                seed.VisualizationTypeRejected = false;
                continue;
            }
        }

        return seed;
    }
}