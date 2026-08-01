using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Infrastructure.SchemaIntrospection;

public class SchemaIntrospectionOptions : ISchemaIntrospectionOptions
{
    public string[] AllowedTables { get; set; } = Array.Empty<string>();
    public string[] ExcludedTables { get; set; } = Array.Empty<string>();
    public string? TenantColumn { get; set; } = "tenant_id";
    public string[] SensitiveColumns { get; set; } = Array.Empty<string>();
    public string[] ExcludedFilterColumns { get; set; } = Array.Empty<string>();
    public short[] WarmupTenantIds { get; set; } = [1];

    // Max cosine DISTANCE (pgvector <=> operator: 0 = identical, 2 = opposite)
    // a schema_chunks/relationship_chunks/metadata_catalog match may have to be
    // considered "relevant". Anything above this is treated as off-domain
    // ("Book a movie ticket", "Show planets") rather than forced into a match.
    // Needs empirical tuning against your embedding model (nomic-embed-text) —
    // 0.75 is a conservative starting point, not a validated constant.
    public double SchemaRelevanceMaxDistance { get; set; } = 0.75;
}