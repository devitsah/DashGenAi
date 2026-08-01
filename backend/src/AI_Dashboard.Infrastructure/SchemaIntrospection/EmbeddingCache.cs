using System.Collections.Concurrent;
using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Infrastructure.SchemaIntrospection;

public class EmbeddingCache : IEmbeddingCache
{
    private readonly ConcurrentDictionary<string, float[]> _cache = new();

    public async Task<float[]> GetOrAddAsync(string key, Func<Task<float[]>> factory)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var vec = await factory();
        _cache[key] = vec;
        return vec;
    }
}
