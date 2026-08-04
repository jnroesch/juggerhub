using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// Public-training browse (feature 043): the visibility gate, status exclusion, the feature-042
/// address block, filters, ordering, and the proximity view. Exercises the real API + Postgres
/// container.
/// </summary>
/// <remarks>
/// <para>
/// Two tests here carry the feature. <see cref="Team_only_sessions_are_invisible_even_to_members"/>
/// pins that browse is defined by the training being public and never by who is asking; and
/// <see cref="Relocated_session_is_filtered_under_its_own_city_not_the_series"/> pins the feature-042
/// address block. Both were verified non-vacuous by deliberately breaking the implementation:
/// resolving the city filter against <c>s.Training.City</c> alone fails exactly the second one.
/// </para>
/// <para>
/// ⚠ Note for whoever extends these: the classic 042 <b>venue-leak</b> guard is NOT reproducible on
/// this surface, and a test claiming to cover it here would be vacuous — verified by breaking the
/// venue resolution to a per-field <c>??</c> and watching all tests still pass. The browse card
/// exposes only <c>locationLabel</c> and the city, the label prefers the city whenever there is one,
/// and the address block is <em>keyed</em> on the city — so a relocated session always has a city and
/// the venue never reaches the output. The venue fallback is only reachable for a training with no
/// canonical city at all, which by definition has no override to leak from. The projection is still
/// written block-shaped, for consistency with every other training projection and so that adding a
/// venue-bearing field later cannot quietly introduce the defect.
/// </para>
/// </remarks>
[Collection("Search")]
public sealed class TrainingBrowseTests
{
    private readonly JuggerHubApiFactory _factory;

    public TrainingBrowseTests(JuggerHubApiFactory factory) => _factory = factory;

    // ---- US1: the gates -----------------------------------------------------------------------

    [Fact]
    public async Task Team_only_sessions_are_invisible_even_to_members()
    {
        var city = "Berlin";
        var (owner, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Gate " + Rnd(), city);

        var publicTraining = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, "Public " + Rnd(), TrainingVisibility.Public, city);
        var teamOnlyTraining = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, "TeamOnly " + Rnd(), TrainingVisibility.TeamOnly, city);

        var visible = await SearchTestSupport.SeedTrainingSessionAsync(_factory, publicTraining, Soon());
        var hidden = await SearchTestSupport.SeedTrainingSessionAsync(_factory, teamOnlyTraining, Soon());

        // An outsider sees only the public one.
        var outsider = await SearchTestSupport.AuthedClientAsync(_factory);
        var outsiderIds = await IdsAsync(outsider, $"/api/v1/trainings?city={city}&take=100");
        Assert.Contains(visible.ToString(), outsiderIds);
        Assert.DoesNotContain(hidden.ToString(), outsiderIds);

        // ⚠ The one that matters: a MEMBER of the owning team sees exactly the same list. Browse is
        // defined by the training being public, never by who is asking (FR-004). A membership join
        // sneaking into the query would light this up.
        var (memberClient, memberId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.AddTeamMemberAsync(_factory, teamId, memberId);
        var memberIds = await IdsAsync(memberClient, $"/api/v1/trainings?city={city}&take=100");
        Assert.Contains(visible.ToString(), memberIds);
        Assert.DoesNotContain(hidden.ToString(), memberIds);
    }

    [Fact]
    public async Task Session_visibility_override_wins_over_the_series_in_both_directions()
    {
        var city = "Hamburg";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Override " + Rnd(), city);

        // A team-only series with ONE session opened to everyone.
        var closedSeries = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, "Closed " + Rnd(), TrainingVisibility.TeamOnly, city, isRecurring: true);
        var openedSession = await SearchTestSupport.SeedTrainingSessionAsync(
            _factory, closedSeries, Soon(), visibilityOverride: TrainingVisibility.Public);
        var stillClosedSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, closedSeries, Soon(2));

        // A public series with ONE session closed again.
        var openSeries = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, "Open " + Rnd(), TrainingVisibility.Public, city, isRecurring: true);
        var closedSession = await SearchTestSupport.SeedTrainingSessionAsync(
            _factory, openSeries, Soon(3), visibilityOverride: TrainingVisibility.TeamOnly);
        var stillOpenSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, openSeries, Soon(4));

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var ids = await IdsAsync(viewer, $"/api/v1/trainings?city={city}&take=100");

        Assert.Contains(openedSession.ToString(), ids);
        Assert.DoesNotContain(stillClosedSession.ToString(), ids);
        Assert.DoesNotContain(closedSession.ToString(), ids);
        Assert.Contains(stillOpenSession.ToString(), ids);
    }

    [Fact]
    public async Task Cancelled_and_skipped_are_never_listed_under_any_filter()
    {
        var city = "München";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Status " + Rnd(), city);
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, "Status " + Rnd(), TrainingVisibility.Public, city, isRecurring: true);

        var scheduled = await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, Soon());
        var cancelled = await SearchTestSupport.SeedTrainingSessionAsync(
            _factory, training, Soon(2), status: TrainingSessionStatus.Cancelled);
        var skipped = await SearchTestSupport.SeedTrainingSessionAsync(
            _factory, training, Soon(3), status: TrainingSessionStatus.Skipped);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);

        var upcoming = await IdsAsync(viewer, $"/api/v1/trainings?city={city}&take=100");
        Assert.Contains(scheduled.ToString(), upcoming);
        Assert.DoesNotContain(cancelled.ToString(), upcoming);
        Assert.DoesNotContain(skipped.ToString(), upcoming);

        // Revealing past sessions must not reveal cancelled/skipped ones either.
        var withPast = await IdsAsync(viewer, $"/api/v1/trainings?city={city}&hidePast=false&take=100");
        Assert.DoesNotContain(cancelled.ToString(), withPast);
        Assert.DoesNotContain(skipped.ToString(), withPast);
    }

    [Fact]
    public async Task Anonymous_callers_are_refused()
    {
        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync("/api/v1/trainings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- US1: the feature-042 address block ---------------------------------------------------

    [Fact]
    public async Task Relocated_session_never_leaks_the_series_address()
    {
        // The guard: the SERIES has a venue name, and one session is relocated to a DIFFERENT city
        // at an address with NO venue name. Resolving the address per-field with `??` would render
        // the series' venue ("Sportpark Müngersdorf") against the session's city — the exact defect
        // feature 042 documented on TrainingSession.
        var seriesCity = "Köln";
        var sessionCity = "Hamburg";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Relocate " + Rnd(), seriesCity);

        var name = "Relocated " + Rnd();
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name, TrainingVisibility.Public, seriesCity,
            venueName: "Sportpark Müngersdorf", street: "Aachener Str. 999", postalCode: "50933",
            isRecurring: true);

        var athome = await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, Soon());
        var relocated = await SearchTestSupport.SeedTrainingSessionAsync(
            _factory, training, Soon(2),
            overrideCity: sessionCity, overrideVenueName: null,
            overrideStreet: "Hallerstr. 1", overridePostalCode: "20146");

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var cards = await CardsAsync(viewer, $"/api/v1/trainings?q={Uri.EscapeDataString(name)}&take=100");

        var homeCard = cards.Single(c => c.GetProperty("sessionId").GetString() == athome.ToString());
        var movedCard = cards.Single(c => c.GetProperty("sessionId").GetString() == relocated.ToString());

        // The unmoved session shows the series' venue, because city → venue → legacy prefers city.
        Assert.Equal($"{seriesCity}, Germany", homeCard.GetProperty("locationLabel").GetString());
        Assert.Equal(seriesCity, homeCard.GetProperty("location").GetProperty("name").GetString());

        // The relocated one shows ITS city, and no trace of the series' address anywhere.
        Assert.Equal($"{sessionCity}, Germany", movedCard.GetProperty("locationLabel").GetString());
        Assert.Equal(sessionCity, movedCard.GetProperty("location").GetProperty("name").GetString());
        Assert.DoesNotContain("Müngersdorf", movedCard.ToString());
        Assert.DoesNotContain("Aachener", movedCard.ToString());
    }

    [Fact]
    public async Task Pre_042_training_without_a_city_is_still_listed_with_a_label()
    {
        // A training created before feature 042: free-text Location only, no CityId. It must still
        // be discoverable and still render something readable (spec edge case 2).
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Legacy " + Rnd(), "Berlin");
        var name = "Legacy " + Rnd();
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name, TrainingVisibility.Public,
            city: null, legacyLocation: "Alte Turnhalle, Buxtehude");
        var session = await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, Soon());

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var cards = await CardsAsync(viewer, $"/api/v1/trainings?q={Uri.EscapeDataString(name)}&take=100");
        var card = cards.Single(c => c.GetProperty("sessionId").GetString() == session.ToString());

        Assert.Equal("Alte Turnhalle, Buxtehude", card.GetProperty("locationLabel").GetString());
        Assert.Equal(JsonValueKind.Null, card.GetProperty("location").ValueKind);

        // It cannot match a city filter — there is no canonical city to match on.
        var filtered = await IdsAsync(viewer, "/api/v1/trainings?city=Berlin&take=100");
        Assert.DoesNotContain(session.ToString(), filtered);
    }

    [Fact]
    public async Task Training_and_event_at_the_same_address_read_identically()
    {
        // SC-003. The label is composed by ONE helper shared with events; if someone copies it
        // instead of calling it, the two drift and this fails.
        var city = "Berlin";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Parity " + Rnd(), city);

        var trainingName = "Parity " + Rnd();
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, trainingName, TrainingVisibility.Public, city);
        await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, Soon());

        var eventName = "Parity " + Rnd();
        var now = DateTime.UtcNow;
        await SearchTestSupport.SeedEventAsync(_factory, eventName, now.AddDays(9), now.AddDays(10), city: city);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var trainingCards = await CardsAsync(viewer, $"/api/v1/trainings?q={Uri.EscapeDataString(trainingName)}&take=100");
        var eventCards = await CardsAsync(viewer, $"/api/v1/events?q={Uri.EscapeDataString(eventName)}&take=100");

        var trainingLabel = trainingCards.Single().GetProperty("locationLabel").GetString();
        var eventLabel = eventCards.Single().GetProperty("locationLabel").GetString();

        Assert.False(string.IsNullOrWhiteSpace(trainingLabel));
        Assert.Equal(eventLabel, trainingLabel);
    }

    [Fact]
    public async Task Paging_repeats_nothing_and_skips_nothing()
    {
        var city = "Köln";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Paging " + Rnd(), city);
        var name = "Paging " + Rnd();
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name, TrainingVisibility.Public, city, isRecurring: true);

        var seeded = new List<string>();
        for (var i = 0; i < 7; i++)
        {
            seeded.Add((await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, Soon(i))).ToString());
        }

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var url = $"/api/v1/trainings?q={Uri.EscapeDataString(name)}";

        var paged = new List<string>();
        for (var skip = 0; skip < 9; skip += 3)
        {
            paged.AddRange(await IdsAsync(viewer, $"{url}&skip={skip}&take=3"));
        }

        Assert.Equal(seeded.Count, paged.Distinct().Count()); // nothing repeated
        Assert.All(seeded, id => Assert.Contains(id, paged)); // nothing skipped
    }

    // ---- US2: filters -------------------------------------------------------------------------

    [Fact]
    public async Task City_and_country_filters_narrow_and_return_no_non_matching_row()
    {
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Filter " + Rnd(), "Berlin");
        var name = "Filter " + Rnd();

        var berlin = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name + " B", TrainingVisibility.Public, "Berlin");
        var hamburg = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name + " H", TrainingVisibility.Public, "Hamburg");
        var berlinSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, berlin, Soon());
        var hamburgSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, hamburg, Soon());

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);

        var inBerlin = await CardsAsync(viewer, $"/api/v1/trainings?q={Uri.EscapeDataString(name)}&city=Berlin&take=100");
        Assert.Contains(inBerlin, c => c.GetProperty("sessionId").GetString() == berlinSession.ToString());
        Assert.DoesNotContain(inBerlin, c => c.GetProperty("sessionId").GetString() == hamburgSession.ToString());
        // Server-side filtering: the endpoint never hands back a row the client would have to drop.
        Assert.All(inBerlin, c => Assert.Equal("Berlin", c.GetProperty("location").GetProperty("name").GetString()));

        // Country matches by ISO code and by name; both test cities are in Germany.
        var german = await IdsAsync(viewer, $"/api/v1/trainings?q={Uri.EscapeDataString(name)}&country=DE&take=100");
        Assert.Contains(berlinSession.ToString(), german);
        Assert.Contains(hamburgSession.ToString(), german);
    }

    [Fact]
    public async Task Relocated_session_is_filtered_under_its_own_city_not_the_series()
    {
        var seriesCity = "Köln";
        var sessionCity = "München";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "FilterBlock " + Rnd(), seriesCity);
        var name = "FilterBlock " + Rnd();
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name, TrainingVisibility.Public, seriesCity, isRecurring: true);

        var relocated = await SearchTestSupport.SeedTrainingSessionAsync(
            _factory, training, Soon(), overrideCity: sessionCity);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var q = Uri.EscapeDataString(name);

        var bySessionCity = await IdsAsync(viewer, $"/api/v1/trainings?q={q}&city={sessionCity}&take=100");
        Assert.Contains(relocated.ToString(), bySessionCity);

        var bySeriesCity = await IdsAsync(viewer, $"/api/v1/trainings?q={q}&city={seriesCity}&take=100");
        Assert.DoesNotContain(relocated.ToString(), bySeriesCity);
    }

    [Fact]
    public async Task Date_range_and_hide_past_narrow_results()
    {
        var city = "Berlin";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Dates " + Rnd(), city);
        var name = "Dates " + Rnd();
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name, TrainingVisibility.Public, city, isRecurring: true);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var past = await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, today.AddDays(-10));
        var soon = await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, today.AddDays(3));
        var later = await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, today.AddDays(40));

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var q = Uri.EscapeDataString(name);

        var upcoming = await IdsAsync(viewer, $"/api/v1/trainings?q={q}&take=100");
        Assert.DoesNotContain(past.ToString(), upcoming);
        Assert.Contains(soon.ToString(), upcoming);

        var withPast = await IdsAsync(viewer, $"/api/v1/trainings?q={q}&hidePast=false&take=100");
        Assert.Contains(past.ToString(), withPast);

        // `to` alone.
        var untilSoon = await IdsAsync(viewer, $"/api/v1/trainings?q={q}&to={today.AddDays(10):yyyy-MM-dd}&take=100");
        Assert.Contains(soon.ToString(), untilSoon);
        Assert.DoesNotContain(later.ToString(), untilSoon);

        // `from` alone.
        var fromLate = await IdsAsync(viewer, $"/api/v1/trainings?q={q}&from={today.AddDays(20):yyyy-MM-dd}&take=100");
        Assert.Contains(later.ToString(), fromLate);
        Assert.DoesNotContain(soon.ToString(), fromLate);
    }

    [Fact]
    public async Task Name_search_is_accent_and_case_insensitive()
    {
        var city = "Berlin";
        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Accent " + Rnd(), city);
        var token = Rnd();
        var training = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, $"Anfängertraining {token}", TrainingVisibility.Public, city);
        var session = await SearchTestSupport.SeedTrainingSessionAsync(_factory, training, Soon());

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);

        var folded = await IdsAsync(viewer, $"/api/v1/trainings?q={Uri.EscapeDataString("anfanger")}&take=100");
        Assert.Contains(session.ToString(), folded);

        var upper = await IdsAsync(viewer, $"/api/v1/trainings?q={Uri.EscapeDataString("ANFÄNGER")}&take=100");
        Assert.Contains(session.ToString(), upper);
    }

    // ---- US3: proximity -----------------------------------------------------------------------

    [Fact]
    public async Task Proximity_orders_by_distance_from_the_callers_home_city()
    {
        // Home = Köln. Hamburg (~356km) is nearer than München (~456km).
        var (viewer, viewerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.SetHomeCityAsync(_factory, viewerId, "Köln");

        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Prox " + Rnd(), "Köln");
        var name = "Prox " + Rnd();

        var far = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name + " far", TrainingVisibility.Public, "München");
        var near = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name + " near", TrainingVisibility.Public, "Hamburg");
        var farSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, far, Soon());
        var nearSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, near, Soon());

        var ordered = await IdsAsync(
            viewer, $"/api/v1/trainings?q={Uri.EscapeDataString(name)}&sort=Proximity&take=100");

        Assert.True(
            ordered.IndexOf(nearSession.ToString()) < ordered.IndexOf(farSession.ToString()),
            "Hamburg should sort ahead of München when home is Köln.");
    }

    [Fact]
    public async Task Proximity_without_a_home_city_is_refused_not_silently_reordered()
    {
        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var response = await viewer.GetAsync("/api/v1/trainings?sort=Proximity");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("home city", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Proximity_excludes_cityless_sessions_from_items_and_from_the_total()
    {
        // The check that separates this from EventSearchService's count-before-join defect: paging
        // to the end must reach exactly totalCount, so a virtual/cityless session excluded by the
        // join must also be excluded from the count.
        var (viewer, viewerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.SetHomeCityAsync(_factory, viewerId, "Köln");

        var (_, ownerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var (teamId, _) = await SearchTestSupport.SeedTeamAsync(_factory, "Cityless " + Rnd(), "Köln");
        var name = "Cityless " + Rnd();

        var located = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name + " located", TrainingVisibility.Public, "Hamburg");
        var virtualTraining = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name + " online", TrainingVisibility.Public,
            city: null, kind: LocationKind.Virtual);
        var legacy = await SearchTestSupport.SeedTrainingAsync(
            _factory, teamId, ownerId, name + " legacy", TrainingVisibility.Public,
            city: null, legacyLocation: "Irgendwo");

        var locatedSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, located, Soon());
        var virtualSession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, virtualTraining, Soon());
        var legacySession = await SearchTestSupport.SeedTrainingSessionAsync(_factory, legacy, Soon());

        var q = Uri.EscapeDataString(name);

        // Default sort: all three are discoverable.
        var byDate = await IdsAsync(viewer, $"/api/v1/trainings?q={q}&take=100");
        Assert.Contains(locatedSession.ToString(), byDate);
        Assert.Contains(virtualSession.ToString(), byDate);
        Assert.Contains(legacySession.ToString(), byDate);

        // Proximity: cityless drops out of the items...
        var page = await PageAsync(viewer, $"/api/v1/trainings?q={q}&sort=Proximity&take=100");
        var ids = page.GetProperty("items").EnumerateArray()
            .Select(c => c.GetProperty("sessionId").GetString()!).ToList();
        Assert.Contains(locatedSession.ToString(), ids);
        Assert.DoesNotContain(virtualSession.ToString(), ids);
        Assert.DoesNotContain(legacySession.ToString(), ids);

        // ...and out of the total, so "load more" cannot chase a count it can never reach.
        Assert.Equal(ids.Count, page.GetProperty("totalCount").GetInt32());
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static string Rnd() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>A near-future date; day-granular, matching how the service filters upcoming.</summary>
    private static DateOnly Soon(int daysAhead = 1) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1 + daysAhead);

    private static async Task<JsonElement> PageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode,
            $"GET {url} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<List<JsonElement>> CardsAsync(HttpClient client, string url) =>
        (await PageAsync(client, url)).GetProperty("items").EnumerateArray().ToList();

    private static async Task<List<string>> IdsAsync(HttpClient client, string url) =>
        (await CardsAsync(client, url))
            .Select(c => (c.TryGetProperty("sessionId", out var s) ? s : c.GetProperty("id")).GetString()!)
            .ToList();
}
