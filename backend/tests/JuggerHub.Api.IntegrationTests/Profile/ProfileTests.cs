using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Profile;

/// <summary>
/// Profile feature (003): handle at registration, the public share page, owner
/// editing, and recent activity. Exercises the real API + Postgres container.
/// </summary>
[Collection("Profile")]
public sealed class ProfileTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly JuggerHubApiFactory _factory;

    public ProfileTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: handle at registration ------------------------------------------

    [Fact]
    public async Task Register_with_valid_handle_creates_a_profile()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var email = AuthTestHelpers.NewEmail();

        var response = await AuthTestHelpers.RegisterAsync(client, email, handle: handle);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var public_ = await client.GetAsync($"/api/v1/profiles/{handle}");
        Assert.Equal(HttpStatusCode.OK, public_.StatusCode);
    }

    [Fact]
    public async Task Register_with_duplicate_handle_is_rejected_with_409()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();

        var first = await AuthTestHelpers.RegisterAsync(client, AuthTestHelpers.NewEmail(), handle: handle);
        var second = await AuthTestHelpers.RegisterAsync(client, AuthTestHelpers.NewEmail(), handle: handle);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("Has Space")]
    [InlineData("UPPER")]
    [InlineData("ab")]            // too short
    [InlineData("-leading")]
    [InlineData("bad_underscore")]
    public async Task Register_with_malformed_handle_is_rejected_with_400(string handle)
    {
        var client = _factory.CreateClient();

        var response = await AuthTestHelpers.RegisterAsync(client, AuthTestHelpers.NewEmail(), handle: handle);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_reserved_handle_is_rejected_with_400()
    {
        var client = _factory.CreateClient();

        var response = await AuthTestHelpers.RegisterAsync(client, AuthTestHelpers.NewEmail(), handle: "admin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Handle_available_endpoint_reports_taken_and_reserved()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        await AuthTestHelpers.RegisterAsync(client, AuthTestHelpers.NewEmail(), handle: handle);

        var taken = await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/handle-available?handle={handle}");
        var free = await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/handle-available?handle={AuthTestHelpers.NewHandle()}");
        var reserved = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/handle-available?handle=api");

        Assert.False(taken.GetProperty("available").GetBoolean());
        Assert.True(free.GetProperty("available").GetBoolean());
        Assert.False(reserved.GetProperty("available").GetBoolean());
    }

    // --- US2: public profile safety -------------------------------------------

    [Fact]
    public async Task Public_profile_never_exposes_email_or_account_data()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var email = AuthTestHelpers.NewEmail();
        await AuthTestHelpers.RegisterAsync(client, email, handle: handle);

        // Anonymous fetch — no session.
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync($"/api/v1/profiles/{handle}");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(email, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(raw);
        Assert.False(doc.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Public_profile_for_unknown_handle_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/profiles/{AuthTestHelpers.NewHandle()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Public_profile_shows_only_selected_pompfen()
    {
        var (client, handle, _) = await RegisterVerifyLoginAsync();
        await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Nik",
            hometown = (string?)null,
            description = (string?)null,
            pompfen = new[] { "Stab", "Schild" },
        });

        var anon = _factory.CreateClient();
        var dto = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{handle}");
        var selected = dto.GetProperty("selectedPompfen").EnumerateArray().Select(e => e.GetString()).ToArray();

        Assert.Equal(2, selected.Length);
        Assert.Contains("Stab", selected);
        Assert.Contains("Schild", selected);
    }

    // --- US3: owner editing + authorization -----------------------------------

    [Fact]
    public async Task Owner_endpoints_require_authentication()
    {
        var anon = _factory.CreateClient();

        var get = await anon.GetAsync("/api/v1/profiles/me");
        var put = await anon.PutAsJsonAsync("/api/v1/profiles/me",
            new { displayName = "x", hometown = (string?)null, description = (string?)null, pompfen = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Fact]
    public async Task Owner_update_persists_fields_and_pompfen()
    {
        var (client, _, _) = await RegisterVerifyLoginAsync();

        var updated = await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Nik Berlin",
            hometown = "Berlin",
            description = "Läufer at heart.",
            pompfen = new[] { "Stab", "Laeufer" },
        });
        var dto = await client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Nik Berlin", dto.GetProperty("displayName").GetString());
        Assert.Equal("Berlin", dto.GetProperty("hometown").GetString());
        Assert.Equal(2, dto.GetProperty("pompfen").GetArrayLength());
    }

    [Fact]
    public async Task Update_ignores_any_handle_field_handle_is_immutable()
    {
        var (client, handle, _) = await RegisterVerifyLoginAsync();

        // A rogue "handle" in the body must not change anything.
        await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Nik",
            hometown = (string?)null,
            description = (string?)null,
            pompfen = Array.Empty<string>(),
            handle = "totally-different",
        });
        var dto = await client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me");

        Assert.Equal(handle, dto.GetProperty("handle").GetString());
        var moved = await client.GetAsync("/api/v1/profiles/totally-different");
        Assert.Equal(HttpStatusCode.NotFound, moved.StatusCode);
    }

    [Fact]
    public async Task Avatar_upload_rejects_non_image_content()
    {
        var (client, _, _) = await RegisterVerifyLoginAsync();

        using var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("this is not an image"u8.ToArray());
        content.Add(bytes, "file", "avatar.png");
        var response = await client.PutAsync("/api/v1/profiles/me/avatar", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Avatar_upload_accepts_a_valid_png()
    {
        var (client, handle, _) = await RegisterVerifyLoginAsync();

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MinimalPng()), "file", "avatar.png");
        var upload = await client.PutAsync("/api/v1/profiles/me/avatar", content);

        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        var avatar = await _factory.CreateClient().GetAsync($"/api/v1/profiles/{handle}/avatar");
        Assert.Equal(HttpStatusCode.OK, avatar.StatusCode);
        Assert.Equal("image/png", avatar.Content.Headers.ContentType?.MediaType);
    }

    // --- US4: recent activity -------------------------------------------------

    [Fact]
    public async Task Activity_is_newest_first_and_paginated()
    {
        var (client, handle, _) = await RegisterVerifyLoginAsync();
        var profileId = await GetProfileIdAsync(handle);
        await SeedActivityAsync(profileId);

        var anon = _factory.CreateClient();
        var page = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{handle}/activity?take=2");
        var items = page.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(4, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, items.Length); // capped by take
        // Newest first: 2025-08 before 2025-06.
        Assert.Equal("Sommerturnier Berlin", items[0].GetProperty("eventName").GetString());
        Assert.Equal("Team A", items[0].GetProperty("teamLabel").GetString());
    }

    [Fact]
    public async Task Activity_for_profile_with_none_is_empty()
    {
        var (_, handle, _) = await RegisterVerifyLoginAsync();

        var anon = _factory.CreateClient();
        var page = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{handle}/activity");

        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<(HttpClient Client, string Handle, string Email)> RegisterVerifyLoginAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, handle, email);
    }

    private async Task<Guid> GetProfileIdAsync(string handle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PlayerProfiles.Where(p => p.Handle == handle).Select(p => p.Id).SingleAsync();
    }

    private async Task SeedActivityAsync(Guid profileId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        static Event ActivityEvent(string name, DateOnly date, string location) => new()
        {
            Name = name,
            Description = name,
            StartsAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            EndsAt = date.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc),
            Location = location,
        };

        var events = new[]
        {
            ActivityEvent("Sommerturnier Berlin", new DateOnly(2025, 8, 16), "Berlin"),
            ActivityEvent("Liga-Spieltag Hamburg", new DateOnly(2025, 6, 21), "Hamburg"),
            ActivityEvent("Trainingscamp Köln", new DateOnly(2025, 5, 10), "Köln"),
            ActivityEvent("Stadtmeisterschaft Leipzig", new DateOnly(2025, 4, 5), "Leipzig"),
        };
        db.Events.AddRange(events);
        await db.SaveChangesAsync();

        var teams = new[] { "Team A", "Team B", "Team A", "Team B" };
        for (var i = 0; i < events.Length; i++)
        {
            db.EventParticipations.Add(new EventParticipation
            {
                ProfileId = profileId,
                EventId = events[i].Id,
                TeamLabel = teams[i],
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>A minimal but valid 1x1 PNG (correct magic bytes).</summary>
    private static byte[] MinimalPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
}
