namespace AI_Dashboard.Application.Common.Interfaces;

// Resolves tenant_id/user_id from the authenticated JWT claims instead of hardcoding them.
// Every controller should pull from this, not from request body/query params,
// so a user can never act on another tenant's or another user's data.
public interface ICurrentUserService
{
    short TenantId { get; }
    int UserId { get; }
    bool IsAuthenticated { get; }
}