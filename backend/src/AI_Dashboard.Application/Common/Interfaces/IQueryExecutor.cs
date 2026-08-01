namespace AI_Dashboard.Application.Common.Interfaces;

public interface IQueryExecutor
{
    /// Executes a read-only SQL query (already validated as safe) and returns rows as dictionaries.
    Task<List<Dictionary<string, object?>>> ExecuteAsync(string sql, CancellationToken ct = default);
}