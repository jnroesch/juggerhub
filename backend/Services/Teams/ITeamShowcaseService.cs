using JuggerHub.Dtos.Profile;
using JuggerHub.Services.Media;

namespace JuggerHub.Services.Teams;

/// <summary>
/// A team's showcase gallery (feature 046 / #99): list, add, caption, remove, reorder — plus the
/// gated byte read. The same five operations as the profile gallery, with two differences that
/// follow from who owns the pictures.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reads require a signed-in caller; writes require a team admin.</b> There is no anonymous team
/// surface at all (feature 026), and the gallery does not widen for members nor narrow for signed-in
/// non-members — it is what the team presents to the platform. Writing is an admin capability, like
/// every other thing a team presents (news, settings, invitations).
/// </para>
/// <para>
/// <b>No account-standing rule applies.</b> A team's gallery belongs to the team, not to whoever
/// uploaded a picture, so it does not disappear because a member was banned. The profile gallery is
/// the one that inherits its owner's standing.
/// </para>
/// </remarks>
public interface ITeamShowcaseService
{
    /// <summary>
    /// The team's gallery, ordered, or null when the slug is unknown. At most five items, so no
    /// pagination. Visible to any signed-in caller.
    /// </summary>
    Task<IReadOnlyList<ShowcaseImageDto>?> ListAsync(string slug, Guid viewerUserId, CancellationToken ct = default);

    /// <summary>
    /// The bytes of one of the team's pictures, or null when it does not exist or the stored object
    /// cannot be read — deliberately indistinguishable (spec FR-023).
    /// </summary>
    Task<MediaContent?> GetImageAsync(string slug, Guid imageId, Guid viewerUserId, CancellationToken ct = default);

    /// <summary>Add a picture to the team's gallery. Admin-only; refuses at the five-image cap.</summary>
    Task<ShowcaseAddResult> AddAsync(string slug, Guid actorUserId, byte[] content, CancellationToken ct = default);

    /// <summary>Set or clear a caption. Admin-only.</summary>
    Task<ShowcaseMutateStatus> SetCaptionAsync(
        string slug, Guid actorUserId, Guid imageId, string? caption, CancellationToken ct = default);

    /// <summary>Remove one picture and its stored object. Admin-only.</summary>
    Task<ShowcaseMutateStatus> RemoveAsync(string slug, Guid actorUserId, Guid imageId, CancellationToken ct = default);

    /// <summary>Apply a complete new order, all-or-nothing. Admin-only.</summary>
    Task<ShowcaseMutateStatus> ReorderAsync(
        string slug, Guid actorUserId, IReadOnlyList<Guid> imageIds, CancellationToken ct = default);

    /// <summary>
    /// The object keys of a team's pictures, for a caller about to delete the team.
    /// </summary>
    /// <remarks>
    /// Exists because <c>TeamService.DeleteAsync</c> hands the delete to PostgreSQL's cascade, which
    /// removes the descriptor rows with no application code running: the keys have to be read before
    /// the delete or they are unrecoverable, and the pictures then survive until an operator runs a
    /// reconciliation sweep.
    /// </remarks>
    Task<IReadOnlyList<string>> ObjectKeysForTeamAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>Best-effort delete of stored objects after their rows are gone. Never throws.</summary>
    Task ReclaimObjectsAsync(IReadOnlyList<string> objectKeys, CancellationToken ct = default);
}
