namespace JuggerHub.Entities;

/// <summary>
/// Which way a <see cref="MarketRequest"/> points (feature 017). An <see cref="Application"/> is a
/// free agent asking to join a party (user → party); an <see cref="Invite"/> is a party admin asking a
/// player in (party → user). The direction decides who may accept/decline (the recipient) versus
/// revoke (the initiator). Serialized as its name (global JsonStringEnumConverter).
/// </summary>
public enum MarketRequestDirection
{
    Application = 0,
    Invite = 1,
}

/// <summary>
/// Lifecycle of a <see cref="MarketRequest"/> (feature 017). Only <see cref="Pending"/> is actionable;
/// the terminal states are retained so the shared inbox can still show "declined"/"awaiting" and so a
/// declined pair may later be superseded by a fresh pending request while the user stays eligible.
/// <see cref="Revoked"/> covers both an initiator's explicit withdraw and the system auto-cancel of a
/// joiner's other pending requests when they take a crew seat ("one event, one crew"). Serialized as
/// its name.
/// </summary>
public enum MarketRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3,
}
