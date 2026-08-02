using JuggerHub.Data;
using JuggerHub.Dtos.Account;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Media;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Account;

/// <summary>
/// Self-service account erasure (feature 037). See <see cref="IAccountDeletionService"/> for the
/// contract; this file is where the disposition of every referencing table actually happens.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape, and why.</b> The <c>User</c> row is <em>not</em> deleted — it cannot be. Roughly
/// twenty foreign keys into it are <c>DeleteBehavior.Restrict</c> by explicit design, each one
/// protecting a record the spec independently says must survive: moderation history, awards granted
/// to other people, news posts, conversations other members are also part of. A row-level delete
/// throws before it touches anything. So: delete the <see cref="PlayerProfile"/> (everything the
/// member owns cascades off it), delete the rows that exist solely to serve them, and neutralise the
/// account row itself. What is left points at an id that identifies nobody.
/// </para>
/// <para>
/// <b>The placeholder is not built here.</b> Retained messages and posts read as "A former player"
/// because the projections fall back when <c>Sender.Profile.DisplayName</c> comes back null — and
/// deleting the profile is exactly what makes it null. Erasure needed no rendering changes
/// (see <see cref="Common.MemberPlaceholder"/>).
/// </para>
/// </remarks>
public sealed class AccountDeletionService : IAccountDeletionService
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _users;
    private readonly SignInManager<User> _signIn;
    private readonly IMediaStore _media;
    private readonly AccountEmailService _email;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        AppDbContext db,
        UserManager<User> users,
        SignInManager<User> signIn,
        IMediaStore media,
        AccountEmailService email,
        ILogger<AccountDeletionService> logger)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _media = media;
        _email = email;
        _logger = logger;
    }

    /// <summary>
    /// What the member is told is erased. Keys, not prose — the client owns the three-language
    /// catalogue (FR-008).
    /// </summary>
    private static readonly string[] ErasedCategories =
        ["Profile", "Photo", "Email", "Memberships", "Notifications", "Sessions"];

    /// <summary>
    /// What survives. <c>ChatMessages</c> and <c>NewsPosts</c> are the ones a member will not
    /// expect (FR-025), which is why they lead.
    /// </summary>
    private static readonly string[] RetainedCategories =
        ["ChatMessages", "NewsPosts", "ModerationRecords", "AwardsGrantedToOthers"];

    /// <summary>
    /// The confirmation literal, per supported language (feature 037 T064). The server accepts the
    /// whole set rather than one hardcoded English word, so a German member types a German word and
    /// the client's own catalogue decides what to ask for.
    /// </summary>
    private static readonly string[] AcceptedConfirmations = ["DELETE", "LÖSCHEN", "ELIMINAR"];

    // --- Preview ---------------------------------------------------------------

    public async Task<AccountDeletionPreviewDto?> PreviewAsync(Guid userId, CancellationToken ct = default)
    {
        var status = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (AccountStatus?)u.Status)
            .FirstOrDefaultAsync(ct);

        if (status is null || !IsEligible(status.Value))
        {
            return null;
        }

        var blockers = await FindBlockersAsync(userId, ct);

        return new AccountDeletionPreviewDto(
            CanDelete: blockers.Count == 0,
            Blockers: blockers,
            Retained: RetainedCategories,
            Erased: ErasedCategories);
    }

    // --- Erasure ---------------------------------------------------------------

    public async Task<AccountDeletionResult> DeleteAsync(
        Guid userId,
        string password,
        string confirmation,
        CancellationToken ct = default)
    {
        // Cheapest check first, and deliberately before the password: a mistyped confirmation must
        // not consume a lockout attempt (spec contract, 400 before 401).
        if (!AcceptedConfirmations.Contains(confirmation?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return AccountDeletionResult.Fail(AccountDeletionOutcome.ConfirmationMismatch);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !IsEligible(user.Status))
        {
            return AccountDeletionResult.Fail(AccountDeletionOutcome.NotEligible);
        }

        // Re-authentication (FR-003). With no grace period, this and the typed confirmation are the
        // ONLY protection against a regretted click (FR-037) — hence lockoutOnFailure, so the check
        // cannot be brute-forced against a session someone walked away from.
        var check = await _signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return AccountDeletionResult.Fail(AccountDeletionOutcome.PasswordRejected);
        }

        // Read the object key BEFORE the cascade removes the descriptor row. Feature 035 moved avatar
        // bytes to blob storage, so deleting the row deletes a POINTER — the image outlives it unless
        // we reclaim it explicitly (FR-015).
        var avatarObjectKey = await _db.ProfileAvatars.AsNoTracking().IgnoreQueryFilters()
            .Where(a => a.Profile.UserId == userId)
            .Select(a => a.ObjectKey)
            .FirstOrDefaultAsync(ct);

        // Send while the address still exists (FR-040). Before the transaction, not inside it: a
        // delivery failure must never roll back an erasure the member asked for — see SendFarewell.
        await SendFarewellAsync(user, ct);

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            var blockers = await strategy.ExecuteAsync(async () =>
            {
                // EVERYTHING that mutates state lives inside this delegate: the strategy replays the
                // whole block on a transient fault, so anything staged outside would be applied twice
                // (constitution Principle VII).
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                _db.ChangeTracker.Clear();

                // Re-check inside the transaction. A blocker acquired between preview and confirm is
                // caught here rather than half-applied (FR-013).
                var outstanding = await FindBlockersAsync(userId, ct);
                if (outstanding.Count > 0)
                {
                    await tx.RollbackAsync(ct);
                    return outstanding;
                }

                await EraseOwnedDataAsync(userId, ct);
                await NeutraliseAccountAsync(userId, ct);

                await tx.CommitAsync(ct);
                return (IReadOnlyList<DeletionBlockerDto>)[];
            });

            if (blockers.Count > 0)
            {
                return AccountDeletionResult.Blocked(blockers);
            }
        }
        catch (Exception ex)
        {
            // Nothing is reported to the member beyond "it did not happen" (FR-042, Principle I).
            // The account is intact because the transaction rolled back.
            _logger.LogError(ex, "Account erasure failed and was rolled back for {UserId}", userId);
            return AccountDeletionResult.Fail(AccountDeletionOutcome.Failed);
        }

        // AFTER commit: a blob delete cannot be rolled back, so doing it earlier would destroy an
        // image for an erasure that then failed. A failure here leaves an orphan that 035's
        // reconciliation sweep reclaims — logged, never surfaced (FR-015).
        await ReclaimAvatarObjectAsync(avatarObjectKey, userId, ct);

        return AccountDeletionResult.Done();
    }

    // --- Eligibility -----------------------------------------------------------

    /// <summary>
    /// Only a live account may erase itself (FR-005). Suspended and banned are refused here rather
    /// than relying on the sign-in gate, because this refusal is what stops erasure being a route out
    /// of moderation — and it is what makes releasing the email address safe (FR-031, FR-033).
    /// </summary>
    private static bool IsEligible(AccountStatus status) => status == AccountStatus.Active;

    // --- Blockers --------------------------------------------------------------

    /// <summary>
    /// Every outstanding obligation, in one pass (FR-011). The existing last-admin guard on
    /// <c>TeamService</c> answers "may this one member leave this one team"; erasure needs the whole
    /// set at once so the member can clear them in a single sitting rather than discovering them one
    /// refusal at a time.
    /// </summary>
    private async Task<IReadOnlyList<DeletionBlockerDto>> FindBlockersAsync(Guid userId, CancellationToken ct)
    {
        var blockers = new List<DeletionBlockerDto>();

        // Teams — mirrors TeamService's rule: a team always keeps at least one admin.
        var soleTeams = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.UserId == userId
                && m.Role == TeamRole.Admin
                && !_db.TeamMemberships.Any(o => o.TeamId == m.TeamId
                    && o.UserId != userId
                    && o.Role == TeamRole.Admin))
            .Select(m => new { m.TeamId, m.Team.Name })
            .ToListAsync(ct);

        blockers.AddRange(soleTeams.Select(t => new DeletionBlockerDto(
            DeletionBlockerKind.SoleTeamAdmin, t.TeamId, t.Name, DeletionBlockerRemedy.MakeAnotherAdmin)));

        // Events — no equivalent guard existed; feature 037 defines one (FR-010). Cancelled events
        // are excluded: there is nothing left to administer.
        var soleEvents = await _db.EventAdmins.AsNoTracking()
            .Where(a => a.UserId == userId
                && a.Event.Status != EventStatus.Cancelled
                && !_db.EventAdmins.Any(o => o.EventId == a.EventId && o.UserId != userId))
            .Select(a => new { a.EventId, a.Event.Name })
            .ToListAsync(ct);

        blockers.AddRange(soleEvents.Select(e => new DeletionBlockerDto(
            DeletionBlockerKind.SoleEventAdmin, e.EventId, e.Name, DeletionBlockerRemedy.MakeAnotherAdmin)));

        // Parties — same reasoning. Named by their event, since a party has no name of its own.
        var soleParties = await _db.PartyMembers.AsNoTracking()
            .Where(m => m.UserId == userId
                && m.Role == PartyMemberRole.Admin
                && m.Status == PartyMemberStatus.In
                && !_db.PartyMembers.Any(o => o.PartyId == m.PartyId
                    && o.UserId != userId
                    && o.Role == PartyMemberRole.Admin
                    && o.Status == PartyMemberStatus.In))
            .Select(m => new { m.PartyId, EventName = m.Party.Event.Name })
            .ToListAsync(ct);

        blockers.AddRange(soleParties.Select(p => new DeletionBlockerDto(
            DeletionBlockerKind.SolePartyAdmin, p.PartyId, p.EventName, DeletionBlockerRemedy.MakeAnotherAdminOrDisband)));

        return blockers;
    }

    // --- Disposition -----------------------------------------------------------

    /// <summary>
    /// Delete everything the member owns. Ordered so the <c>Restrict</c> rows go before the profile
    /// cascade; see <c>specs/037-account-deletion/data-model.md</c> for the full inventory and the
    /// reason each table is on the list it is on.
    /// </summary>
    private async Task EraseOwnedDataAsync(Guid userId, CancellationToken ct)
    {
        // Blocks are Restrict on BOTH sides, so they must go explicitly and BEFORE the account row is
        // touched. Removing them is correct rather than merely convenient: the account they guarded
        // against no longer exists, and a returning member registers a genuinely new one (FR-020).
        await _db.UserBlocks
            .Where(b => b.BlockerUserId == userId || b.BlockedUserId == userId)
            .ExecuteDeleteAsync(ct);

        // Pending invitations, both directions (FR-019). Accepted ones have become memberships and are
        // handled below; these FKs are Restrict, so they must be explicit.
        await _db.TeamInvitations
            .Where(i => i.CreatedByUserId == userId || i.TargetUserId == userId)
            .ExecuteDeleteAsync(ct);
        await _db.EventAdminInvitations
            .Where(i => i.CreatedByUserId == userId || i.TargetUserId == userId)
            .ExecuteDeleteAsync(ct);
        await _db.PartyAdminInvitations
            .Where(i => i.CreatedByUserId == userId || i.TargetUserId == userId)
            .ExecuteDeleteAsync(ct);

        // Sessions. DELETED, not revoked: revocation leaves the row, and the row carries a per-session
        // originating IP address that must go with everything else (FR-016).
        await _db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

        // Records that exist solely to serve this member (FR-017).
        await _db.Notifications.Where(n => n.RecipientUserId == userId).ExecuteDeleteAsync(ct);
        await _db.NotificationPreferences.Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);

        // Participation (FR-018).
        await _db.TeamMemberships.Where(m => m.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.TeamJoinRequests.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.EventSignups.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.EventAdmins.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.PartyMembers.Where(m => m.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.TrainingResponses.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.MercenaryListings.Where(l => l.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.MarketRequests.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);

        // Conversation membership, not conversation content. The messages stay (FR-024); this only
        // stops the erased account being listed as a current participant.
        await _db.ConversationParticipants.Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);

        // The profile itself, LAST — this is the single delete that carries the rest with it:
        // pompfen, avatar descriptor, event participations, and awards received all cascade. It is
        // also what makes every retained message render as the neutral placeholder.
        await _db.PlayerProfiles.IgnoreQueryFilters()
            .Where(p => p.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Strip the account row of everything that identifies a person, and mark it terminal.
    /// </summary>
    /// <remarks>
    /// <b>Releasing the email is not enough to free the address.</b> Registration creates accounts as
    /// <c>new User { UserName = email, Email = email }</c> and <c>NormalizedUserName</c> carries a
    /// unique index — so a residual username collides, <c>CreateAsync</c> fails, and the failure lands
    /// on registration's deliberately neutral "accepted" response. The returning member would be told
    /// they had registered when no account was created. Every uniqueness-constrained identifier has to
    /// be released, not just the one with "email" in its name (FR-034).
    /// <para>
    /// The replacement values are derived from fresh randomness, never from the old handle, email, or
    /// a hash of either — any of those would be a re-identification vector (FR-026).
    /// </para>
    /// </remarks>
    private async Task NeutraliseAccountAsync(Guid userId, CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString("N");
        var placeholderUserName = $"deleted-{token}";

        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Status, AccountStatus.Deleted)
                .SetProperty(u => u.StatusChangedAt, DateTime.UtcNow)
                // Released, so the address can register again (FR-031). Postgres permits many NULLs
                // in a unique index, so any number of erasures coexist.
                .SetProperty(u => u.Email, (string?)null)
                .SetProperty(u => u.NormalizedEmail, (string?)null)
                .SetProperty(u => u.UserName, placeholderUserName)
                .SetProperty(u => u.NormalizedUserName, placeholderUserName.ToUpperInvariant())
                .SetProperty(u => u.EmailConfirmed, false)
                .SetProperty(u => u.PasswordHash, (string?)null)
                .SetProperty(u => u.SecurityStamp, Guid.NewGuid().ToString("N"))
                .SetProperty(u => u.ConcurrencyStamp, Guid.NewGuid().ToString("N"))
                .SetProperty(u => u.PhoneNumber, (string?)null)
                .SetProperty(u => u.PhoneNumberConfirmed, false)
                .SetProperty(u => u.TwoFactorEnabled, false)
                .SetProperty(u => u.LockoutEnd, (DateTimeOffset?)null)
                .SetProperty(u => u.AccessFailedCount, 0)
                // An interface preference that existed only to serve them (FR-017).
                .SetProperty(u => u.PreferredLanguage, (string?)null), ct);

        // Identity's own satellites. Roles included: a configured admin who erases their own account
        // must not keep the role attached to a neutralised row.
        await _db.UserRoles.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.UserClaims.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.UserLogins.Where(l => l.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.UserTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
    }

    // --- Side effects ----------------------------------------------------------

    /// <summary>
    /// Tell the member it happened, at the address that is about to stop existing (FR-040).
    /// </summary>
    /// <remarks>
    /// A send failure is logged and swallowed. <b>It must never roll back the erasure</b>: the member
    /// asked to be erased, and failing to email them is not a reason to keep their data. This is the
    /// judgement Principle VII requires be written down where it is made.
    /// </remarks>
    private async Task SendFarewellAsync(User user, CancellationToken ct)
    {
        try
        {
            await _email.SendAccountDeletedNotificationAsync(user, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account-deleted notification could not be sent; erasure continues");
        }
    }

    /// <summary>
    /// Reclaim the avatar's stored object after commit (FR-015). Feature 035 put the bytes in blob
    /// storage, so the cascade deleted a descriptor and not an image.
    /// </summary>
    private async Task ReclaimAvatarObjectAsync(string? objectKey, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        try
        {
            await _media.DeleteAsync(objectKey, ct);
        }
        catch (Exception ex)
        {
            // The account is already gone and cannot be brought back, so this cannot fail the request.
            // Logged at error because it leaves a real orphan — reclaimed by 035's reconciliation
            // sweep, but somebody should know it happened.
            _logger.LogError(ex,
                "Avatar object could not be reclaimed after erasing {UserId}; left for reconciliation", userId);
        }
    }
}
