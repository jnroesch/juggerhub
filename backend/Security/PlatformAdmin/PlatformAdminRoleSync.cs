using JuggerHub.Common;
using JuggerHub.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace JuggerHub.Security.PlatformAdmin;

/// <summary>
/// Startup synchronization of the <see cref="RoleName"/> Identity role against the
/// configured admin identities (feature 013, replacing 012's interim per-request
/// allowlist — GitHub issue #21). Configuration is the source of truth and this sync
/// MIRRORS it: configured emails whose accounts exist gain the role, current members
/// no longer configured lose it, and configured emails without an account are skipped
/// (picked up at a later startup, once registered).
/// </summary>
/// <remarks>
/// Runs once per process start, right after migrations. Idempotent. Never throws:
/// a failed sync only means admins may be missing, and the authorization side fails
/// closed anyway (no role membership ⇒ no access). Zero configured admins is legal
/// but loudly logged — the platform then has no working administrator.
/// </remarks>
public sealed class PlatformAdminRoleSync
{
    public const string RoleName = "PlatformAdmin";

    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly UserManager<User> _users;
    private readonly IOptions<AdminOptions> _options;
    private readonly ILogger<PlatformAdminRoleSync> _logger;

    public PlatformAdminRoleSync(
        RoleManager<IdentityRole<Guid>> roles,
        UserManager<User> users,
        IOptions<AdminOptions> options,
        ILogger<PlatformAdminRoleSync> logger)
    {
        _roles = roles;
        _users = users;
        _options = options;
        _logger = logger;
    }

    public async Task SyncAsync()
    {
        try
        {
            await SyncCoreAsync();
        }
        catch (Exception ex)
        {
            // Never block startup: with a failed sync the policy simply authorizes
            // nobody new (fail closed). Generic log only; no config values leaked.
            _logger.LogError(ex, "Platform-admin role sync failed; admin designations may be stale.");
        }
    }

    /// <summary>
    /// Designates a just-created account iff its email is configured — so the very first
    /// admin doesn't need a restart between registering and being an admin. Config stays
    /// the only source of truth; this merely applies it earlier than the next startup.
    /// Never throws (registration must not fail over this); fail direction is closed.
    /// </summary>
    public async Task TryDesignateOnRegistrationAsync(User user)
    {
        try
        {
            var email = user.Email?.Trim().ToLowerInvariant();
            if (email is null || !_options.Value.NormalizedEmails.Contains(email))
            {
                return;
            }

            if (!await _roles.RoleExistsAsync(RoleName))
            {
                await _roles.CreateAsync(new IdentityRole<Guid>(RoleName));
            }

            var added = await _users.AddToRoleAsync(user, RoleName);
            LogResult(added, "Granted platform-admin to user {UserId} at registration (configured identity).", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform-admin designation at registration failed; the startup sync will retry.");
        }
    }

    private async Task SyncCoreAsync()
    {
        if (!await _roles.RoleExistsAsync(RoleName))
        {
            var created = await _roles.CreateAsync(new IdentityRole<Guid>(RoleName));
            if (!created.Succeeded)
            {
                _logger.LogError("Could not create the {Role} role: {Codes}",
                    RoleName, string.Join(',', created.Errors.Select(e => e.Code)));
                return;
            }
        }

        var configured = _options.Value.NormalizedEmails;
        var currentMembers = await _users.GetUsersInRoleAsync(RoleName);

        // Revoke: members whose email is no longer configured (mirror semantics).
        foreach (var member in currentMembers)
        {
            var email = member.Email?.Trim().ToLowerInvariant();
            if (email is null || !configured.Contains(email))
            {
                var removed = await _users.RemoveFromRoleAsync(member, RoleName);
                LogResult(removed, "Revoked platform-admin from user {UserId} (no longer configured).", member.Id);
            }
        }

        // Grant: configured emails with an existing account that lacks the role.
        foreach (var email in configured)
        {
            var user = await _users.FindByEmailAsync(email);
            if (user is null)
            {
                // Do not log the address itself at warning level in a way that suggests
                // action; it simply hasn't registered yet.
                _logger.LogInformation("A configured platform-admin identity has no account yet; skipped (picked up after registration at the next startup).");
                continue;
            }

            if (!await _users.IsInRoleAsync(user, RoleName))
            {
                var added = await _users.AddToRoleAsync(user, RoleName);
                LogResult(added, "Granted platform-admin to user {UserId}.", user.Id);
            }
        }

        var adminCount = (await _users.GetUsersInRoleAsync(RoleName)).Count;
        if (adminCount == 0)
        {
            _logger.LogWarning(
                "No platform administrators exist ({Role} role is empty). All admin operations will be refused until ADMIN_EMAILS is configured and the app restarted.",
                RoleName);
        }
        else
        {
            _logger.LogInformation("Platform-admin role sync complete: {Count} administrator(s).", adminCount);
        }
    }

    private void LogResult(IdentityResult result, string successMessage, Guid userId)
    {
        if (result.Succeeded)
        {
            _logger.LogInformation(successMessage, userId);
        }
        else
        {
            _logger.LogError("Platform-admin role change for user {UserId} failed: {Codes}",
                userId, string.Join(',', result.Errors.Select(e => e.Code)));
        }
    }
}
