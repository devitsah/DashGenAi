using System.Collections.Concurrent;
using AI_Dashboard.Application.Common.Schema;

namespace AI_Dashboard.Infrastructure.SchemaIntrospection;

public class SchemaCache
{
    private readonly ConcurrentDictionary<short, List<TableMetadata>> _cache = new();

    public List<TableMetadata>? Get(short tenantId) =>
        _cache.TryGetValue(tenantId, out var t) ? t : null;

    public void Set(short tenantId, List<TableMetadata> tables) => _cache[tenantId] = tables;
}
