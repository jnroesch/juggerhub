using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Trainings;

/// <summary>
/// Trainings (018) end-to-end against the real API + Postgres container: create (series/one-off +
/// validation), RSVP upsert + who's-coming, the this-vs-series edit fork, skip/cancel, public/guest
/// access + isolation, and the cross-team dashboard agenda — plus the server-side authorization
/// boundaries (member vs admin vs outsider).
/// </summary>
[Collection("Trainings")]
public sealed class TrainingApiTests : TrainingTestSupport
{
    public TrainingApiTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- US2: create ----------------------------------------------------------

    [Fact]
    public async Task Create_weekly_series_generates_one_session_per_weekday_occurrence()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var start = NextWeekday(DayOfWeek.Tuesday);
        var end = start.AddDays(7 * 4); // 5 Tuesdays inclusive (weeks 0..4)
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, end);

        Assert.Equal(5, created.GetProperty("sessionCount").GetInt32());
        Assert.Equal(5, await SessionCountAsync(created.GetProperty("trainingId").GetGuid()));
    }

    [Fact]
    public async Task Create_one_off_makes_a_single_session()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var created = await CreateOneOffAsync(admin, slug, NextWeekday(DayOfWeek.Saturday));

        Assert.Equal(1, created.GetProperty("sessionCount").GetInt32());
        Assert.Equal(1, await SessionCountAsync(created.GetProperty("trainingId").GetGuid()));
    }

    [Fact]
    public async Task Create_by_a_non_admin_member_is_forbidden()
    {
        var (admin, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(admin);
        var (member, memberId, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);

        var start = NextWeekday(DayOfWeek.Tuesday);
        var body = new
        {
            isRecurring = true, name = "X", description = (string?)null,
            locationKind = "InPerson", location = "Hall", virtualLink = (string?)null,
            weekday = "Tuesday", interval = "Weekly", startTime = "19:00:00", endTime = "21:00:00",
            startDate = start.ToString("yyyy-MM-dd"), endDate = start.AddDays(21).ToString("yyyy-MM-dd"), visibility = "TeamOnly",
        };
        var resp = await member.PostAsJsonAsync($"/api/v1/teams/{slug}/trainings", body);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Create_with_end_date_before_start_is_rejected()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var start = NextWeekday(DayOfWeek.Tuesday);
        var body = new
        {
            isRecurring = true, name = "X", description = (string?)null,
            locationKind = "InPerson", location = "Hall", virtualLink = (string?)null,
            weekday = "Tuesday", interval = "Weekly", startTime = "19:00:00", endTime = "21:00:00",
            startDate = start.ToString("yyyy-MM-dd"), endDate = start.AddDays(-7).ToString("yyyy-MM-dd"), visibility = "TeamOnly",
        };
        var resp = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/trainings", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- US1: respond ---------------------------------------------------------

    [Fact]
    public async Task Member_rsvp_is_a_single_current_response_and_updates_counts()
    {
        var (admin, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(admin);
        var (member, memberId, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);

        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        var sessionId = created.GetProperty("firstSessionId").GetGuid();

        var going = await member.PutAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}/response", new { answer = "Going" });
        going.EnsureSuccessStatusCode();
        Assert.Equal(1, (await going.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("goingCount").GetInt32());

        var cant = await member.PutAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}/response", new { answer = "Cant" });
        cant.EnsureSuccessStatusCode();
        var row = await cant.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, row.GetProperty("goingCount").GetInt32());
        Assert.Equal(1, row.GetProperty("cantCount").GetInt32());
        Assert.Equal("Cant", row.GetProperty("myAnswer").GetString());

        // Exactly one response row for this (session, user).
        var detail = await member.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");
        Assert.Equal("Cant", detail.GetProperty("myAnswer").GetString());
    }

    [Fact]
    public async Task Non_member_cannot_view_or_rsvp_a_team_only_session()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);
        var (outsider, _, _) = await NewUserAsync();

        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        var sessionId = created.GetProperty("firstSessionId").GetGuid();

        var view = await outsider.GetAsync($"/api/v1/trainings/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.NotFound, view.StatusCode);

        var rsvp = await outsider.PutAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}/response", new { answer = "Going" });
        Assert.Equal(HttpStatusCode.NotFound, rsvp.StatusCode);
    }

    [Fact]
    public async Task Session_list_carries_badge_going_count_and_my_answer()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);

        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        var sessionId = created.GetProperty("firstSessionId").GetGuid();
        (await admin.PutAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}/response", new { answer = "Going" })).EnsureSuccessStatusCode();

        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/trainings/sessions?window=all&take=100");
        var first = page.GetProperty("items").EnumerateArray().First(i => i.GetProperty("sessionId").GetGuid() == sessionId);
        Assert.False(first.GetProperty("isOneOff").GetBoolean());
        Assert.Equal(1, first.GetProperty("goingCount").GetInt32());
        Assert.Equal("Going", first.GetProperty("myAnswer").GetString());
    }

    [Fact]
    public async Task Session_detail_exposes_series_schedule_for_the_edit_form()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);
        var start = NextWeekday(DayOfWeek.Tuesday);
        var series = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        var oneOff = await CreateOneOffAsync(admin, slug, NextWeekday(DayOfWeek.Saturday));

        var seriesDetail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{series.GetProperty("firstSessionId").GetGuid()}");
        Assert.Equal("Tuesday", seriesDetail.GetProperty("weekday").GetString());
        Assert.Equal("Weekly", seriesDetail.GetProperty("interval").GetString());
        Assert.False(seriesDetail.GetProperty("endDate").ValueKind == JsonValueKind.Null);

        var oneOffDetail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{oneOff.GetProperty("firstSessionId").GetGuid()}");
        Assert.Equal(JsonValueKind.Null, oneOffDetail.GetProperty("weekday").ValueKind);
        Assert.Equal(JsonValueKind.Null, oneOffDetail.GetProperty("interval").ValueKind);
        Assert.Equal(JsonValueKind.Null, oneOffDetail.GetProperty("endDate").ValueKind);
    }

    // --- US3: edit fork, skip, cancel ----------------------------------------

    [Fact]
    public async Task Whole_series_time_edit_applies_in_place_to_upcoming_sessions()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);
        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        var trainingId = created.GetProperty("trainingId").GetGuid();

        var edit = await admin.PatchAsJsonAsync($"/api/v1/trainings/{trainingId}", new { startTime = "19:30:00", endTime = "21:30:00" });
        edit.EnsureSuccessStatusCode();

        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/trainings/sessions?window=all&take=100");
        Assert.All(page.GetProperty("items").EnumerateArray(), i => Assert.Equal("19:30:00", i.GetProperty("startTime").GetString()));
    }

    [Fact]
    public async Task Single_session_edit_detaches_it_from_later_series_edits()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);
        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        var trainingId = created.GetProperty("trainingId").GetGuid();
        var sessionId = created.GetProperty("firstSessionId").GetGuid();

        // Detach this session with its own location.
        (await admin.PatchAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}", new { location = "Hall B" })).EnsureSuccessStatusCode();
        // Whole-series time edit.
        (await admin.PatchAsJsonAsync($"/api/v1/trainings/{trainingId}", new { startTime = "18:00:00", endTime = "20:00:00" })).EnsureSuccessStatusCode();

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");
        Assert.True(detail.GetProperty("isDetached").GetBoolean());
        Assert.Equal("Hall B", detail.GetProperty("location").GetString());
        Assert.Equal("19:00:00", detail.GetProperty("startTime").GetString()); // kept its own, not the series 18:00
    }

    [Fact]
    public async Task Extending_the_end_date_generates_new_sessions()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);
        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(7 * 2)); // 3 sessions
        var trainingId = created.GetProperty("trainingId").GetGuid();
        Assert.Equal(3, await SessionCountAsync(trainingId));

        var edit = await admin.PatchAsJsonAsync($"/api/v1/trainings/{trainingId}", new { endDate = start.AddDays(7 * 4).ToString("yyyy-MM-dd") });
        edit.EnsureSuccessStatusCode();
        var result = await edit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("addedSessions").GetInt32());
        Assert.Equal(5, await SessionCountAsync(trainingId));
    }

    [Fact]
    public async Task Skip_hides_the_session_and_cancel_keeps_it_but_blocks_responses()
    {
        var (admin, _, _) = await NewUserAsync();
        var (teamId, slug) = await CreateTeamAsync(admin);
        var (member, memberId, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, memberId);
        var start = NextWeekday(DayOfWeek.Tuesday);
        var created = await CreateSeriesAsync(admin, slug, DayOfWeek.Tuesday, start, start.AddDays(21));
        var items = (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/trainings/sessions?window=all&take=100"))
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sessionId").GetGuid()).ToList();

        // Skip the first, cancel the second.
        (await admin.PostAsync($"/api/v1/trainings/sessions/{items[0]}/skip", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/trainings/sessions/{items[1]}/cancel", null)).EnsureSuccessStatusCode();

        var after = (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/trainings/sessions?window=all&take=100"))
            .GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(after, i => i.GetProperty("sessionId").GetGuid() == items[0]); // skipped: hidden
        var cancelled = after.First(i => i.GetProperty("sessionId").GetGuid() == items[1]);
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString()); // cancelled: still visible

        var rsvp = await member.PutAsJsonAsync($"/api/v1/trainings/sessions/{items[1]}/response", new { answer = "Going" });
        Assert.Equal(HttpStatusCode.Conflict, rsvp.StatusCode);
    }

    // --- US4: public / guests -------------------------------------------------

    [Fact]
    public async Task Public_session_lets_an_outsider_rsvp_as_a_counted_removable_guest()
    {
        var (admin, _, _) = await NewUserAsync();
        var (_, slug) = await CreateTeamAsync(admin);
        var (outsider, outsiderId, _) = await NewUserAsync();

        var date = NextWeekday(DayOfWeek.Saturday);
        var created = await CreateOneOffAsync(admin, slug, date, visibility: "Public");
        var sessionId = created.GetProperty("firstSessionId").GetGuid();

        // Outsider can view + RSVP as guest.
        var detail = await outsider.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}");
        Assert.True(detail.GetProperty("viewerIsGuest").GetBoolean());
        var rsvp = await outsider.PutAsJsonAsync($"/api/v1/trainings/sessions/{sessionId}/response", new { answer = "Going" });
        rsvp.EnsureSuccessStatusCode();
        Assert.Equal(1, (await rsvp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("goingCount").GetInt32());

        // Admin attendance shows the guest and can remove them.
        var attendance = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/trainings/sessions/{sessionId}/attendance?take=100");
        var guest = attendance.GetProperty("items").EnumerateArray().First(i => i.GetProperty("isGuest").GetBoolean());
        Assert.False(guest.GetProperty("isTeamAdmin").GetBoolean());

        var remove = await admin.DeleteAsync($"/api/v1/trainings/sessions/{sessionId}/guests/{outsiderId}");
        remove.EnsureSuccessStatusCode();

        // Guest is gone and never joined the team.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JuggerHub.Data.AppDbContext>();
        Assert.False(await db.TrainingResponses.AnyAsync(r => r.TrainingSessionId == sessionId && r.UserId == outsiderId));
        Assert.False(await db.TeamMemberships.AnyAsync(m => m.UserId == outsiderId));
    }

    // --- US5: dashboard agenda ------------------------------------------------

    [Fact]
    public async Task Agenda_merges_upcoming_sessions_across_teams_and_public_guest_sessions()
    {
        // Team A: the user is a member with a session.
        var (adminA, _, _) = await NewUserAsync();
        var (teamAId, slugA) = await CreateTeamAsync(adminA);
        var (user, userId, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamAId, userId);
        var createdA = await CreateSeriesAsync(adminA, slugA, DayOfWeek.Tuesday, NextWeekday(DayOfWeek.Tuesday), NextWeekday(DayOfWeek.Tuesday).AddDays(7));
        var sessionA = createdA.GetProperty("firstSessionId").GetGuid();

        // Team B: a public session the user joins as a guest (not a member of B).
        var (adminB, _, _) = await NewUserAsync();
        var (_, slugB) = await CreateTeamAsync(adminB);
        var createdB = await CreateOneOffAsync(adminB, slugB, NextWeekday(DayOfWeek.Wednesday), visibility: "Public");
        var sessionB = createdB.GetProperty("firstSessionId").GetGuid();
        (await user.PutAsJsonAsync($"/api/v1/trainings/sessions/{sessionB}/response", new { answer = "Going" })).EnsureSuccessStatusCode();

        var agenda = await user.GetFromJsonAsync<JsonElement>("/api/v1/me/trainings?take=100");
        var ids = agenda.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sessionId").GetGuid()).ToList();
        Assert.Contains(sessionA, ids);
        Assert.Contains(sessionB, ids);
    }
}
