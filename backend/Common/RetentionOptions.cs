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
    /// How long a refresh token row is kept <em>after its own expiry</em>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>16 is not arbitrary: it is the number that makes the published ceiling 30 days.</b> The
    /// longest token lifetime is remember-me at 14 days, so 14 + 16 bounds a row at 30 days from
    /// sign-in, and a non-persistent session at 17. The privacy policy states that ceiling in terms
    /// of signing in, because that is an event a reader can place; expiry of a rotating token is
    /// not. <c>RefreshTokenRetentionTests.Retention_ceiling_stays_within_the_thirty_days_the_policy_states</c>
    /// pins the arithmetic, so raising a token lifetime cannot quietly falsify the sentence.
    /// </para>
    /// <para>
    /// Why keep the row past expiry at all, given the token is dead: presenting an expired or
    /// revoked token revokes its whole family (<c>RefreshTokenService.RotateAsync</c>), whereas a
    /// missing row is merely a 401 with the family left intact. Note that rotation does *not* move
    /// <c>ExpiresAt</c> — it only sets <c>RevokedAt</c> — so keying deletion on expiry already
    /// preserves reuse detection for the whole nominal lifetime of every token, which is where the
    /// ordinary attack plays out. This grace covers only the residual case: a late replay of an
    /// already-expired token against a family still kept alive by rotation.
    /// </para>
    /// </remarks>
    public int RefreshTokenGraceDays { get; set; } = 16;

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
