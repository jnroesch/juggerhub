using System.Text.Json;

namespace JuggerHub.Common;

/// <summary>
/// Writes generic RFC7231 <c>ProblemDetails</c> responses — never a stack trace,
/// internal message, or secret. Shared by the global exception handler and the
/// JWT challenge so the error envelope and JSON naming policy stay consistent and
/// the serializer options are allocated once (constitution Principle I).
/// </summary>
public static class ProblemResponse
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Sets the status/content-type and writes the problem body. Callers must
    /// check <see cref="HttpResponse.HasStarted"/> first.
    /// </summary>
    public static Task WriteAsync(HttpContext context, int status, string type, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new { type, title, status, detail };
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions));
    }
}
