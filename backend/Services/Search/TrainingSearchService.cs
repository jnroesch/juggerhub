using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using JuggerHub.Services.Geocoding;
using JuggerHub.Services.Trainings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>
/// Public-training browse/search (feature 043). Lists sessions teams have opened to everyone,
/// across every team — the discovery surface that turns "a team opened its training" into
/// "a stranger found it".
/// </summary>
public interface ITrainingSearchService
{
    /// <remarks>
    /// <paramref name="homeCityId"/> is the caller's home city, required only for
    /// <see cref="TrainingSort.Proximity"/>; when null under that sort the service falls back to the
    /// default ordering (the controller returns 409 first, so this is belt-and-braces).
    /// </remarks>
    Task<PagedResult<TrainingCardDto>> BrowseAsync(
        TrainingBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class TrainingSearchService : ITrainingSearchService
{
    private readonly AppDbContext _db;
    private readonly SearchOptions _options;

    public TrainingSearchService(AppDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PagedResult<TrainingCardDto>> BrowseAsync(
        TrainingBrowseQuery query, PaginationRequest pagination, Guid? homeCityId = null, CancellationToken ct = default)
    {
        // ---- The two unconditional gates ------------------------------------------------------
        //
        // ⚠ These two clauses ARE the feature's security boundary and must never become conditional
        // on the caller. In particular there is deliberately NO team-membership join anywhere in
        // this query: browse does not widen for a member of the owning team, and does not narrow
        // for an outsider (spec FR-004). That is what makes the rule structural rather than a
        // convention someone has to remember.
        var q = _db.TrainingSessions.AsNoTracking()
            .Where(s => (s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public)
            .Where(s => s.Status == TrainingSessionStatus.Scheduled);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (query.HidePast)
        {
            // Day-granular, deliberately (feature 043 research R2). Every other trainings query in
            // the product filters `SessionDate >= today` — a session must not vanish from browse
            // while it is still showing on the team's own tab. Consequence, accepted: a session that
            // ended earlier today stays listed until midnight UTC.
            q = q.Where(s => s.SessionDate >= today);
        }

        if (query.From is { } from)
        {
            q = q.Where(s => s.SessionDate >= from);
        }

        if (query.To is { } to)
        {
            q = q.Where(s => s.SessionDate <= to);
        }

        // ⚠ The address block is resolved by branching on CityIdOverride at the TOP of each
        // predicate, so a session is matched wholly against its own address or wholly against the
        // series' — never a mix. See the block comment below before editing.
        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            q = q.Where(s => s.CityIdOverride != null
                ? s.CityOverride != null
                    && EF.Functions.ILike(AppDbContext.Unaccent(s.CityOverride.Name), AppDbContext.Unaccent(city))
                : s.Training.City != null
                    && EF.Functions.ILike(AppDbContext.Unaccent(s.Training.City.Name), AppDbContext.Unaccent(city)));
        }

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            var country = query.Country.Trim();
            q = q.Where(s => s.CityIdOverride != null
                ? s.CityOverride != null
                    && (s.CityOverride.CountryCode == country
                        || EF.Functions.ILike(AppDbContext.Unaccent(s.CityOverride.CountryName), AppDbContext.Unaccent(country)))
                : s.Training.City != null
                    && (s.Training.City.CountryCode == country
                        || EF.Functions.ILike(AppDbContext.Unaccent(s.Training.City.CountryName), AppDbContext.Unaccent(country))));
        }

        var term = SearchQuery.Normalize(query.Q, _options.MinQueryLength);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            q = q.Where(s =>
                EF.Functions.ILike(AppDbContext.Unaccent(s.Training.Name), AppDbContext.Unaccent(pattern)));
        }

        var useProximity = query.Sort == TrainingSort.Proximity && homeCityId is not null;

        // ---- Total ----------------------------------------------------------------------------
        //
        // ⚠ Under proximity the total MUST be computed with the SAME exclusion the join applies,
        // or "load more" stalls short of a count it can never reach. TeamSearchService,
        // EventSearchService (since issue #146) and this service all recompute the total this way.
        var total = useProximity
            ? await q.CountAsync(
                s => (s.CityIdOverride != null ? s.CityIdOverride : s.Training.CityId) != null
                    && _db.CityDistances.Any(d => d.FromCityId == homeCityId!.Value
                        && d.ToCityId == (s.CityIdOverride != null ? s.CityIdOverride : s.Training.CityId)),
                ct)
            : await q.CountAsync(ct);

        var page = useProximity
            ? ProximityPage(q, homeCityId!.Value, pagination)
            : q.OrderBy(s => s.SessionDate).ThenBy(s => s.Id) // stable tiebreaker (UUIDv7)
                .Skip(pagination.NormalizedSkip).Take(pagination.NormalizedTake)
                .Select(CardProjection());

        var raw = await page.ToListAsync(ct);
        var items = raw.Select(ToCard).ToList();
        return new PagedResult<TrainingCardDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    /// <summary>
    /// Nearest-first via the <see cref="CityDistance"/> cache, anchored on the caller's home city.
    /// The inner join is what excludes cityless sessions — every virtual training, and any training
    /// predating feature 042 (spec FR-022).
    /// </summary>
    private IQueryable<TrainingCardRaw> ProximityPage(
        IQueryable<TrainingSession> q, Guid homeCityId, PaginationRequest pagination)
    {
        var ordered =
            from s in q
            join d in _db.CityDistances.Where(cd => cd.FromCityId == homeCityId)
                // The block key IS the city id, so the ternary and `??` are equivalent here — see
                // the block comment below for why it is still written the long way.
                on (s.CityIdOverride != null ? s.CityIdOverride : s.Training.CityId) equals (Guid?)d.ToCityId
            orderby d.DistanceKm, s.SessionDate, s.Id
            select s;

        return ordered
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(CardProjection());
    }

    // ---- ⚠⚠ THE ADDRESS BLOCK — read before editing any predicate or projection above ---------
    //
    // The session address is ONE INDIVISIBLE BLOCK keyed on CityIdOverride (feature 042). Every
    // OTHER override on TrainingSession resolves as `X ?? Training.X`; the address MUST NOT. A
    // per-field `??` would render a relocated session's street under the SERIES' city, or leak the
    // series' venue name onto a session relocated to a venue-less address.
    //
    // So every predicate and projection here branches on `s.CityIdOverride != null` FIRST and reads
    // only one side of that branch. The CITY ID is the one field where `??` would in fact be
    // equivalent, because the block is KEYED on it — it is still written the long way, because the
    // shorthand is indistinguishable at a glance from the defect the TrainingSession comment
    // forbids, and consistency costs nothing.
    //
    // ⚠ These expressions are written INLINE and must stay inline. Factoring them into
    // `private static bool EffectiveCityName(TrainingSession s)`-style helpers compiles cleanly and
    // then throws at RUNTIME: EF Core cannot translate an arbitrary method call inside an
    // expression tree, and a green build proves nothing about it.

    /// <summary>
    /// The raw parts of a browse card, straight out of SQL. The display label is composed after
    /// materialization by <see cref="ToCard"/> — the same two-step the trainings tab and the event
    /// agenda use — because the label helper is a C# method and cannot be translated to SQL.
    /// </summary>
    internal sealed record TrainingCardRaw(
        Guid SessionId,
        Guid TrainingId,
        string Name,
        string TeamSlug,
        string TeamName,
        bool IsOneOff,
        DateOnly SessionDate,
        TimeOnly StartTime,
        TimeOnly EndTime,
        LocationKind Kind,
        City? City,
        string? VenueName,
        string? LegacyLocation);

    /// <remarks>
    /// ⚠ The address resolves as an INDIVISIBLE BLOCK keyed on <c>CityIdOverride</c> — see the
    /// helpers above and feature 042 research R1.
    /// </remarks>
    private static System.Linq.Expressions.Expression<Func<TrainingSession, TrainingCardRaw>> CardProjection() =>
        s => new TrainingCardRaw(
            s.Id,
            s.TrainingId,
            s.Training.Name,
            s.Training.Team.Slug,
            s.Training.Team.Name,
            !s.Training.IsRecurring,
            s.SessionDate,
            s.StartTimeOverride ?? s.Training.StartTime,
            s.EndTimeOverride ?? s.Training.EndTime,
            s.LocationKindOverride ?? s.Training.LocationKind,
            s.CityIdOverride != null ? s.CityOverride : s.Training.City,
            s.CityIdOverride != null ? s.VenueNameOverride : s.Training.VenueName,
            s.CityIdOverride != null ? s.LocationOverride : s.Training.Location);

    /// <summary>
    /// Composes the card from a materialized raw row.
    /// </summary>
    private static TrainingCardDto ToCard(TrainingCardRaw r) => new(
        r.SessionId,
        r.TrainingId,
        r.Name,
        r.TeamSlug,
        r.TeamName,
        r.IsOneOff,
        r.SessionDate,
        r.StartTime,
        r.EndTime,
        r.Kind,
        r.Kind == LocationKind.Virtual ? null : LocationLabels.ToLocation(r.City),
        BrowseLocationLabel(r));

    /// <summary>
    /// The label shown on a browse row: <c>"City, Country"</c>, falling back to the venue name and
    /// then the legacy free-text location when a training has no canonical city.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ This deliberately does <b>not</b> call <see cref="TrainingSeriesService.LocationLabelFor"/>,
    /// and the difference is load-bearing rather than an oversight. There are two different location
    /// labels in this product, on two different surfaces:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Browse rows</b> render <c>"City, Country"</c>. <see cref="EventSearchService"/>'s card
    /// projection builds exactly that inline (it is in a SQL projection, so it cannot call a helper),
    /// and this method reuses the same <see cref="LocationLabels.Display"/> formatting so a training
    /// and an event at the same address read character-for-character identically on their respective
    /// browse lists (spec SC-003).</item>
    /// <item><b>The dashboard agenda</b> renders the city alone, via
    /// <see cref="Home.HomeProjections.LocationLabel"/> — and events use that same helper there, so
    /// that surface is internally consistent too.</item>
    /// </list>
    /// <para>
    /// Calling <c>LocationLabelFor</c> here would produce "Berlin" against the events list's
    /// "Berlin, Germany" — which is precisely what the SC-003 integration test caught. Feature 042's
    /// "one shared helper" claim was about the agenda, not about browse.
    /// </para>
    /// <para>
    /// The virtual case keeps trainings' deliberate 042 divergence: Event returns the literal
    /// "Online", Training returns empty and lets the client render the word from
    /// <c>locationKind</c> in the viewer's own language.
    /// </para>
    /// </remarks>
    private static string BrowseLocationLabel(TrainingCardRaw r)
    {
        if (r.Kind == LocationKind.Virtual)
        {
            return string.Empty;
        }

        if (r.City is not null)
        {
            return LocationLabels.Display(r.City.Name, r.City.CountryName);
        }

        return !string.IsNullOrWhiteSpace(r.VenueName) ? r.VenueName!
            : r.LegacyLocation ?? string.Empty;
    }
}
