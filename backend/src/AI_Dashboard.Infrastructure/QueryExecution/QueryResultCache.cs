using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AI_Dashboard.Infrastructure.QueryExecution;

/// Singleton cache for widget query results.
/// Key: SHA256 of the SQL string. TTL: 5 minutes.
/// A background timer evicts expired entries every 10 minutes so memory
/// doesn't grow unbounded when many unique queries are executed.
public class QueryResultCache : IDisposable
{
    private record Entry(List<Dictionary<string, object?>> Rows, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _cache = new();
    private readonly TimeSpan _ttl;
    private readonly Timer _evictionTimer;

    public QueryResultCache(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
        _evictionTimer = new Timer(_ => Evict(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    public bool TryGet(string sql, out List<Dictionary<string, object?>> rows)
    {
        rows = new();
        var key = Hash(sql);
        if (!_cache.TryGetValue(key, out var entry) || DateTime.UtcNow > entry.ExpiresAt)
            return false;
        rows = entry.Rows;
        return true;
    }

    public void Set(string sql, List<Dictionary<string, object?>> rows)
        => _cache[Hash(sql)] = new Entry(rows, DateTime.UtcNow.Add(_ttl));

    private void Evict()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _cache.Keys)
            if (_cache.TryGetValue(key, out var entry) && now > entry.ExpiresAt)
                _cache.TryRemove(key, out _);
    }

    public void Dispose() => _evictionTimer.Dispose();

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
