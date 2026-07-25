namespace JuggerHub.Entities;

/// <summary>
/// A precomputed great-circle distance between two <see cref="City"/> records (feature 030),
/// powering "near you" proximity sort without PostGIS. Distance is a pure function of the two
/// cities' coordinates, so it is computed once (in C#, at city-creation time) and cached here.
/// </summary>
/// <remarks>
/// Stored <b>bidirectionally</b> (both <c>A→B</c> and <c>B→A</c>), plus a self-row <c>X→X = 0</c>,
/// so a proximity query is a single-sided indexed join anchored on <see cref="FromCityId"/> =
/// the player's home city, ordered by <see cref="DistanceKm"/>. The self-row is required: it is
/// what makes a player's own-city entities appear (at distance 0, ranked first). Backfill runs
/// inside the EF execution strategy as one retriable unit (constitution Principle VII). At Jugger's
/// scale (tens–low hundreds of cities) the pair set is small.
/// </remarks>
public sealed class CityDistance : BaseEntity
{
    public Guid FromCityId { get; set; }

    public Guid ToCityId { get; set; }

    /// <summary>Great-circle (haversine) distance in kilometres. Non-negative; 0 for the self-row.</summary>
    public double DistanceKm { get; set; }

    public City FromCity { get; set; } = null!;

    public City ToCity { get; set; } = null!;
}
