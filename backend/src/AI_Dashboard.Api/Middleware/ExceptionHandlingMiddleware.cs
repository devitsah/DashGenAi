using System.Net;
using System.Text.Json;

namespace AI_Dashboard.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path} — Inner: {Inner}",
                context.Request.Path, ex.InnerException?.Message ?? "none");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex switch
            {
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException        => (int)HttpStatusCode.NotFound,
                InvalidOperationException   => (int)HttpStatusCode.BadRequest,
                _                          => (int)HttpStatusCode.InternalServerError
            };

            var inner   = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message;
            var message = inner is not null ? $"{ex.Message} — {inner}" : ex.Message;
            var payload = JsonSerializer.Serialize(new { message });
            await context.Response.WriteAsync(payload);
        }
    }
}