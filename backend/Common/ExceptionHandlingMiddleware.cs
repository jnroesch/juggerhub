using System.Net;

namespace JuggerHub.Common;

/// <summary>
/// Global exception handler (constitution Principle I). Logs the full exception
/// server-side and returns a generic RFC7231 <c>ProblemDetails</c> body — never a
/// stack trace, internal message, or secret — with HTTP 500.
/// </summary>
public sealed class ExceptionHandlingMiddleware
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
            // Strip CR/LF from user-controlled request values to prevent log forging.
            _logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}",
                Sanitize(context.Request.Method),
                Sanitize(context.Request.Path.ToString()));
            await HandleExceptionAsync(context);
        }
    }

    private static string Sanitize(string value) =>
        value.Replace("\r", "").Replace("\n", "");

    private static Task HandleExceptionAsync(HttpContext context)
    {
        // If the response has already started we cannot safely rewrite it.
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.Clear();
        return ProblemResponse.WriteAsync(
            context,
            (int)HttpStatusCode.InternalServerError,
            "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            "An error occurred",
            "An unexpected error occurred. Please try again later.");
    }
}
