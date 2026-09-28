using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Teams;

/// <summary>
/// EF-Core-direct implementation of <see cref="ITeamPollService"/> (feature 062). See
/// <c>specs/062-team-polls/research.md</c> for why it is shaped the way it is; the short version:
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Open</b> and <b>who counts</b> are derived when a poll is read — from its times
/// (<see cref="TeamPollOpen"/>) and from the team's current membership — so nothing ever sweeps or
/// rewrites (R4, R5).</item>
/// <item><b>Who chose what</b> leaves this class in exactly two places, both in
/// <see cref="BuildAsync"/>, each behind one condition (R6). Nothing else may add a third.</item>
/// <item><b>Every write to a poll's answers or content runs under a lock on the poll row</b>, which is
/// what keeps one answer per member, and an answer from ever naming an option an edit removed (R3).</item>
/// </list>
/// </remarks>
public sealed class TeamPollService : ITeamPollService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;
    private readonly INotificationService _notifications;
    private readonly INotificationPreferenceService _preferences;
    private readonly TeamEmailService _email;
    private readonly ILogger<TeamPollService> _logger;

    public TeamPollService(
        AppDbContext db,
        TeamMembershipGuard guard,
        INotificationService notifications,
        INotificationPreferenceService preferences,
        TeamEmailService email,
        ILogger<TeamPollService> logger)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
        _preferences = preferences;
        _email = email;
        _logger = logger;
    }

    // --- Read ----------------------------------------------------------------------------------------

    public async Task<PagedResult<TeamPollDto>?> ListAsync(
        string slug, Guid userId, TeamPollState state, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var query = _db.TeamPolls.AsNoTracking().Where(p => p.TeamId == a.TeamId);
        query = state == TeamPollState.Open
            ? query.Where(TeamPollOpen.At(now))
            : query.Where(TeamPollOpen.ClosedAt(now));

        var total = await query.CountAsync(ct);

        // Open: newest first. Closed: most recently closed first — early close or its time passing.
        var ordered = state == TeamPollState.Open
            ? query.OrderByDescending(p => p.CreatedDate)
            : query.OrderByDescending(p => p.ClosedAt ?? p.ClosesAt).ThenByDescending(p => p.CreatedDate);

        var ids = await ordered
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var items = await BuildAsync(ids, a, userId, now, ct);
        return new PagedResult<TeamPollDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // --- Start ---------------------------------------------------------------------------------------

    public async Task<TeamPollResult> CreateAsync(
        string slug, Guid userId, CreateTeamPollRequest request, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamPollResult.Of(TeamPollStatus.TeamNotFound);
        }

        // Asking the whole team is an admin's call, like posting news (spec FR-006).
        if (!a.IsAdmin)
        {
            return TeamPollResult.Of(TeamPollStatus.Forbidden);
        }

        var now = DateTime.UtcNow;
        var (content, problem) = TeamPollRules.ValidateContent(request.Question, request.Options, request.ClosesAt, now);
        if (problem is not null)
        {
            return TeamPollResult.Refused(TeamPollStatus.Invalid, problem);
        }

        // The cap is a count-then-insert, so two admins at nine open polls would both see nine and the
        // team would end with eleven. The team row lock serialises them (research R3) — the TeamService
        // idiom. Everything that mutates is created inside the delegate, so a replay re-creates it.
        var strategy = _db.Database.CreateExecutionStrategy();
        var pollId = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Teams\" WHERE \"Id\" = {a.TeamId} FOR UPDATE", ct);

            var open = await _db.TeamPolls.Where(p => p.TeamId == a.TeamId).Where(TeamPollOpen.At(now)).CountAsync(ct);
            if (open >= TeamPollRules.MaxOpenPolls)
            {
                return (Guid?)null;
            }

            var poll = new TeamPoll
            {
                TeamId = a.TeamId,
                AuthorUserId = userId,
                Question = content!.Question,
                AllowsMultiple = request.AllowsMultiple,
                IsAnonymous = request.IsAnonymous,
                ResultsAfterAnswer = request.ResultsAfterAnswer,
                ClosesAt = content.ClosesAtUtc,
            };
            for (var i = 0; i < content.Options.Count; i++)
            {
                poll.Options.Add(new TeamPollOption { Text = content.Options[i], Position = i });
            }

            _db.TeamPolls.Add(poll);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return poll.Id;
        });

        if (pollId is not { } id)
        {
            return TeamPollResult.Refused(TeamPollStatus.Conflict, new TeamPollProblem(
                TeamPollCode.TooManyOpen, null,
                $"The team already has {TeamPollRules.MaxOpenPolls} open polls. Close or delete one first."));
        }

        // Only now that the poll is committed, and never able to fail the request (spec FR-026).
        await NotifyTeamAsync(a.TeamId, id, userId, content!.Question, ct);

        var built = await BuildAsync([id], a, userId, DateTime.UtcNow, ct);
        return new TeamPollResult(TeamPollStatus.Created, built.Single());
    }

    /// <summary>
    /// Tell every current member but the author that a poll opened (spec FR-026 – FR-029): an Alerts row
    /// and a device notice through the engine, which reads each member's <i>Team news</i> setting per
    /// channel, and an email in each recipient's own language where they allow it.
    /// </summary>
    /// <remarks>
    /// Best-effort, the <c>TeamNewsService.PostAsync</c> shape: the poll exists either way, and a failure
    /// here is logged with ids only — never the question.
    /// </remarks>
    private async Task NotifyTeamAsync(Guid teamId, Guid pollId, Guid authorId, string question, CancellationToken ct)
    {
        List<Guid> recipients;
        string teamSlug;
        string teamName;
        try
        {
            var team = await _db.Teams.AsNoTracking()
                .Where(t => t.Id == teamId)
                .Select(t => new { t.Slug, t.Name })
                .FirstAsync(ct);
            teamSlug = team.Slug;
            teamName = team.Name;

            recipients = await _db.TeamMemberships.AsNoTracking()
                .Where(m => m.TeamId == teamId && m.UserId != authorId)
                .Select(m => m.UserId)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve the recipients of poll {PollId}.", pollId);
            return;
        }

        if (recipients.Count == 0)
        {
            return;
        }

        try
        {
            await _notifications.CreateManyAsync(
                recipients,
                NotificationType.TeamPoll,
                new TeamPollPayload(teamSlug, teamName, pollId, question),
                actorUserId: authorId,
                dedupeKeyPrefix: PollDedupePrefix(pollId),
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to alert the members about poll {PollId}.", pollId);
        }

        try
        {
            var emailRecipients = await _preferences.GetEnabledRecipientsAsync(
                recipients, NotificationCategory.TeamNews, NotificationChannel.Email, ct);
            if (emailRecipients.Count == 0)
            {
                return;
            }

            // Read through the profile set, whose ban filter drops a banned member: they cannot sign in
            // to answer, so an email would only be noise.
            var addressees = await _db.PlayerProfiles.AsNoTracking()
                .Where(p => emailRecipients.Contains(p.UserId) && p.User.Email != null)
                .Select(p => new { Email = p.User.Email!, p.User.PreferredLanguage, p.DisplayName })
                .ToListAsync(ct);

            foreach (var addressee in addressees)
            {
                // The recipient's language, not the author's (the 058 pattern): the email is addressed
                // to them.
                await _email.SendTeamPollEmailAsync(
                    addressee.Email, addressee.DisplayName, teamName, teamSlug, pollId, question,
                    SupportedLanguages.ResolveOrDefault(addressee.PreferredLanguage), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to email the members about poll {PollId}.", pollId);
        }
    }

    // --- Answer --------------------------------------------------------------------------------------

    public Task<TeamPollResult> AnswerAsync(
        string slug, Guid pollId, Guid userId, IReadOnlyList<Guid> optionIds, CancellationToken ct = default) =>
        WriteAnswerAsync(slug, pollId, userId, optionIds.Distinct().ToList(), ct);

    public Task<TeamPollResult> WithdrawAsync(string slug, Guid pollId, Guid userId, CancellationToken ct = default) =>
        WriteAnswerAsync(slug, pollId, userId, chosen: null, ct);

    /// <summary>
    /// Replace the caller's answer with <paramref name="chosen"/>, or remove it when null.
    /// </summary>
    /// <remarks>
    /// Runs under a lock on the poll row (research R3). That lock is what makes "one answer per member"
    /// true — no index can say it (R2) — and what stops an answer from landing after the poll closed or
    /// on an option an edit has just replaced: close and edit take the same row first.
    /// </remarks>
    private async Task<TeamPollResult> WriteAnswerAsync(
        string slug, Guid pollId, Guid userId, List<Guid>? chosen, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamPollResult.Of(TeamPollStatus.TeamNotFound);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        var outcome = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            if (await LockPollAsync(pollId, a.TeamId, ct) is not { } poll)
            {
                return TeamPollResult.Of(TeamPollStatus.PollNotFound);
            }

            // Decided by the moment the lock is held: an answer arriving after the close time is
            // refused, even if the member's screen still showed the poll open (spec edge cases).
            if (!TeamPollOpen.IsOpen(poll.ClosedAt, poll.ClosesAt, DateTime.UtcNow))
            {
                return ClosedResult();
            }

            if (chosen is not null
                && TeamPollRules.ValidateChoice(poll.AllowsMultiple, chosen, poll.OptionIds) is { } problem)
            {
                return TeamPollResult.Refused(TeamPollStatus.Invalid, problem);
            }

            await _db.TeamPollVotes
                .Where(v => v.PollId == pollId && v.UserId == userId)
                .ExecuteDeleteAsync(ct);

            if (chosen is not null)
            {
                _db.TeamPollVotes.AddRange(chosen.Select(optionId => new TeamPollVote
                {
                    PollId = pollId,
                    OptionId = optionId,
                    UserId = userId,
                }));
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return TeamPollResult.Of(TeamPollStatus.Ok);
        });

        return outcome.Status == TeamPollStatus.Ok ? await ViewAsync(pollId, a, userId, ct) : outcome;
    }

    // --- Change, close, delete (user stories 4 and 5) ----------------------------------------------

    public async Task<TeamPollResult> UpdateAsync(
        string slug, Guid pollId, Guid userId, UpdateTeamPollRequest request, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamPollResult.Of(TeamPollStatus.TeamNotFound);
        }

        // Any current admin, any poll, whoever started it (spec FR-025; feature 057's rule for news).
        if (!a.IsAdmin)
        {
            return TeamPollResult.Of(TeamPollStatus.Forbidden);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        var outcome = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // The same lock an answer takes: "nobody has answered yet" and the option replacement below
            // must see committed truth, so an answer can never name an option this edit removes (FR-023).
            if (await LockPollAsync(pollId, a.TeamId, ct) is not { } poll)
            {
                return TeamPollResult.Of(TeamPollStatus.PollNotFound);
            }

            var now = DateTime.UtcNow;
            if (!TeamPollOpen.IsOpen(poll.ClosedAt, poll.ClosesAt, now))
            {
                return ClosedResult();
            }

            var (content, problem) = TeamPollRules.ValidateContent(request.Question, request.Options, request.ClosesAt, now);
            if (problem is not null)
            {
                return TeamPollResult.Refused(TeamPollStatus.Invalid, problem);
            }

            var optionsChanged = !poll.Options.Select(o => o.Text).SequenceEqual(content!.Options, StringComparer.Ordinal);
            var questionChanged = !string.Equals(poll.Question, content.Question, StringComparison.Ordinal);
            var contentChanged = optionsChanged
                || questionChanged
                || poll.AllowsMultiple != request.AllowsMultiple
                || poll.ResultsAfterAnswer != request.ResultsAfterAnswer;

            // Once anyone has answered, what they answered must not change underneath them. Nothing is
            // written — not even a close time sent alongside — so the refusal is all-or-nothing.
            if (contentChanged && poll.HasAnswers)
            {
                return TeamPollResult.Refused(TeamPollStatus.Conflict, new TeamPollProblem(
                    TeamPollCode.Answered, null,
                    "Somebody has answered, so the question and options can no longer change."));
            }

            // Only when they actually changed. Replacing unchanged options would give them new ids and
            // cascade-delete every answer — which is exactly what moving the close time of an answered
            // poll would otherwise do.
            if (optionsChanged)
            {
                await _db.TeamPollOptions.Where(o => o.PollId == pollId).ExecuteDeleteAsync(ct);
                _db.TeamPollOptions.AddRange(content.Options.Select((text, i) => new TeamPollOption
                {
                    PollId = pollId,
                    Text = text,
                    Position = i,
                }));
                await _db.SaveChangesAsync(ct);
            }

            // ExecuteUpdate bypasses the audit interceptor, so ModifiedDate is set here or nowhere
            // (constitution Principle III). IsAnonymous is deliberately absent: it never changes.
            await _db.TeamPolls
                .Where(p => p.Id == pollId && p.TeamId == a.TeamId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Question, content.Question)
                    .SetProperty(p => p.AllowsMultiple, request.AllowsMultiple)
                    .SetProperty(p => p.ResultsAfterAnswer, request.ResultsAfterAnswer)
                    .SetProperty(p => p.ClosesAt, content.ClosesAtUtc)
                    .SetProperty(p => p.ModifiedDate, now), ct);

            // The alerts that quote the question say the corrected one from now on — silently: not
            // unread again, not moved, nothing re-sent (spec FR-030). Inside the transaction, so the poll
            // and its alerts never disagree. The team's current name is read here, so a rename that
            // committed meanwhile is kept rather than undone.
            if (questionChanged)
            {
                var team = await _db.Teams.AsNoTracking()
                    .Where(t => t.Id == a.TeamId)
                    .Select(t => new { t.Slug, t.Name })
                    .FirstAsync(ct);
                await _notifications.ReplacePayloadAsync(
                    NotificationType.TeamPoll,
                    PollDedupePrefix(pollId),
                    new TeamPollPayload(team.Slug, team.Name, pollId, content.Question),
                    ct);
            }

            await tx.CommitAsync(ct);
            return TeamPollResult.Of(TeamPollStatus.Ok);
        });

        return outcome.Status == TeamPollStatus.Ok ? await ViewAsync(pollId, a, userId, ct) : outcome;
    }

    public async Task<TeamPollResult> CloseAsync(string slug, Guid pollId, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamPollResult.Of(TeamPollStatus.TeamNotFound);
        }

        if (!a.IsAdmin)
        {
            return TeamPollResult.Of(TeamPollStatus.Forbidden);
        }

        // One conditional statement. An UPDATE takes the row lock itself: if an answer holds it, this
        // waits and then re-checks "still open" against the committed row, so an answer either landed
        // before the close or is refused after it (research R3). ModifiedDate by hand — ExecuteUpdate
        // skips the audit interceptor.
        var now = DateTime.UtcNow;
        var closed = await _db.TeamPolls
            .Where(p => p.Id == pollId && p.TeamId == a.TeamId)
            .Where(TeamPollOpen.At(now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ClosedAt, now)
                .SetProperty(p => p.ModifiedDate, now), ct);

        if (closed == 0)
        {
            var exists = await _db.TeamPolls.AsNoTracking().AnyAsync(p => p.Id == pollId && p.TeamId == a.TeamId, ct);
            return exists ? ClosedResult() : TeamPollResult.Of(TeamPollStatus.PollNotFound);
        }

        return await ViewAsync(pollId, a, userId, ct);
    }

    public async Task<TeamPollStatus> DeleteAsync(string slug, Guid pollId, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamPollStatus.TeamNotFound;
        }

        if (!a.IsAdmin)
        {
            return TeamPollStatus.Forbidden;
        }

        // The poll and its alerts go together or not at all (spec FR-024; feature 057's shape). Deleted
        // separately, a failure in between would leave the question quoted in every inbox with no way
        // back: deleting again answers "not found". Both statements are idempotent, so a replay
        // converges. Options and answers cascade with the poll.
        var strategy = _db.Database.CreateExecutionStrategy();
        var (deleted, unreadRecipients) = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var rows = await _db.TeamPolls
                .Where(p => p.Id == pollId && p.TeamId == a.TeamId)
                .ExecuteDeleteAsync(ct);
            if (rows == 0)
            {
                // Never reach the alerts for a poll that is not this team's: this early return is what
                // keeps another team's poll id away from that team's rows.
                return (false, (IReadOnlyCollection<Guid>)[]);
            }

            // Found by the prefix every row was written under — so a member who has since left loses
            // the row too, which a roster lookup would miss (feature 057).
            var recipients = await _notifications.DeleteManyAsync(NotificationType.TeamPoll, PollDedupePrefix(pollId), ct);
            await tx.CommitAsync(ct);
            return (true, recipients);
        });

        if (!deleted)
        {
            return TeamPollStatus.PollNotFound;
        }

        // Only after the commit: a badge lowered before it could be contradicted by a rollback.
        await _notifications.RefreshUnreadBadgesAsync(unreadRecipients, ct);
        return TeamPollStatus.Ok;
    }

    // --- The one place a poll is shaped for a caller ------------------------------------------------

    /// <summary>
    /// Build polls for one caller, in the order of <paramref name="pollIds"/> (research R5/R6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three reads: the polls with their options, the team's current members with their names, and
    /// every vote for these polls. Everything else — counts, who answered, who has not — is worked out
    /// here, from those, so one definition of "counts" serves all of it: <b>a vote counts only while
    /// its voter is a current, non-banned member</b>. Banned players drop out of the member list
    /// because it is read through <c>PlayerProfiles</c>, whose ban filter hides them; an erased
    /// account has no membership left.
    /// </para>
    /// <para>
    /// <b>Who chose what enters the result in exactly two places</b>, <see cref="VotersVisible"/> and
    /// <see cref="NotAnsweredVisible"/>. In an anonymous poll neither is ever true, for any caller,
    /// and the voters' ids never leave this method (spec FR-018). A third place is a privacy defect.
    /// </para>
    /// </remarks>
    private async Task<List<TeamPollDto>> BuildAsync(
        IReadOnlyList<Guid> pollIds, TeamAccess access, Guid viewerId, DateTime now, CancellationToken ct)
    {
        if (pollIds.Count == 0)
        {
            return [];
        }

        var polls = await _db.TeamPolls.AsNoTracking()
            .Where(p => pollIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Question,
                p.AllowsMultiple,
                p.IsAnonymous,
                p.ResultsAfterAnswer,
                p.CreatedDate,
                p.ClosesAt,
                p.ClosedAt,
                // Through the profile set, never p.Author.Profile: the ban filter makes that navigation
                // misbehave (feature 044). Null once the author is banned or erased; the client names
                // them with the former-player placeholder.
                AuthorName = _db.PlayerProfiles.Where(pp => pp.UserId == p.AuthorUserId).Select(pp => pp.DisplayName).FirstOrDefault(),
                AuthorHandle = _db.PlayerProfiles.Where(pp => pp.UserId == p.AuthorUserId).Select(pp => pp.Handle).FirstOrDefault(),
                Options = p.Options.OrderBy(o => o.Position).Select(o => new { o.Id, o.Text }).ToList(),
            })
            .ToListAsync(ct);

        // The counted set, in roster order (admins first, then by joining), which is also the order the
        // not-answered list is shown in.
        var members = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == access.TeamId)
            .Join(_db.PlayerProfiles, m => m.UserId, p => p.UserId,
                (m, p) => new { m.UserId, m.Role, m.JoinedDate, p.DisplayName, p.Handle })
            .OrderByDescending(x => x.Role)
            .ThenBy(x => x.JoinedDate)
            .ToListAsync(ct);
        var memberIds = members.Select(m => m.UserId).ToHashSet();

        var votes = await _db.TeamPollVotes.AsNoTracking()
            .Where(v => pollIds.Contains(v.PollId))
            .Select(v => new { v.PollId, v.OptionId, v.UserId })
            .ToListAsync(ct);

        var byId = polls.ToDictionary(p => p.Id);
        var result = new List<TeamPollDto>(pollIds.Count);
        foreach (var id in pollIds)
        {
            if (!byId.TryGetValue(id, out var p))
            {
                continue;
            }

            var stored = votes.Where(v => v.PollId == id).ToList();
            var counted = stored.Where(v => memberIds.Contains(v.UserId)).ToList();
            var answered = counted.Select(v => v.UserId).ToHashSet();
            var mine = counted.Where(v => v.UserId == viewerId).Select(v => v.OptionId).ToList();

            var isOpen = TeamPollOpen.IsOpen(p.ClosedAt, p.ClosesAt, now);

            // Hidden until you answer applies to everyone, the author and admins included (clarified):
            // there is no bypass here, and nothing downstream may add one.
            var resultsVisible = !p.ResultsAfterAnswer || !isOpen || mine.Count > 0;

            var options = p.Options.Select(o => new TeamPollOptionDto(
                o.Id,
                o.Text,
                resultsVisible ? counted.Count(v => v.OptionId == o.Id) : null,
                VotersVisible(p.IsAnonymous, resultsVisible)
                    ? members
                        .Where(m => counted.Any(v => v.OptionId == o.Id && v.UserId == m.UserId))
                        .Select(m => new TeamPollPersonDto(m.DisplayName, m.Handle))
                        .ToList()
                    : null)).ToList();

            var notAnswered = NotAnsweredVisible(p.IsAnonymous, access)
                ? members
                    .Where(m => !answered.Contains(m.UserId))
                    .Select(m => new TeamPollPersonDto(m.DisplayName, m.Handle))
                    .ToList()
                : null;

            result.Add(new TeamPollDto(
                p.Id,
                p.Question,
                p.AllowsMultiple,
                p.IsAnonymous,
                p.ResultsAfterAnswer,
                p.CreatedDate,
                p.ClosesAt,
                isOpen ? null : p.ClosedAt ?? p.ClosesAt,
                isOpen,
                p.AuthorName,
                p.AuthorHandle,
                MemberCount: members.Count,
                AnsweredCount: answered.Count,
                ResultsVisible: resultsVisible,
                // Any stored answer, a former member's included: it is what locks the content (FR-023).
                HasAnswers: stored.Count > 0,
                MyOptionIds: mine,
                Options: options,
                NotAnswered: notAnswered));
        }

        return result;
    }

    /// <summary>
    /// Who chose an option is shown only in a named poll whose result the caller may see (spec FR-014,
    /// FR-018). One of the only two places an answer's author can reach a response.
    /// </summary>
    private static bool VotersVisible(bool isAnonymous, bool resultsVisible) => !isAnonymous && resultsVisible;

    /// <summary>
    /// Who has not answered is shown only to the team's current admins, and only in a named poll
    /// (spec FR-014a, FR-018): in an anonymous poll, whether someone voted can reveal too much. The other
    /// of the only two places.
    /// </summary>
    private static bool NotAnsweredVisible(bool isAnonymous, TeamAccess access) => !isAnonymous && access.IsAdmin;

    // --- Helpers -------------------------------------------------------------------------------------

    /// <summary>The poll as the caller now sees it, after a change.</summary>
    private async Task<TeamPollResult> ViewAsync(Guid pollId, TeamAccess access, Guid userId, CancellationToken ct)
    {
        var built = await BuildAsync([pollId], access, userId, DateTime.UtcNow, ct);
        return built.Count == 0
            ? TeamPollResult.Of(TeamPollStatus.PollNotFound)
            : new TeamPollResult(TeamPollStatus.Ok, built[0]);
    }

    /// <summary>What a locked poll row looks like to the writers that hold the lock.</summary>
    private sealed record LockedPoll(
        string Question,
        bool AllowsMultiple,
        bool ResultsAfterAnswer,
        DateTime? ClosesAt,
        DateTime? ClosedAt,
        IReadOnlyList<(Guid Id, string Text)> Options,
        bool HasAnswers)
    {
        public IReadOnlySet<Guid> OptionIds { get; } = Options.Select(o => o.Id).ToHashSet();
    }

    /// <summary>
    /// Lock the poll row for the rest of the transaction, then read it. <b>The lock statement returns no
    /// data</b>, so the read has to come after it, inside the same transaction, or it would see a state
    /// another writer is about to change. Scoped to the team the slug resolved to, so another team's
    /// poll id is simply absent here.
    /// </summary>
    private async Task<LockedPoll?> LockPollAsync(Guid pollId, Guid teamId, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"TeamPolls\" WHERE \"Id\" = {pollId} AND \"TeamId\" = {teamId} FOR UPDATE", ct);

        var row = await _db.TeamPolls.AsNoTracking()
            .Where(p => p.Id == pollId && p.TeamId == teamId)
            .Select(p => new
            {
                p.Question,
                p.AllowsMultiple,
                p.ResultsAfterAnswer,
                p.ClosesAt,
                p.ClosedAt,
                Options = p.Options.OrderBy(o => o.Position).Select(o => new { o.Id, o.Text }).ToList(),
                HasAnswers = p.Votes.Any(),
            })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new LockedPoll(
                row.Question, row.AllowsMultiple, row.ResultsAfterAnswer, row.ClosesAt, row.ClosedAt,
                row.Options.Select(o => (o.Id, o.Text)).ToList(), row.HasAnswers);
    }

    private static TeamPollResult ClosedResult() =>
        TeamPollResult.Refused(TeamPollStatus.Conflict, new TeamPollProblem(
            TeamPollCode.Closed, null, "This poll has closed."));

    /// <summary>
    /// The dedupe-key prefix a poll's alerts are written under. Starting a poll creates them under it,
    /// and editing and deleting find them by it — every recipient's, former members' included — so it
    /// is spelled once.
    /// </summary>
    private static string PollDedupePrefix(Guid pollId) => $"poll:{pollId}";
}
