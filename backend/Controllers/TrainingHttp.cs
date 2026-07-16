using JuggerHub.Services.Trainings;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>Maps a <see cref="TrainingOutcome"/> to an HTTP problem response, shared by the trainings controllers (feature 018).</summary>
internal static class TrainingHttp
{
    public static ObjectResult Fail(ControllerBase controller, TrainingOutcome outcome, string? detail) => outcome switch
    {
        TrainingOutcome.NotFound => controller.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail ?? "No such training."),
        TrainingOutcome.Forbidden => controller.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail ?? "Not allowed."),
        TrainingOutcome.NotTeamAdmin => controller.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail ?? "Only a team admin can do this."),
        TrainingOutcome.Invalid => controller.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: detail),
        TrainingOutcome.Conflict => controller.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail),
        _ => controller.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Request failed", detail: detail),
    };
}
