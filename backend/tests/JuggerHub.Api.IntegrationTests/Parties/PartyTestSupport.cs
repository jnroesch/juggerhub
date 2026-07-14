using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Parties;

/// <summary>Shares one Testcontainers Postgres + host across all party test classes.</summary>
[CollectionDefinition("Parties")]
public sealed class PartiesCollection : ICollectionFixture<JuggerHubApiFactory>;

/// <summary>
/// Shared helpers for the event-parties (016) integration tests: user/team/event/party seeding
/// against the real API + Postgres container.
/// </summary>
public abstract class PartyTestSupport
{
    protected JuggerHubApiFactory Factory { get; }

    protected PartyTestSupport(JuggerHubApiFactory factory) => Factory = factory;

    protected async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = Factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, Factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    protected async Task<(Guid TeamId, string Slug)> CreateTeamAsync(HttpClient adminClient)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var resp = await adminClient.PostAsJsonAsync("/api/v1/teams",
            new { name = "Rheinfeuer", slug, type = "Mixteam", city = (string?)null });
        resp.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teamId = await db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).FirstAsync();
        return (teamId, slug);
    }

    /// <summary>Directly seed a plain team membership (no invite dance) for roster tests.</summary>
    protected async Task AddTeamMemberAsync(Guid teamId, Guid userId, TeamRole role = TeamRole.Member)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TeamMemberships.Add(new TeamMembership
        {
            TeamId = teamId,
            UserId = userId,
            Role = role,
            JoinedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    protected async Task<Guid> CreateTeamsEventAsync(HttpClient client, int rosterCap = 8, bool paid = false, int participationLimit = 8)
    {
        object body = new
        {
            name = "Tempelhof Summer Slam",
            type = "Tournament",
            description = "Two days of open Jugger on the old airfield.",
            startsAt = "2026-09-05T09:00:00Z",
            endsAt = "2026-09-06T18:00:00Z",
            locationKind = "Virtual",
            venueName = (string?)null,
            street = (string?)null,
            postalCode = (string?)null,
            city = (string?)null,
            country = (string?)null,
            virtualLink = "https://jugger.example/slam",
            participantMode = "Teams",
            participationLimit,
            rosterCap,
            isPaid = paid,
            feeAmount = paid ? 40m : (decimal?)null,
            feeCurrency = paid ? "EUR" : (string?)null,
            feeRecipientName = paid ? "JSC Berlin e.V." : (string?)null,
            feeIban = paid ? "DE89370400440532013000" : (string?)null,
            feePaymentDeadline = (string?)null,
        };
        var resp = await client.PostAsJsonAsync("/api/v1/events", body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Form a party (admin must administer the team); returns the new party id.</summary>
    protected async Task<Guid> FormPartyAsync(HttpClient adminClient, Guid eventId, Guid teamId, string? message = null)
    {
        var resp = await adminClient.PostAsJsonAsync("/api/v1/parties", new { eventId, teamId, message });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected static string ExtractPartyInviteToken(string html)
    {
        const string marker = "/party-invite/";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOfAny(['"', '<', ' ', '\''], start);
        return html[start..end];
    }
}
