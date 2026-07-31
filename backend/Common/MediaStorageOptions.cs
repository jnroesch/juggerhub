namespace JuggerHub.Common;

/// <summary>
/// Configuration for the media object store (feature 035 / #97). Bound from the
/// <c>MediaStorage</c> section. Shape is identical across local/Dev/Prod (Principle V); only the
/// connection string differs — Azurite locally, a real storage account when deployed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConnectionString"/> is the ONLY secret here and never has a built-in default: a
/// missing connection string must fail loudly at startup rather than silently fall back to some
/// other environment's storage. Everything else has a safe default so the feature runs with
/// minimal configuration.
/// </para>
/// <para>
/// Resilience limits deliberately live elsewhere — in <c>Resilience:Outbound:MediaStore</c>, bound
/// by <see cref="ResilienceOptions"/> — so this integration is tuned exactly like every other one
/// (Principle VII: one integration, one resilience section, never a per-call-site decision).
/// </para>
/// </remarks>
public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    /// <summary>The integration name selecting <c>Resilience:Outbound:MediaStore</c> and naming
    /// the pipeline in telemetry, and the name of the HttpClient carrying it.</summary>
    public const string ResilienceName = "MediaStore";

    /// <summary>Storage connection string. Azurite locally; a real account key when deployed,
    /// supplied via GitHub Environments → Kubernetes Secret. No Key Vault (Principle V).</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container holding every media object. Intentionally the same name in every environment —
    /// isolation comes from each environment having its own storage <em>account</em>, not from
    /// container naming, so a misconfigured connection string fails to connect rather than
    /// quietly writing into a neighbour's container.
    /// </summary>
    public string ContainerName { get; set; } = "media";

    /// <summary>
    /// Emit <c>Cache-Control: private, no-cache</c> on media reads — store permitted, revalidation
    /// required.
    /// </summary>
    /// <remarks>
    /// Both tokens are load-bearing. <c>private</c> keeps a gated avatar out of shared caches,
    /// which would recreate in an intermediary the exposure that serving bytes through our own
    /// endpoints exists to prevent. <c>no-cache</c> rather than a long <c>max-age</c> is what makes
    /// a ban or a switch to private take effect on the very next request: a freshness window would
    /// stop the browser asking at all, leaving a removed member rendering in someone's page until
    /// it expired. Revalidation costs one descriptor read and a 304, so the caching win survives.
    /// </remarks>
    public bool CacheRevalidate { get; set; } = true;

    /// <summary>
    /// How long an unreferenced object must have existed before the reconciliation sweep may
    /// reclaim it. This is what stops the sweep deleting an object belonging to an upload that is
    /// still in flight — the object is written before its descriptor row is committed, so there is
    /// always a brief legitimate window where an object has no referent.
    /// </summary>
    public int OrphanGraceMinutes { get; set; } = 60;

    /// <summary>
    /// Repairs values that would weaken a limit, returning the reasons so startup can log them.
    /// Mirrors <see cref="ResilienceOptions.Normalize"/>: a bad value degrades to a safe default
    /// rather than taking the application down or disabling the protection outright.
    /// </summary>
    public IReadOnlyList<string> Normalize()
    {
        var defaults = new MediaStorageOptions();
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(ContainerName))
        {
            problems.Add($"{nameof(ContainerName)} must not be empty; using '{defaults.ContainerName}'.");
            ContainerName = defaults.ContainerName;
        }

        // A zero or negative grace period would let the sweep delete an object whose upload has not
        // yet committed its descriptor — turning a maintenance job into data loss.
        if (OrphanGraceMinutes <= 0)
        {
            problems.Add($"{nameof(OrphanGraceMinutes)} must be positive; using {defaults.OrphanGraceMinutes}.");
            OrphanGraceMinutes = defaults.OrphanGraceMinutes;
        }

        return problems;
    }
}
