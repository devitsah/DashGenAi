using AI_Dashboard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI_Dashboard.Api.Controllers;

[ApiController]
[Route("api/schema")]
[Authorize]
public class SchemaController : ControllerBase
{
    private readonly ISchemaIntrospectionService _schema;
    private readonly ICurrentUserService _currentUser;

    public SchemaController(ISchemaIntrospectionService schema, ICurrentUserService currentUser)
    {
        _schema = schema;
        _currentUser = currentUser;
    }

    /// Returns example prompts derived from the actual DB schema — no hardcoded domain terms.
    [HttpGet("examples")]
    public async Task<IActionResult> GetExamples(CancellationToken ct)
    {
        var tables = await _schema.GetTablesAsync(_currentUser.TenantId, ct);
        var examples = new List<string>();

        foreach (var t in tables.Take(4))
        {
            var name = t.TableName.Replace('_', ' ');
            examples.Add($"Show total {name} count");

            var textCol = t.Columns.FirstOrDefault(c =>
                c.DataType is "character varying" or "varchar" or "text" &&
                !c.ColumnName.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) &&
                !c.ColumnName.Equals("id", StringComparison.OrdinalIgnoreCase));

            if (textCol is not null)
                examples.Add($"Show {name} by {textCol.ColumnName.Replace('_', ' ')} as a bar chart");

            var numCol = t.Columns.FirstOrDefault(c =>
                c.DataType is "integer" or "bigint" or "numeric" or "decimal" or "real" or "double precision" &&
                !c.ColumnName.Equals("id", StringComparison.OrdinalIgnoreCase) &&
                !c.ColumnName.Equals("tenant_id", StringComparison.OrdinalIgnoreCase));

            if (numCol is not null)
                examples.Add($"Show average {numCol.ColumnName.Replace('_', ' ')} per {name}");
        }

        return Ok(examples.Distinct().Take(5).ToList());
    }

    // Powers the Add Widget dialog's Data Source / Group By / Metric dropdowns -
    // only whitelisted, non-sensitive tables & columns the AI pipeline itself is
    // allowed to see, so a widget built by hand can't reach anything more than a
    // prompt-generated one could.
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables(CancellationToken ct)
    {
        var tables = await _schema.GetTablesAsync(_currentUser.TenantId, ct);
        return Ok(tables.Select(t => new
        {
            table = t.TableName,
            columns = t.Columns.Select(c => new
            {
                name = c.ColumnName,
                dataType = c.DataType,
                numeric = IsNumeric(c.DataType)
            })
        }));
    }

    private static bool IsNumeric(string dataType) => dataType.ToLowerInvariant() switch
    {
        "integer" or "bigint" or "smallint" or "numeric" or "decimal" or "real" or "double precision" => true,
        _ => false
    };
}