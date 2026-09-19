namespace JuggerHub.Common;

/// <summary>
/// Application-server identity for Web Push (feature 055). VAPID (RFC 8292) is how a push service
/// knows which application server a delivery came from: the public key is handed to the browser at
/// subscribe time, and every delivery is signed with the private half.
///
/// <para>
/// Each environment has its OWN key pair. A subscription is bound to the public key it was created
/// with, so a Dev key can never deliver to a Prod subscription and vice versa — which is the point.
/// Generate a pair with <c>scripts/New-VapidKeyPair.ps1</c>.
/// </para>
///
/// <para>
/// <see cref="PrivateKey"/> is a secret and travels the same path as every other one: empty here,
/// commented out in <c>.env.sample</c>, a <c>${VAR:-}</c> in compose, and a Kubernetes Secret from
/// a GitHub Environment in Dev/Prod. <see cref="PublicKey"/> is not a secret — the browser needs
/// it — but it is still per-environment, so it is served from an endpoint rather than baked into
/// the frontend bundle (which would force one build per environment; feature 033 established why
/// that is the thing to avoid).
/// </para>
/// </summary>
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>Name of the resilience pipeline and of its configuration section under <c>Resilience:Outbound</c>.</summary>
    public const string ResilienceName = "WebPush";

    /// <summary>
    /// VAPID <c>sub</c> claim: how a push service contacts us about our traffic. Must be a
    /// <c>mailto:</c> or <c>https:</c> URI — push services reject anything else.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Base64url of the uncompressed P-256 point (<c>0x04 || X || Y</c>). Public; sent to browsers.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Base64url of the P-256 private scalar <c>D</c>. <b>Secret. Never logged, never sent anywhere.</b></summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// True when all three values are present and the subject has a scheme a push service accepts.
    /// Startup fails when this is false: a silently disabled notification channel is worse than a
    /// refused start, which is the same reasoning the Redis and chat-encryption guards use.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(PrivateKey)
        && !string.IsNullOrWhiteSpace(Subject)
        && (Subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || Subject.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
