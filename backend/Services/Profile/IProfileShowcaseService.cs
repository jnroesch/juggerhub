using JuggerHub.Dtos.Profile;
using JuggerHub.Services.Media;

namespace JuggerHub.Services.Profile;

/// <summary>
/// A player's showcase gallery (feature 046 / #99): list, add, caption, remove, reorder — plus the
/// gated byte read. Accesses EF Core directly (no repository layer) and returns DTOs; the controller
/// never sees an entity, and never sees an object key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reads apply the feature-026 visibility rule and the banned-account filter, in that order,
/// before the media store is touched.</b> That ordering is the security contract inherited from
/// feature 035: authorization is decided against relational data, never delegated to a storage
/// system that cannot express it.
/// </para>
/// <para>
/// <b>Writes are owner-only.</b> Every write takes the authenticated subject's own user id — there
/// is no "act on behalf of" parameter, so acting on someone else's gallery is not expressible.
/// </para>
/// </remarks>
public interface IProfileShowcaseService
{
    /// <summary>
    /// The gallery for a handle, ordered, or null when the handle is unknown, the owner is banned,
    /// or the profile is hidden from this viewer. At most five items, so no pagination.
    /// </summary>
    Task<IReadOnlyList<ShowcaseImageDto>?> ListAsync(string handle, Guid? viewerUserId, CancellationToken ct = default);

    /// <summary>
    /// The bytes for one showcase image, or null when it does not exist, the viewer is not entitled
    /// to it, or the stored object cannot be read. The three cases are deliberately
    /// indistinguishable to the caller (spec FR-023).
    /// </summary>
    Task<MediaContent?> GetImageAsync(string handle, Guid imageId, Guid? viewerUserId, CancellationToken ct = default);

    /// <summary>Add a picture to the caller's own gallery, refusing at the five-image cap.</summary>
    Task<ShowcaseAddResult> AddAsync(Guid userId, byte[] content, CancellationToken ct = default);

    /// <summary>Set or clear a caption on one of the caller's own pictures.</summary>
    Task<ShowcaseMutateStatus> SetCaptionAsync(Guid userId, Guid imageId, string? caption, CancellationToken ct = default);

    /// <summary>Remove one of the caller's own pictures, and its stored object with it.</summary>
    Task<ShowcaseMutateStatus> RemoveAsync(Guid userId, Guid imageId, CancellationToken ct = default);

    /// <summary>Apply a complete new order to the caller's own gallery, all-or-nothing.</summary>
    Task<ShowcaseMutateStatus> ReorderAsync(Guid userId, IReadOnlyList<Guid> imageIds, CancellationToken ct = default);
}
