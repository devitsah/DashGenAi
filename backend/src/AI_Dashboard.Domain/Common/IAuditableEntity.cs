namespace AI_Dashboard.Domain.Common;

/// <summary>
/// Implemented by entities that track full audit info (created_by/updated_by
/// in addition to the timestamps from ITimestamped). AppDbContext.SaveChanges
/// auto-stamps CreatedBy on insert and UpdatedBy on update from the current
/// authenticated user, so callers never need to set these by hand.
/// </summary>
public interface IAuditableEntity : ITimestamped
{
    int CreatedBy { get; set; }
    int? UpdatedBy { get; set; }
}
