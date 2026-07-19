using JuggerHub.Services.Chat;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>Maps a <see cref="ChatOutcome"/> to an HTTP problem response, shared by the chat controllers (feature 019).</summary>
/// <remarks>
/// Note the deliberate absence of a "not a member" status: <see cref="ChatOutcome.NotFound"/> covers
/// both "no such conversation" and "not yours", because a 403 would confirm that a conversation exists
/// (spec FR-048). The detail strings stay generic for the same reason.
/// </remarks>
internal static class ChatHttp
{
    public static ObjectResult Fail(ControllerBase controller, ChatOutcome outcome, string? detail) => outcome switch
    {
        ChatOutcome.NotFound => controller.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail ?? "No such chat."),
        ChatOutcome.Forbidden => controller.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail ?? "Not allowed."),
        ChatOutcome.Invalid => controller.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: detail),
        ChatOutcome.Conflict => controller.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail ?? "This chat is closed."),
        _ => controller.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Request failed", detail: detail),
    };
}
