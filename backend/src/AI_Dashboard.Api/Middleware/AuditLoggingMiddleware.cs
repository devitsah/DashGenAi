namespace AI_Dashboard.Api.Middleware;
 
/// <summary>
/// Logs every authenticated, state-changing (POST/PUT/DELETE) request at
/// the HTTP level — who, what endpoint, when, and the resulting status
/// code. This is a coarse, always-on safety net; the detailed,
/// entity-level audit trail (old value / new value per dashboard or
/// widget change) is recorded explicitly via AuditTrailRecorder inside
/// the relevant command handlers, which Phase 3's audit UI reads from.
/// </summary>
public class AuditLoggingMiddleware
{
   private readonly RequestDelegate _next;
   private readonly ILogger<AuditLoggingMiddleware> _logger;
 
   private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase)
   {
       "POST", "PUT", "DELETE", "PATCH"
   };
 
   public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
   {
       _next = next;
       _logger = logger;
   }
 
   public async Task InvokeAsync(HttpContext context)
   {
       await _next(context);
 
       if (!AuditedMethods.Contains(context.Request.Method))
           return;
 
       var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
 
       _logger.LogInformation(
           "AUDIT user={UserId} method={Method} path={Path} status={StatusCode}",
           userId, context.Request.Method, context.Request.Path, context.Response.StatusCode);
   }
}
