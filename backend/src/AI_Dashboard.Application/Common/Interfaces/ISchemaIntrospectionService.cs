using AI_Dashboard.Application.Common.Schema;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface ISchemaIntrospectionService
{
    Task<string>             GetSchemaDescriptionAsync(short tenantId, CancellationToken ct = default); //SCHEMA OF ALL TABLES IN TEXT
    Task<List<TableMetadata>> GetTablesAsync(short tenantId,           CancellationToken ct = default);
    Task<List<string>>       GetDistinctValuesAsync(short tenantId, string table, string column, CancellationToken ct = default);
}
