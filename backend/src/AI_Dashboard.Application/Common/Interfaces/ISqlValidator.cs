namespace AI_Dashboard.Application.Common.Interfaces;

public interface ISqlValidator
{
    /// Returns null if SQL is valid, or an error message if invalid.
    Task<string?> ValidateAsync(string sql, CancellationToken ct = default);
}
