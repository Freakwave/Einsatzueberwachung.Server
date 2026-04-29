using System.Text.Json;
using Einsatzueberwachung.Server.Models;

namespace Einsatzueberwachung.Server.Middleware;

public sealed class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unbehandelte Exception bei {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = ex is InvalidOperationException
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;

        context.Response.ContentType = "application/json";

        var response = new ErrorResponse("Ein interner Fehler ist aufgetreten.");
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}
