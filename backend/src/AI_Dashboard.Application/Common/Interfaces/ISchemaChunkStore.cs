using AI_Dashboard.Application.Common.Schema;

namespace AI_Dashboard.Application.Common.Interfaces;

public interface ISchemaChunkStore
{
    // --- Clear all RAG data for a tenant (call before rebuild to remove stale entries) ---
    Task ClearAsync(short tenantId, CancellationToken ct = default);

    // --- True if this tenant has ANY schema chunks at all. Used to detect a tenant
    // that was never covered by SchemaPreWarmService's background warmup (wrong
    // WarmupTenantIds config, warmup still running, or warmup failed at startup),
    // so callers can build on demand instead of forever returning "no match". ---
    Task<bool> HasChunksAsync(short tenantId, CancellationToken ct = default);

    // --- Schema chunks (table + column structure) ---
    Task RebuildAsync(short tenantId, List<TableMetadata> tables, CancellationToken ct = default);
    Task<List<TableMetadata>> RetrieveRelevantTablesAsync(
        short tenantId, string queryText, List<TableMetadata> allTables, int topK = 2, CancellationToken ct = default);

    // --- Relationship chunks (FK join paths) ---
    Task RebuildRelationshipsAsync(short tenantId, List<TableMetadata> tables, CancellationToken ct = default);
    Task<List<RelationshipChunk>> RetrieveRelevantRelationshipsAsync(
        short tenantId, string queryText, int topK = 3, CancellationToken ct = default);

    // --- Metadata catalog (per-column sample values) ---
    Task RebuildMetadataCatalogAsync(short tenantId, List<TableMetadata> tables,
        Func<string, string, Task<List<string>>> getSampleValues, CancellationToken ct = default);
    Task<List<MetadataEntry>> RetrieveRelevantMetadataAsync(
        short tenantId, string queryText, int topK = 5, CancellationToken ct = default);
}

public record RelationshipChunk(string FromTable, string ToTable, string RelationshipText);
public record MetadataEntry(string TableName, string ColumnName, string DataType, List<string> SampleValues);