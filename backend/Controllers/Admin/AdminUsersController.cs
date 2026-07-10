using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// Admin user management (feature 013 US3/US4): search/list, per-player detail, and
/// the recorded account actions. Thin — outcome mapping only; rules live in
/// <see cref="IAdminUserService"/>. Server-side <c>PlatformAdmin</c> policy is the
/// boundary for every route.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class AdminUsersController : AdminControllerBase
{
    private readonly IAdminUserService _adminUsers;

    public AdminUsersController(IAdminUserService adminUsers) => _adminUsers = adminUsers;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserListItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] AccountStatus? status,
        [FromQuery] PaginationRequest pagination,
        CancellationToken ct)
        => Ok(await _adminUsers.SearchAsync(q, status, pagination, ct));

    [HttpGet("{handle}")]
    public async Task<ActionResult<AdminUserDetailDto>> Detail(string handle, CancellationToken ct)
    {
        var detail = await _adminUsers.GetDetailAsync(handle, ct);
        return detail is null ? PlayerNotFound() : Ok(detail);
    }

    [HttpPost("{handle}/suspend")]
    public Task<IActionResult> Suspend(string handle, CancellationToken ct) =>
        ActAsync(handle, _adminUsers.SuspendAsync, ct);

    [HttpPost("{handle}/reinstate")]
    public Task<IActionResult> Reinstate(string handle, CancellationToken ct) =>
        ActAsync(handle, _adminUsers.ReinstateAsync, ct);

    [HttpPost("{handle}/ban")]
    public Task<IActionResult> Ban(string handle, CancellationToken ct) =>
        ActAsync(handle, _adminUsers.BanAsync, ct);

    [HttpPost("{handle}/unban")]
    public Task<IActionResult> Unban(string handle, CancellationToken ct) =>
        ActAsync(handle, _adminUsers.UnbanAsync, ct);

    [HttpPost("{handle}/reset-password")]
    public Task<IActionResult> ResetPassword(string handle, CancellationToken ct) =>
        ActAsync(handle, _adminUsers.SendPasswordResetAsync, ct);

    private async Task<IActionResult> ActAsync(
        string handle,
        Func<Guid, string, CancellationToken, Task<AdminUserActionOutcome>> action,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var actorId))
        {
            return Unauthorized();
        }

        var outcome = await action(actorId, handle, ct);
        return outcome switch
        {
            AdminUserActionOutcome.Done => NoContent(),
            AdminUserActionOutcome.NotFound => PlayerNotFound(),
            AdminUserActionOutcome.InvalidTransition => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Nothing to change",
                detail: "The account is not in a state this action applies to."),
            AdminUserActionOutcome.ProtectedAdmin => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity, title: "Admin accounts are protected",
                detail: "Platform administrators can't be suspended or banned. Remove them from the admin configuration first."),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Action failed"),
        };
    }

    private ObjectResult PlayerNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Player not found",
            detail: "No player matches that handle.");
}
