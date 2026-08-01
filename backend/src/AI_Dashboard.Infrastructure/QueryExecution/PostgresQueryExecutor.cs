using AI_Dashboard.Application.Common.Interfaces;
using Npgsql;

namespace AI_Dashboard.Infrastructure.QueryExecution;

public class PostgresQueryExecutor : IQueryExecutor
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly QueryResultCache _cache;

    private static readonly string[] ForbiddenKeywords =
        { "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE", "GRANT", "REVOKE", ";--" };

    // Pull connections from the app's single shared NpgsqlDataSource (same one EF Core
    // and pgvector use) instead of building a raw NpgsqlConnection straight from the
    // connection string. Two independent pools against the same connection string can
    // hand out physical connections that step on each other under concurrent load
    // (schema pre-warm, validator, and executor all running around the same time),
    // which surfaces as Npgsql's "A command is already in progress" guard exception
    // even though each caller's own code is behaving correctly in isolation.
    public PostgresQueryExecutor(NpgsqlDataSource dataSource, QueryResultCache cache)
    {
        _dataSource = dataSource;
        _cache = cache;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteAsync(string sql, CancellationToken ct = default)
    {
        ValidateReadOnly(sql);

        if (sql.Contains(":tenantId", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Query contains unresolved :tenantId placeholder.");

        if (_cache.TryGet(sql, out var cached)) return cached;

        const int RowLimit = 500;
        var safeSql = EnforceLimit(sql, RowLimit);

        var results = new List<Dictionary<string, object?>>();

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        // Use SET SESSION CHARACTERISTICS instead of a transaction so we don't need
        // to manage a transaction object alongside an open reader on the same connection.
        // This avoids the "A command is already in progress" error that occurred when
        // the validator's EXPLAIN and the executor's reader shared a pooled connection.
        await using (var setCmd = new NpgsqlCommand("SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY", conn))
            await setCmd.ExecuteNonQueryAsync(ct);

        try
        {
            await using var cmd = new NpgsqlCommand(safeSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : Normalize(reader.GetValue(i));
                results.Add(row);
            }
        }
        finally
        {
            // Reset to read-write so the pooled connection is clean for the next caller
            // (EF Core writes, schema introspection, etc. all share this pool).
            await using var resetCmd = new NpgsqlCommand("SET SESSION CHARACTERISTICS AS TRANSACTION READ WRITE", conn);
            await resetCmd.ExecuteNonQueryAsync(ct);
        }

        _cache.Set(sql, results);
        return results;
    }

    private static void ValidateReadOnly(string sql)
    {
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only SELECT statements are permitted.");

        var upper = sql.ToUpperInvariant();
        foreach (var kw in ForbiddenKeywords)
            if (upper.Contains(kw))
                throw new InvalidOperationException($"Query contains a forbidden keyword: {kw}");
    }

    // Appends LIMIT if the query doesn't already have one, preventing unbounded result sets.
    private static string EnforceLimit(string sql, int limit)
    {
        var upper = sql.ToUpperInvariant();
        return upper.Contains("LIMIT") ? sql : $"{sql.TrimEnd().TrimEnd(';')} LIMIT {limit}";
    }

    // Converts Npgsql-specific and .NET types that System.Text.Json can't serialize
    // into safe primitives (string / long / double / bool / null).
    private static object? Normalize(object? value) => value switch
    {
        null                                    => null,
        bool b                                  => b,
        byte or short or int or long            => Convert.ToInt64(value),
        float or double or decimal              => Convert.ToDouble(value),
        DateTime dt                             => dt.ToString("o"),
        DateTimeOffset dto                      => dto.ToString("o"),
        DateOnly d                              => d.ToString("yyyy-MM-dd"),
        TimeOnly t                              => t.ToString("HH:mm:ss"),
        TimeSpan ts                             => ts.ToString(),
        Guid g                                  => g.ToString(),
        _                                       => value.ToString()
    };
}