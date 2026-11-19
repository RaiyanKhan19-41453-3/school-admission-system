using System.Net;
using System.Text.Json;
using Admissions.Services;

namespace Admissions.Middleware;

/// <summary>Converts domain exceptions into clean JSON responses; logs unexpected errors.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (DomainException ex)
        {
            await WriteAsync(ctx, ex.Status, new { error = ex.Code, message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await WriteAsync(ctx, (int)HttpStatusCode.InternalServerError,
                new { error = "SERVER_ERROR", message = "An unexpected error occurred." });
        }
    }

    static async Task WriteAsync(HttpContext ctx, int status, object body)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(body);
    }
}
