namespace JuggerHub.Services.Recognition;

/// <summary>Result of an attempt to grant a badge/achievement; maps to an HTTP status in the controller.</summary>
public enum GrantOutcome
{
    Granted,
    DefinitionNotFound,
    DefinitionRetired,
    SubjectNotFound,
    SubjectTypeMismatch,
    Duplicate,
}

/// <summary>Result of a revoke attempt.</summary>
public enum RevokeOutcome
{
    Revoked,
    NotFound,
}

/// <summary>Result of an icon upload.</summary>
public enum IconOutcome
{
    Stored,
    Empty,
    TooLarge,
    InvalidType,
    DefinitionNotFound,

    /// <summary>Image pixel dimensions exceed the decode safety limit (#101, mirrors avatars).</summary>
    DimensionsTooLarge,

    /// <summary>Image bytes could not be decoded (corrupt/truncated) (#101, mirrors avatars).</summary>
    Unreadable,
}

/// <summary>
/// Result of an icon upload, carrying the processor's non-technical reason for non-success
/// outcomes (#101) so the admin sees *why* an upload was rejected — same shape as the avatar
/// path's <c>AvatarSetResult</c>.
/// </summary>
public sealed record IconSetResult(IconOutcome Outcome, string? Reason)
{
    public static IconSetResult Stored() => new(IconOutcome.Stored, null);

    public static IconSetResult Fail(IconOutcome outcome, string reason) => new(outcome, reason);
}
