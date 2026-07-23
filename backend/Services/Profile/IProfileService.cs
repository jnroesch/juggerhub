using JuggerHub.Dtos.Profile;

namespace JuggerHub.Services.Profile;

/// <summary>Outcome of an avatar upload attempt.</summary>
public enum AvatarSetStatus
{
    Success,
    InvalidType,
    TooLarge,
    Empty,
    ProfileNotFound,
}

/// <summary>Raw avatar bytes for serving.</summary>
public readonly record struct AvatarData(byte[] Bytes, string ContentType);

/// <summary>Registration-time handle outcome (distinguishes malformed from taken).</summary>
public enum HandleCheckStatus
{
    Available,
    Invalid,
    Taken,
}

/// <summary>Outcome of marking a profile's onboarding complete.</summary>
public enum CompleteOnboardingStatus
{
    Completed,
    ProfileNotFound,
}

/// <summary>Result of resolving a handle for registration, incl. its normalized form.</summary>
public readonly record struct HandleCheck(HandleCheckStatus Status, string Normalized, string? Reason);

/// <summary>Result of an avatar upload (carries a reason for non-success outcomes).</summary>
public sealed record AvatarSetResult(AvatarSetStatus Status, string? Reason)
{
    public static AvatarSetResult Ok() => new(AvatarSetStatus.Success, null);
    public static AvatarSetResult Fail(AvatarSetStatus status, string reason) => new(status, reason);
}

/// <summary>
/// Profile domain service: handle checks, owner read/update, avatar set/get, and the
/// public (sensitive-data-free) projection. Accesses EF Core directly (no repository
/// layer) and returns DTOs. The controller never sees entities.
/// </summary>
public interface IProfileService
{
    /// <summary>Format + reserved + uniqueness check for live UX (availability endpoint).</summary>
    Task<HandleAvailabilityDto> CheckHandleAsync(string rawHandle, CancellationToken ct = default);

    /// <summary>Resolve a handle at registration, distinguishing malformed from already-taken.</summary>
    Task<HandleCheck> ResolveHandleForRegistrationAsync(string rawHandle, CancellationToken ct = default);

    /// <summary>The authenticated owner's profile, or null if they have none.</summary>
    Task<OwnerProfileDto?> GetOwnerAsync(Guid userId, CancellationToken ct = default);

    /// <summary>True iff the user's profile has completed onboarding (<c>OnboardingCompletedAt != null</c>); false if no profile.</summary>
    Task<bool> HasCompletedOnboardingAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The user's immutable handle (their profile slug), or null if they have no profile.</summary>
    Task<string?> GetHandleAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Mark the owner's onboarding complete. Idempotent: sets the timestamp only if
    /// currently unset, so the first completion stands and repeats are no-ops.
    /// </summary>
    Task<CompleteOnboardingStatus> CompleteOnboardingAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Update the owner's editable fields + pompfen selection; null if no profile.</summary>
    Task<OwnerProfileDto?> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>
    /// The public, sensitive-data-free profile for a handle, or null if unknown OR hidden from
    /// this viewer. Visibility gate (feature 026): an anonymous caller (<paramref name="viewerUserId"/>
    /// is null) sees the profile only when it is public; an authenticated caller sees any profile.
    /// A private profile therefore returns the SAME null as a missing handle (no existence oracle).
    /// </summary>
    Task<PublicProfileDto?> GetPublicAsync(string handle, Guid? viewerUserId, CancellationToken ct = default);

    /// <summary>The internal profile id for a handle (for activity paging), or null if unknown OR
    /// hidden from this viewer (same visibility gate as <see cref="GetPublicAsync"/>).</summary>
    Task<Guid?> GetProfileIdAsync(string handle, Guid? viewerUserId, CancellationToken ct = default);

    /// <summary>Validate (sniff + size) and store the owner's avatar.</summary>
    Task<AvatarSetResult> SetAvatarAsync(Guid userId, byte[] content, string? declaredContentType, CancellationToken ct = default);

    /// <summary>The avatar bytes for a handle, or null if none / unknown handle / hidden from this
    /// viewer (same visibility gate as <see cref="GetPublicAsync"/>).</summary>
    Task<AvatarData?> GetAvatarAsync(string handle, Guid? viewerUserId, CancellationToken ct = default);
}
