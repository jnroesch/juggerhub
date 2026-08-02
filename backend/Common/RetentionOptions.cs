namespace JuggerHub.Common;

/// <summary>
/// Configuration for automated data retention (GH #106). Bound from the <c>Retention</c>
/// config section with defaults that work with zero configuration. No secrets here.
/// </summary>
/// <remarks>
/// These are not tuning knobs. <see cref="RefreshTokenGraceDays"/> is published as a factual
/// claim in the privacy policy's retention section, so changing it changes a legal document —
/// the two have to move together.
/// </remarks>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>
    /// Whether the background sweep runs at all. On everywhere by default; the integration
    /// tests turn it off so a timer cannot race the assertions that call the sweep directly.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long a refresh token row is kept <em>after its own expiry</em>. Not measured from
    /// issuance: a persistent token already lives 14 days, so 30 here means a row survives up
    /// to 44 days from sign-in.
    /// </summary>
    /// <remarks>
    /// The grace period exists for reuse detection, not for tidiness. Presenting an expired or
    /// revoked token revokes its whole family (<c>RefreshTokenService.RotateAsync</c>); once the
    /// row is gone that same replay is merely rejected, and a family kept alive by rotation is
    /// no longer torn down. Shortening this shortens the window in which a stolen token still
    /// triggers that response.
    /// </remarks>
    public int RefreshTokenGraceDays { get; set; } = 30;

    /// <summary>How often the sweep runs. Deletion is by age, so a missed run self-corrects.</summary>
    public int SweepIntervalHours { get; set; } = 24;

    /// <summary>
    /// Delay before the first sweep after startup, so it does not compete with the startup
    /// migrations and the initial burst of traffic a rolling deploy brings.
    /// </summary>
    public int StartupDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Hard ceiling on a single sweep. Principle VII: nothing waits forever — a sweep that
    /// cannot finish must fail loudly rather than hold a connection until the process dies.
    /// </summary>
    public int SweepTimeoutMinutes { get; set; } = 10;
}
