namespace AI_Dashboard.Domain.Common;
 
/// <summary>
/// Common fields every platform entity carries. Mirrors the audit columns
/// (created_by/updated_by/created_at/updated_at) present on every table in
/// the platform schema's ER diagram.
/// </summary>
public abstract class BaseEntity
{
   public int Id { get; set; }
   public short TenantId { get; set; } = 1;
   public int? CreatedBy { get; set; }
   public int? UpdatedBy { get; set; }
   public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
   public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
 