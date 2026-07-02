namespace JuggerHub.Entities;

/// <summary>How a team is organised. A <see cref="CityTeam"/> has a home city; a
/// <see cref="Mixteam"/> is players from different cities who enter tournaments as one
/// crew and has no city. Serialized as its name (global JsonStringEnumConverter).</summary>
public enum TeamType
{
    CityTeam = 0,
    Mixteam = 1,
}

/// <summary>A member's role within a single team. A team always keeps ≥ 1 <see cref="Admin"/>
/// (last-admin guard). Serialized as its name.</summary>
public enum TeamRole
{
    Member = 0,
    Admin = 1,
}

/// <summary>Whether an invitation is a shared, reusable link or a targeted invite bound to
/// one user (delivered by email). Serialized as its name.</summary>
public enum InvitationKind
{
    Link = 0,
    Targeted = 1,
}

/// <summary>Lifecycle of an invitation. <c>Expired</c> is NOT stored — it is derived from
/// <see cref="TeamInvitation.ExpiresDate"/>. Serialized as its name.</summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3,
}
