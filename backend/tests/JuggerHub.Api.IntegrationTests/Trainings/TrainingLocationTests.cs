using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Trainings;

/// <summary>
/// Structured training locations (feature 042) end-to-end: capture on create, the shared location
/// label, whole-series address edits, and the per-session address override.
/// </summary>
/// <remarks>
/// The single most important test here is
/// <see cref="Session_relocated_to_a_venueless_address_does_not_inherit_the_series_venue"/>. The
/// address override is an indivisible block keyed on <c>CityIdOverride</c>; an implementation that
/// resolves it per-field with <c>?? Training.X</c> — the pattern every OTHER override on
/// <see cref="TrainingSession"/> uses — passes every other test in this file and fails only that one.
/// </remarks>
[Collection("Trainings")]
public sealed class TrainingLocationTests : TrainingTestSupport
{
    public TrainingLocationTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- Helpers --------------------------------------------------------------

    private static object InPersonBody(
        DateOnly start,
        string? venueName = "Sportpark Müngersdorf",
        string? street = "Aachener Str. 999",
        string? postalCode = "50933",
        object? location = null,
        string name = "Tuesday training") => new
        {
            isRecurring = false,
            name,
            description = (string?)null,
            locationKind = "InPerson",
            venueName,
            street,
            postalCode,
            location = location ?? new { cityExternalId = "TEST:köln", name = "Köln" },
            virtualLink = (string?)null,
            weekday = (string?)null,
            interval = (string?)null,
            startTime = "19:00:00",
            endTime = "21:00:00",
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = (string?)null,
            visibility = "TeamOnly",
        };

    private async Task<Training> TrainingAsync(Guid trainingId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Trainings.Include(t => t.City).AsNoTracking().FirstAsync(t => t.Id == trainingId);
    }

    private async Task<TrainingSession> SessionAsync(Guid sessionId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TrainingSessions.AsNoTracking().FirstAsync(s => s.Id == sessionId);
    }

    private static async Task<(HttpStatusCode Status, string Body)> PostAsync(HttpClient client, string slug, object body)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/teams/{slug}/trainings", body);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    // --- US1: capture on create ------------------------------------------------

    [Fact]
    public async Task Create_in_person_stores_venue_street_postal_and_the_resolved_city()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (status, body) = await PostAsync(admin, slug, InPersonBody(NextWeekday(DayOfWeek.Tuesday)));
        Assert.Equal(HttpStatusCode.Created, status);

        var trainingId = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("trainingId").GetGuid();
        var training = await TrainingAsync(trainingId);

        Assert.Equal("Sportpark Müngersdorf", training.VenueName);
        Assert.Equal("Aachener Str. 999", training.Street);
        Assert.Equal("50933", training.PostalCode);
        Assert.NotNull(training.CityId);
        Assert.Equal("Köln", training.City!.Name);
        // The free-text column is now system-derived, never the admin's text (FR-012).
        Assert.Equal("Köln, Germany", training.Location);
    }

    [Fact]
    public async Task Create_in_person_without_a_street_is_rejected()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (status, _) = await PostAsync(admin, slug, InPersonBody(NextWeekday(DayOfWeek.Tuesday), street: null));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Create_in_person_without_a_postal_code_is_rejected()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (status, _) = await PostAsync(admin, slug, InPersonBody(NextWeekday(DayOfWeek.Tuesday), postalCode: null));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Create_in_person_without_a_city_is_rejected()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (status, body) = await PostAsync(admin, slug,
            InPersonBody(NextWeekday(DayOfWeek.Tuesday), location: new { cityExternalId = (string?)null, name = (string?)null }));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("city", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_in_person_with_a_venue_name_but_no_street_is_rejected()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        // A venue name is a label for a place, not a locatable address (spec edge cases).
        var (status, _) = await PostAsync(admin, slug,
            InPersonBody(NextWeekday(DayOfWeek.Tuesday), street: null, postalCode: null));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Create_with_an_unresolvable_city_is_rejected_and_stores_nothing()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (status, body) = await PostAsync(admin, slug, InPersonBody(
            NextWeekday(DayOfWeek.Tuesday),
            location: new { cityExternalId = "geonames:999999999", name = "Nowhere" },
            name: "Unresolvable city training"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("city could not be found", body, StringComparison.OrdinalIgnoreCase);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Trainings.AnyAsync(t => t.Name == "Unresolvable city training"));
    }

    [Fact]
    public async Task Create_virtual_stores_no_address_even_when_one_is_supplied()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        // FR-003: address members are simply not read for a virtual training.
        var (status, body) = await PostAsync(admin, slug, new
        {
            isRecurring = false,
            name = "Tactics call",
            description = (string?)null,
            locationKind = "Virtual",
            venueName = "Ignored hall",
            street = "Ignored street 1",
            postalCode = "00000",
            location = new { cityExternalId = "TEST:köln", name = "Köln" },
            virtualLink = "meet.example.com/abc",
            weekday = (string?)null,
            interval = (string?)null,
            startTime = "20:00:00",
            endTime = "21:00:00",
            startDate = NextWeekday(DayOfWeek.Thursday).ToString("yyyy-MM-dd"),
            endDate = (string?)null,
            visibility = "TeamOnly",
        });

        Assert.Equal(HttpStatusCode.Created, status);
        var training = await TrainingAsync(JsonSerializer.Deserialize<JsonElement>(body).GetProperty("trainingId").GetGuid());

        Assert.Null(training.VenueName);
        Assert.Null(training.Street);
        Assert.Null(training.PostalCode);
        Assert.Null(training.CityId);
        // Unlike an event, a virtual training keeps a null legacy label — the client renders
        // "Online" from the kind (research R2).
        Assert.Null(training.Location);
        Assert.Equal("https://meet.example.com/abc", training.VirtualLink);
    }

    // --- US2: one consistent label everywhere ----------------------------------

    [Fact]
    public async Task Detail_returns_the_structured_address_and_a_city_anchored_label()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (_, body) = await PostAsync(admin, slug, InPersonBody(NextWeekday(DayOfWeek.Tuesday)));
        var sessionId = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("firstSessionId").GetGuid();

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");

        Assert.Equal("Sportpark Müngersdorf", detail.GetProperty("venueName").GetString());
        Assert.Equal("Aachener Str. 999", detail.GetProperty("street").GetString());
        Assert.Equal("50933", detail.GetProperty("postalCode").GetString());
        Assert.Equal("Köln", detail.GetProperty("location").GetProperty("name").GetString());
        Assert.Equal("Köln, Germany", detail.GetProperty("location").GetProperty("label").GetString());
        Assert.Equal("TEST:köln", detail.GetProperty("location").GetProperty("externalId").GetString());
        Assert.Equal("Köln", detail.GetProperty("locationLabel").GetString());
        // `location` is now the structured city object, never the old free-text string.
        Assert.Equal(JsonValueKind.Object, detail.GetProperty("location").ValueKind);
    }

    [Fact]
    public async Task Tab_row_and_agenda_carry_the_same_label_as_the_detail()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (_, body) = await PostAsync(admin, slug, InPersonBody(NextWeekday(DayOfWeek.Tuesday)));
        var sessionId = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("firstSessionId").GetGuid();

        // Respond so the session appears on the caller's agenda.
        (await admin.PutAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}/response", new { answer = "Going" }))
            .EnsureSuccessStatusCode();

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");
        var rows = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/trainings/sessions?window=all&take=100");
        var agenda = await admin.GetFromJsonAsync<JsonElement>("/api/v1/me/trainings?take=100");

        var expected = detail.GetProperty("locationLabel").GetString();
        var row = rows.GetProperty("items").EnumerateArray().First(i => i.GetProperty("sessionId").GetGuid() == sessionId);
        var agendaItem = agenda.GetProperty("items").EnumerateArray().First(i => i.GetProperty("sessionId").GetGuid() == sessionId);

        Assert.Equal("Köln", expected);
        Assert.Equal(expected, row.GetProperty("locationLabel").GetString());
        Assert.Equal(expected, agendaItem.GetProperty("locationLabel").GetString());
    }

    [Fact]
    public async Task A_training_and_an_event_at_the_same_address_read_identically()
    {
        // SC-003. Both labels come from HomeProjections.LocationLabel — this is the test that
        // fails if a second implementation of the city → venue → legacy rule ever appears.
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (_, body) = await PostAsync(admin, slug, InPersonBody(NextWeekday(DayOfWeek.Tuesday)));
        var sessionId = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("firstSessionId").GetGuid();
        var trainingLabel = (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}"))
            .GetProperty("locationLabel").GetString();

        var eventResp = await admin.PostAsJsonAsync("/api/v1/events", new
        {
            name = "Same address cup",
            type = "Tournament",
            description = "A tournament at the very same address as the training, to compare labels.",
            startsAt = DateTime.UtcNow.AddDays(30).ToString("O"),
            endsAt = DateTime.UtcNow.AddDays(31).ToString("O"),
            locationKind = "InPerson",
            venueName = "Sportpark Müngersdorf",
            street = "Aachener Str. 999",
            postalCode = "50933",
            location = new { cityExternalId = "TEST:köln", name = "Köln" },
            virtualLink = (string?)null,
            participantMode = "Individuals",
            participationLimit = 24,
            isPaid = false,
        });
        Assert.True(eventResp.IsSuccessStatusCode, await eventResp.Content.ReadAsStringAsync());

        var eventId = JsonSerializer.Deserialize<JsonElement>(await eventResp.Content.ReadAsStringAsync())
            .GetProperty("id").GetGuid();

        // Up-next is built from the caller's signups, so join before comparing.
        (await admin.PostAsJsonAsync($"/api/v1/events/{eventId}/signup", new { }))
            .EnsureSuccessStatusCode();

        var home = await admin.GetFromJsonAsync<JsonElement>("/api/v1/home/up-next?take=100");
        var eventItem = home.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("id").GetGuid() == eventId);

        Assert.Equal(trainingLabel, eventItem.GetProperty("locationLabel").GetString());
    }

    [Fact]
    public async Task A_training_with_a_city_but_no_venue_labels_as_the_city_alone()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (status, body) = await PostAsync(admin, slug,
            InPersonBody(NextWeekday(DayOfWeek.Tuesday), venueName: null));
        Assert.Equal(HttpStatusCode.Created, status);

        var sessionId = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("firstSessionId").GetGuid();
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");

        Assert.Null(detail.GetProperty("venueName").GetString());
        Assert.Equal("Köln", detail.GetProperty("locationLabel").GetString());
    }

    [Fact]
    public async Task A_virtual_training_carries_no_label_and_keeps_its_link()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var (_, body) = await PostAsync(admin, slug, new
        {
            isRecurring = false, name = "Tactics call", description = (string?)null,
            locationKind = "Virtual", venueName = (string?)null, street = (string?)null,
            postalCode = (string?)null, location = (object?)null,
            virtualLink = "https://meet.example.com/xyz",
            weekday = (string?)null, interval = (string?)null,
            startTime = "20:00:00", endTime = "21:00:00",
            startDate = NextWeekday(DayOfWeek.Friday).ToString("yyyy-MM-dd"),
            endDate = (string?)null, visibility = "TeamOnly",
        });

        var sessionId = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("firstSessionId").GetGuid();
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");

        // The client renders "Online" from the kind, exactly as before 042.
        Assert.Equal("Virtual", detail.GetProperty("locationKind").GetString());
        Assert.Equal(string.Empty, detail.GetProperty("locationLabel").GetString());
        Assert.Equal("https://meet.example.com/xyz", detail.GetProperty("virtualLink").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("location").ValueKind);
    }

    // --- US3: whole-series address edits ---------------------------------------

    /// <summary>Creates a Köln series and returns (trainingId, firstSessionId, slug).</summary>
    private async Task<(HttpClient Admin, Guid TrainingId, Guid SessionId, string Slug)> KolnSeriesAsync()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);
        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        return (admin, created.GetProperty("trainingId").GetGuid(), created.GetProperty("firstSessionId").GetGuid(), slug);
    }

    private static object BerlinAddress(string? venueName = "Tempelhofer Feld") => new
    {
        locationKind = "InPerson",
        venueName,
        street = "Tempelhofer Damm 1",
        postalCode = "12101",
        location = new { cityExternalId = "TEST:berlin", name = "Berlin" },
    };

    [Fact]
    public async Task Series_address_edit_moves_every_upcoming_session_that_still_follows_it()
    {
        var (admin, trainingId, sessionId, slug) = await KolnSeriesAsync();

        (await admin.PatchAsJsonAsync($"/api/v1/trainings/{trainingId}", BerlinAddress()))
            .EnsureSuccessStatusCode();

        var rows = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/trainings/sessions?window=all&take=100");
        var labels = rows.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("locationLabel").GetString()).ToList();

        Assert.NotEmpty(labels);
        Assert.All(labels, l => Assert.Equal("Berlin", l));

        // Inheritance, not a per-session write: no session gained its own address.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.TrainingSessions.AnyAsync(s => s.TrainingId == trainingId && s.CityIdOverride != null));
        _ = sessionId;
    }

    [Fact]
    public async Task Series_edit_clearing_the_city_on_an_in_person_training_is_refused()
    {
        var (admin, trainingId, _, _) = await KolnSeriesAsync();

        var resp = await admin.PatchAsJsonAsync($"/api/v1/trainings/{trainingId}", new
        {
            locationKind = "InPerson",
            venueName = "Somewhere",
            street = "Some street 1",
            postalCode = "12345",
            location = new { cityExternalId = (string?)null, name = (string?)null },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("city", await resp.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Series_edit_switching_to_virtual_clears_the_stored_address()
    {
        var (admin, trainingId, _, _) = await KolnSeriesAsync();

        (await admin.PatchAsJsonAsync($"/api/v1/trainings/{trainingId}", new
        {
            locationKind = "Virtual",
            virtualLink = "https://meet.example.com/series",
        })).EnsureSuccessStatusCode();

        var training = await TrainingAsync(trainingId);

        Assert.Null(training.VenueName);
        Assert.Null(training.Street);
        Assert.Null(training.PostalCode);
        Assert.Null(training.CityId);
        Assert.Null(training.Location);
    }

    // --- US4: per-session relocation -------------------------------------------

    [Fact]
    public async Task Session_relocated_to_a_venueless_address_does_not_inherit_the_series_venue()
    {
        // ⚠ THE LOAD-BEARING TEST (042 FR-007 / research R1).
        //
        // The series HAS a venue name ("Sportpark Müngersdorf"); this session is relocated to an
        // address with NONE. Resolving the address per-field — `VenueNameOverride ?? Training.VenueName`,
        // the pattern every other override on TrainingSession uses — would render the SERIES' venue
        // against the SESSION's Berlin street. That implementation passes every other test here.
        var (admin, _, sessionId, _) = await KolnSeriesAsync();

        (await admin.PatchAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}", BerlinAddress(venueName: null)))
            .EnsureSuccessStatusCode();

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("venueName").ValueKind);
        Assert.Equal("Tempelhofer Damm 1", detail.GetProperty("street").GetString());
        Assert.Equal("Berlin", detail.GetProperty("location").GetProperty("name").GetString());
        Assert.Equal("Berlin", detail.GetProperty("locationLabel").GetString());
        Assert.DoesNotContain("Müngersdorf", detail.GetProperty("locationLabel").GetString()!);
    }

    [Fact]
    public async Task Relocating_one_session_leaves_its_siblings_untouched()
    {
        var (admin, trainingId, sessionId, slug) = await KolnSeriesAsync();

        (await admin.PatchAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}", BerlinAddress()))
            .EnsureSuccessStatusCode();

        var rows = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/trainings/sessions?window=all&take=100");
        var byId = rows.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("sessionId").GetGuid(), i => i.GetProperty("locationLabel").GetString());

        Assert.Equal("Berlin", byId[sessionId]);
        Assert.All(byId.Where(kv => kv.Key != sessionId), kv => Assert.Equal("Köln", kv.Value));
        _ = trainingId;
    }

    [Fact]
    public async Task Session_relocation_without_a_city_is_refused()
    {
        var (admin, _, sessionId, _) = await KolnSeriesAsync();

        var resp = await admin.PatchAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}", new
        {
            locationKind = "InPerson",
            venueName = "Hall B",
            street = "Hallenweg 2",
            postalCode = "50667",
            location = new { cityExternalId = (string?)null, name = (string?)null },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("city", await resp.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_relocated_session_keeps_its_address_when_the_series_moves_afterwards()
    {
        var (admin, trainingId, sessionId, _) = await KolnSeriesAsync();

        // Relocate this session to Berlin...
        (await admin.PatchAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}", BerlinAddress()))
            .EnsureSuccessStatusCode();

        // ...then move the whole series somewhere else entirely.
        (await admin.PatchAsJsonAsync($"/api/v1/trainings/{trainingId}", new
        {
            locationKind = "InPerson",
            venueName = "Neue Halle",
            street = "Hamburger Str. 5",
            postalCode = "20095",
            location = new { cityExternalId = "TEST:hamburg", name = "Hamburg" },
        })).EnsureSuccessStatusCode();

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");

        Assert.Equal("Berlin", detail.GetProperty("locationLabel").GetString());
        Assert.Equal("Tempelhofer Damm 1", detail.GetProperty("street").GetString());
        Assert.True(detail.GetProperty("isDetached").GetBoolean());
    }

    [Fact]
    public async Task A_session_switched_to_virtual_stores_no_address()
    {
        var (admin, _, sessionId, _) = await KolnSeriesAsync();

        (await admin.PatchAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}", new
        {
            locationKind = "Virtual",
            virtualLink = "https://meet.example.com/one-off",
        })).EnsureSuccessStatusCode();

        var session = await SessionAsync(sessionId);

        Assert.Null(session.VenueNameOverride);
        Assert.Null(session.StreetOverride);
        Assert.Null(session.PostalCodeOverride);
        Assert.Null(session.CityIdOverride);
        Assert.Null(session.LocationOverride);
    }

    [Fact]
    public async Task A_time_only_session_edit_freezes_the_address_without_changing_it()
    {
        // Intended consequence of extending the existing detach-freeze to the address: the session
        // now owns its address even though the admin only touched the time (042 research R3).
        var (admin, _, sessionId, _) = await KolnSeriesAsync();

        (await admin.PatchAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}",
            new { startTime = "18:00:00", endTime = "20:00:00" })).EnsureSuccessStatusCode();

        var session = await SessionAsync(sessionId);
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");

        Assert.NotNull(session.CityIdOverride);
        Assert.Equal("Sportpark Müngersdorf", session.VenueNameOverride);
        Assert.Equal("Köln", detail.GetProperty("locationLabel").GetString());
    }
}
