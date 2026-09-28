using JuggerHub.Common;
using JuggerHub.Dtos.Parties;

namespace JuggerHub.Services.Parties;

/// <summary>Outcome of an attempt to edit a party news post (feature 059).</summary>
public enum PartyNewsEditStatus
{
    /// <summary>Saved — or the text was already the same, in which case nothing was written.</summary>
    Updated,
    /// <summary>No such party, or the caller is not in its crew: the same answer reading gives.</summary>
    PartyNotFound,
    Forbidden,
    /// <summary>No such post in this party: deleted, never existed, or another party's.</summary>
    PostNotFound,
}

/// <summary>The post as it now stands, plus the outcome.</summary>
public sealed record PartyNewsEditResult(PartyNewsEditStatus Status, PartyNewsDto? Post);

/// <summary>Outcome of an attempt to delete a party news post (feature 059).</summary>
public enum PartyNewsDeleteStatus
{
    Deleted,
    /// <summary>No such party, or the caller is not in its crew: the same answer reading gives.</summary>
    PartyNotFound,
    Forbidden,
    /// <summary>No such post in this party: already deleted, never existed, or another party's.</summary>
    PostNotFound,
}

/// <summary>
/// Private party news feed (feature 016): list (crew-only) and create (party-admin, notifies the
/// crew in-app + email, mirroring team news). Feature 059 adds edit and delete, open to any current
/// party admin for any post. These have their own statuses rather than <see cref="PartyOutcome"/>,
/// which the market shares and whose controllers turn an unmapped member into a 400.
/// </summary>
public interface IPartyNewsService
{
    /// <summary>The party news feed (crew-only); null when the caller is not in the crew.</summary>
    Task<PagedResult<PartyNewsDto>?> ListAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Post a party news update (party-admin); notifies the crew.</summary>
    Task<PartyResult<PartyNewsDto>> CreateAsync(Guid partyId, string body, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Replace a post's text (any current party admin, any post). The post keeps its author, date and
    /// place and is marked edited. Nobody is notified, and the alerts already delivered for it are
    /// left exactly as they are: they quote none of its text. Text equal to the current text (after
    /// trimming) writes nothing.
    /// </summary>
    Task<PartyNewsEditResult> EditAsync(Guid partyId, Guid postId, Guid actorUserId, string body, CancellationToken ct = default);

    /// <summary>
    /// Delete a post for good (any current party admin, any post), together with the alerts that
    /// announced it — everyone's it reached, people who have since left the crew included. Nobody is
    /// notified; copies already sent by email are out of reach.
    /// </summary>
    Task<PartyNewsDeleteStatus> DeleteAsync(Guid partyId, Guid postId, Guid actorUserId, CancellationToken ct = default);
}
