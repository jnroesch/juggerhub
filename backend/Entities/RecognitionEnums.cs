namespace JuggerHub.Entities;

/// <summary>
/// How an award came to be. v1 only ever writes <see cref="Manual"/> (an admin grants it);
/// <see cref="Automatic"/> is reserved so criteria-based awarding (deferred US3) can be added
/// later without reworking earned-award history. Serialized by name (global
/// <c>JsonStringEnumConverter</c>).
/// </summary>
public enum AwardSource
{
    Manual = 0,
    Automatic = 1,
}

/// <summary>Lifecycle of an award. Revoked awards are retained for audit but never displayed.</summary>
public enum AwardStatus
{
    Active = 0,
    Revoked = 1,
}

/// <summary>
/// The kind of subject an award targets or a definition applies to. Storage uses two nullable
/// FKs (<c>PlayerProfileId</c>/<c>TeamId</c>); this enum is used in DTOs and validation.
/// </summary>
public enum SubjectType
{
    Player = 0,
    Team = 1,
}
