namespace JuggerHub.Entities;

/// <summary>What kind of event this is. <see cref="Other"/> carries a free-text
/// <c>CustomTypeLabel</c>. Serialized as its name (global JsonStringEnumConverter).</summary>
public enum EventType
{
    Tournament = 0,
    Workshop = 1,
    Other = 2,
}

/// <summary>Where an event happens. <see cref="InPerson"/> needs a full address (incl. country);
/// <see cref="Virtual"/> needs a link. Serialized as its name.</summary>
public enum LocationKind
{
    InPerson = 0,
    Virtual = 1,
}

/// <summary>Who may sign up. An event is either <see cref="Teams"/>-only or
/// <see cref="Individuals"/>-only, chosen at creation. Serialized as its name.</summary>
public enum ParticipantMode
{
    Teams = 0,
    Individuals = 1,
}

/// <summary>Lifecycle of an event. <see cref="Cancelled"/> is terminal (irreversible).
/// Serialized as its name.</summary>
public enum EventStatus
{
    Published = 0,
    Cancelled = 1,
}

/// <summary>A sign-up's state. Occupied spots = <see cref="Joined"/> + <see cref="AwaitingApproval"/>;
/// <see cref="Waitlisted"/> never counts toward the limit. Serialized as its name.</summary>
public enum SignupStatus
{
    Joined = 0,
    AwaitingApproval = 1,
    Waitlisted = 2,
}

// InvitationKind and InvitationStatus (co-admin invites) are reused from TeamEnums.cs.
