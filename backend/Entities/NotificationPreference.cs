namespace JuggerHub.Entities;

/// <summary>
/// A single user's setting for one (category, channel) cell of the notification matrix (feature
/// 011). Sparse: a row exists only for a cell the user has explicitly set — the absence of a row
/// means the opt-out default, <c>enabled</c>. This keeps the model extensible (new categories or
/// channels add possible cells without migrating anyone) and makes "everything on by default" free.
/// Owned strictly by <see cref="UserId"/> and removed with the account.
/// </summary>
public sealed class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }

    public NotificationCategory Category { get; set; }

    public NotificationChannel Channel { get; set; }

    /// <summary>The value the user set for this cell. Missing row ⇒ treated as <c>true</c> (default on).</summary>
    public bool Enabled { get; set; }

    public User User { get; set; } = null!;
}
