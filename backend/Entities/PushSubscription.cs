namespace JuggerHub.Entities;

/// <summary>
/// One browser, on one machine, that a member has turned notifications on for (feature 055).
///
/// <para>
/// Modelled on <see cref="RefreshToken"/>, which is the same kind of thing: a per-device credential
/// owned by exactly one account. The differences are deliberate. <see cref="Endpoint"/> is stored
/// verbatim rather than hashed, because unlike a refresh token it must be sent back to the push
/// service to deliver anything. <see cref="P256dh"/> and <see cref="Auth"/> are the device's own
/// public key material, produced by the browser — they are not secrets of ours, and encrypting them
/// at rest would be ceremony given the endpoint beside them cannot be.
/// </para>
///
/// <para>
/// <b>This IS owned data and belongs in <c>AccountDeletionService.EraseOwnedDataAsync</c></b> — the
/// opposite of <see cref="TermsAcceptance"/>, whose doc comment warns that it must never be added
/// there despite also being keyed by <c>UserId</c>. The distinction is purpose: a device
/// subscription exists solely to serve the member and has no audit value that outlives them, while
/// a terms acceptance evidences what the operator was told and must survive.
/// </para>
///
/// <para>
/// A row is removed when the member turns the device off, when they sign out of it, when the push
/// service reports it gone (404/410), when it has been unreachable past the retention window, and
/// when the account is deleted.
/// </para>
/// </summary>
public sealed class PushSubscription : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// The push service URL the browser issued for this device. Unique across all accounts: a
    /// device that changes hands <b>moves</b> to the new account rather than existing twice.
    /// Treated as opaque — never parsed, never used to infer a provider, never logged.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The device's public key (base64url), as the browser produced it.</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>The device's auth secret (base64url), as the browser produced it.</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// A recognisable label derived from the browser, shown only so a member can tell one of their
    /// devices from another. Never parsed and never used for a decision.
    /// </summary>
    public string? DeviceLabel { get; set; }

    /// <summary>
    /// When a delivery to this device last succeeded. Null until the first one. The retention sweep
    /// deletes by this column, which is why it is indexed.
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    public User User { get; set; } = null!;
}
