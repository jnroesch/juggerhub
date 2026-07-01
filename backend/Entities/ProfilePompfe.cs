namespace JuggerHub.Entities;

/// <summary>
/// One favorite pompfe/position selected by a profile. The set is modeled as rows
/// with a unique (ProfileId, Pompfe) constraint so a profile can have zero or more
/// distinct selections (see specs/003-profile/data-model.md).
/// </summary>
public sealed class ProfilePompfe : BaseEntity
{
    public Guid ProfileId { get; set; }

    public Pompfe Pompfe { get; set; }

    public PlayerProfile Profile { get; set; } = null!;
}
