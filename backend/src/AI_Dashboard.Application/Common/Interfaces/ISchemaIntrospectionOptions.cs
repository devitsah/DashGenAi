namespace AI_Dashboard.Application.Common.Interfaces;

public interface ISchemaIntrospectionOptions
{
    string[] AllowedTables { get; }
    string[] ExcludedTables { get; }
    string? TenantColumn { get; }
    string[] SensitiveColumns { get; }
    string[] ExcludedFilterColumns { get; }
    short[] WarmupTenantIds { get; }
    double SchemaRelevanceMaxDistance { get; }
}