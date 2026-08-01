using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public CurrentUserService(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public short TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;
            return claim is not null ? short.Parse(claim) : throw new UnauthorizedAccessException("tenant_id claim missing");
        }
    }

    public int UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value;
            return claim is not null ? int.Parse(claim) : throw new UnauthorizedAccessException("user_id claim missing");
        }
    }
}