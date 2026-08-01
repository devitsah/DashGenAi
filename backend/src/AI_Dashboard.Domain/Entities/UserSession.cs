using AI_Dashboard.Domain.Common;

namespace AI_Dashboard.Domain.Entities;

public class UserSession : ITimestamped
{
    public short TenantId { get; set; } = 1;
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public string TokenHash { get; set; } = default!;
  

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}