namespace JuggerHub.Common;

/// <summary>
/// Configuration for reading Tugeny, the tournament-day bracket tool whose public, MIT-licensed
/// data interface feeds tournament results (feature 050). Bound from the <c>Tugeny</c> section.
/// Shape is identical across local/Dev/Prod (Principle V); nothing here is a secret — the interface
/// is public and unauthenticated.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BaseUrl"/> is the ONLY host this feature ever contacts. User input never supplies a
/// host: an event admin's pasted address is reduced to a validated slug and appended to this base,
/// which is what keeps the link action free of server-side request forgery (Principle I).
/// </para>
/// <para>
/// Resilience limits deliberately live elsewhere — in <c>Resilience:Outbound:Tugeny</c>, bound by
/// <see cref="ResilienceOptions"/> — so this integration is tuned exactly like every other one
/// (Principle VII: one integration, one resilience section, never a per-call-site decision).
/// </para>
/// </remarks>
public sealed class TugenyOptions
{
    public const string SectionName = "Tugeny";

    /// <summary>The integration name selecting <c>Resilience:Outbound:Tugeny</c> and naming the
    /// pipeline in telemetry, and the name of the HttpClient carrying it.</summary>
    public const string ResilienceName = "Tugeny";

    private const string DefaultBaseUrl = "https://tugeny.org/";

    /// <summary>
    /// The largest response body accepted. The largest real one (528 matches) is ~246 KB; the cap
    /// leaves an order of magnitude of headroom and stops a runaway body from being buffered.
    /// </summary>
    private const int DefaultMaxResponseBytes = 4 * 1024 * 1024;

    /// <summary>Tugeny's site root. The data interface is under <c>api/persistent/</c>.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Response bodies larger than this fail permanently, without a retry.</summary>
    public int MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;

    /// <summary>The site root as a <see cref="Uri"/>, always ending in a slash.</summary>
    public Uri BaseUri => new(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");

    /// <summary>
    /// Repairs invalid values in place, falling back to the safe defaults — never to "unlimited"
    /// (Principle VII). Returns one message per repair so startup can log it.
    /// </summary>
    public IReadOnlyList<string> Normalize()
    {
        var problems = new List<string>();

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            problems.Add($"BaseUrl '{BaseUrl}' is not an absolute http(s) URL; using {DefaultBaseUrl}.");
            BaseUrl = DefaultBaseUrl;
        }

        if (MaxResponseBytes <= 0)
        {
            problems.Add($"MaxResponseBytes must be positive; using {DefaultMaxResponseBytes}.");
            MaxResponseBytes = DefaultMaxResponseBytes;
        }

        return problems;
    }
}
