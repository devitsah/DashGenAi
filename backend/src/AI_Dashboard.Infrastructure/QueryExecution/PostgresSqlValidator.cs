using AI_Dashboard.Application.Common.Interfaces;
using Npgsql;

namespace AI_Dashboard.Infrastructure.QueryExecution;

public class PostgresSqlValidator : ISqlValidator
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSqlValidator(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<string?> ValidateAsync(string sql, CancellationToken ct = default)
    {
        var testSql = sql.Replace(":tenantId", "0", StringComparison.OrdinalIgnoreCase);
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand($"EXPLAIN {testSql}", conn);
            // EXPLAIN returns rows — must use ExecuteReaderAsync and fully consume/dispose
            // the reader before returning the connection to the pool. Using
            // ExecuteNonQueryAsync on a result-returning statement leaves the connection
            // in a dirty state, causing "A command is already in progress" for the next
            // caller that gets the same pooled connection.
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) { } // drain all rows
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}