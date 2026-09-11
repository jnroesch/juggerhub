using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Email;

/// <summary>
/// The name an account email greets its recipient by: the player's handle.
/// </summary>
/// <remarks>
/// Not the local part of the email address, which these emails used before: that is a string the
/// player never chose as a name, while the handle is the one they picked at registration.
///
/// The profile is usually NOT loaded: callers hand over a <see cref="User"/> from
/// <c>UserManager.FindBy…Async</c>, which does not include it. Only registration has it in memory,
/// because the profile rides the same graph as the new account.
/// </remarks>
internal static class EmailRecipientName
{
    public static async Task<string> ForAsync(AppDbContext db, User user, CancellationToken ct)
    {
        var handle = user.Profile?.Handle
            ?? await db.PlayerProfiles
                .Where(p => p.UserId == user.Id)
                .Select(p => p.Handle)
                .FirstOrDefaultAsync(ct);

        // Every account gets its profile atomically at registration, so this is the unreachable
        // case in practice — kept so a missing row degrades a greeting, not an email.
        return handle is { Length: > 0 } ? handle : "there";
    }
}
