namespace AI_Dashboard.Domain.Common;

/// <summary>
/// Implemented by entities that track creation/update timestamps.
/// AppDbContext.SaveChanges uses this to auto-stamp CreatedAt on insert
/// and UpdatedAt on update, so no repository/handler needs to set these by hand.
/// </summary>
public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
