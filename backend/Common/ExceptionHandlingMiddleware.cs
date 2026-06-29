using System.Net;
using System.Text.Json;

namespace JuggerHub.Common;

/// <summary>
/// Global exception handler (constitution Principle I). Logs the full exception
/// server-side and returns a generic RFC7231 <c>ProblemDetails</c> body — never a
/// stack trace, internal message, or secret — with HTTP 500.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
            _logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
            await HandleExceptionAsync(context);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context)
    {
        // If the response has already started we cannot safely rewrite it.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "An error occurred",
            status = (int)HttpStatusCode.InternalServerError,
            detail = "An unexpected error occurred. Please try again later.",
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions));
    }
}
