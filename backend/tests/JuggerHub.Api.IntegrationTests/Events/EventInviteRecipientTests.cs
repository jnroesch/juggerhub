using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Events;

/// <summary>
/// A targeted co-admin invitation is bound to its recipient (GH #401): holding the token does not
/// make someone else an event admin, and does not let them use the invitation up. A shared link
/// stays a bearer credential. Exercises the real API + Postgres container.
/// </summary>
[Collection("Events")]
public sealed class EventInviteRecipientTests
{
    private readonly JuggerHubApiFactory _factory;

    public EventInviteRecipientTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Another_account_cannot_accept_a_targeted_invitation()
    {
        var (org, _, _) = await NewUserAsync();
        var eventId = await CreateEventAsync(org);
        var (target, targetId, targetEmail) = await NewUserAsync();
        var token = await InviteAsync(org, eventId, targetId, targetEmail);

        var (mallory, malloryId, _) = await NewUserAsync();
        var accept = await mallory.PostAsync($"/api/v1/event-invitations/{token}/accept", null);

        // Refused exactly as a token that does not exist is, so the answer confirms nothing.
        var unknown = await mallory.PostAsync($"/api/v1/event-invitations/{Guid.NewGuid():N}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        await AssertSameProblemAsync(unknown, accept);

        Assert.False(await IsAdminAsync(eventId, malloryId));
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));

        // The recipient is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await target.PostAsync($"/api/v1/event-invitations/{token}/accept", null)).StatusCode);
        Assert.True(await IsAdminAsync(eventId, targetId));
        Assert.Equal(InvitationStatus.Accepted, await StatusAsync(token));
    }

    [Fact]
    public async Task Another_account_cannot_decline_a_targeted_invitation()
    {
        var (org, _, _) = await NewUserAsync();
        var eventId = await CreateEventAsync(org);
        var (target, targetId, targetEmail) = await NewUserAsync();
        var token = await InviteAsync(org, eventId, targetId, targetEmail);

        var (mallory, _, _) = await NewUserAsync();
        var decline = await mallory.PostAsync($"/api/v1/event-invitations/{token}/decline", null);

        var unknown = await mallory.PostAsync($"/api/v1/event-invitations/{Guid.NewGuid():N}/decline", null);
        Assert.Equal(HttpStatusCode.NotFound, decline.StatusCode);
        await AssertSameProblemAsync(unknown, decline);

        // Not used up: the recipient can still turn it down.
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));
        Assert.Equal(HttpStatusCode.NoContent, (await target.PostAsync($"/api/v1/event-invitations/{token}/decline", null)).StatusCode);
        Assert.Equal(InvitationStatus.Declined, await StatusAsync(token));
        Assert.False(await IsAdminAsync(eventId, targetId));
    }

    [Fact]
    public async Task An_admin_who_is_not_the_recipient_cannot_use_a_targeted_invitation_up()
    {
        // The "already an admin" shortcut used to mark a targeted invitation accepted for whoever
        // asked. The recipient check has to come before it.
        var (org, _, _) = await NewUserAsync();
        var eventId = await CreateEventAsync(org);
        var (_, targetId, targetEmail) = await NewUserAsync();
        var token = await InviteAsync(org, eventId, targetId, targetEmail);

        var accept = await org.PostAsync($"/api/v1/event-invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));
    }

    [Fact]
    public async Task A_shared_link_is_still_usable_by_whoever_holds_it()
    {
        var (org, _, _) = await NewUserAsync();
        var eventId = await CreateEventAsync(org);
        var link = await org.PostAsync($"/api/v1/events/{eventId}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        var (first, firstId, _) = await NewUserAsync();
        var (second, secondId, _) = await NewUserAsync();
        var (third, _, _) = await NewUserAsync();

        Assert.Equal(HttpStatusCode.OK, (await first.PostAsync($"/api/v1/event-invitations/{token}/accept", null)).StatusCode);
        // Declining a link is a no-op for everyone else.
        Assert.Equal(HttpStatusCode.NoContent, (await third.PostAsync($"/api/v1/event-invitations/{token}/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsync($"/api/v1/event-invitations/{token}/accept", null)).StatusCode);

        Assert.True(await IsAdminAsync(eventId, firstId));
        Assert.True(await IsAdminAsync(eventId, secondId));
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));
    }

    // --- helpers --------------------------------------------------------------

    /// <summary>Same status, title and detail: the refusal is the unknown-token answer, not one like it.</summary>
    private static async Task AssertSameProblemAsync(HttpResponseMessage expected, HttpResponseMessage actual)
    {
        Assert.Equal(expected.StatusCode, actual.StatusCode);
        var want = await expected.Content.ReadFromJsonAsync<JsonElement>();
        var got = await actual.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(want.GetProperty("title").GetString(), got.GetProperty("title").GetString());
        Assert.Equal(want.GetProperty("detail").GetString(), got.GetProperty("detail").GetString());
    }

    private async Task<InvitationStatus> StatusAsync(string token)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EventAdminInvitations.AsNoTracking().Where(i => i.Token == token).Select(i => i.Status).SingleAsync();
    }

    private async Task<bool> IsAdminAsync(Guid eventId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EventAdmins.AnyAsync(a => a.EventId == eventId && a.UserId == userId);
    }

    /// <summary>Invite <paramref name="targetId"/> as a co-admin and read the token from their email.</summary>
    private async Task<string> InviteAsync(HttpClient admin, Guid eventId, Guid targetId, string targetEmail)
    {
        var invite = await admin.PostAsJsonAsync($"/api/v1/events/{eventId}/invitations", new { userId = targetId });
        invite.EnsureSuccessStatusCode();

        const string marker = "/event-invite/";
        var html = _factory.EmailSender.LatestFor(targetEmail)!.HtmlBody;
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOfAny(['"', '<', ' ', '\''], start);
        return html[start..end];
    }

    private static async Task<Guid> CreateEventAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/events", new
        {
            name = "Pompfen Skills Session",
            type = "Workshop",
            description = "Online technique clinic for runners and chains.",
            startsAt = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            endsAt = DateTime.UtcNow.AddDays(30).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            locationKind = "Virtual",
            venueName = (string?)null,
            street = (string?)null,
            postalCode = (string?)null,
            location = (object?)null,
            virtualLink = "https://zoom.us/j/1234567890",
            participantMode = "Individuals",
            participationLimit = 30,
            isPaid = false,
            feeAmount = (decimal?)null,
            feeCurrency = (string?)null,
            feeRecipientName = (string?)null,
            feeIban = (string?)null,
            feePaymentDeadline = (string?)null,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Client, Guid UserId, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: AuthTestHelpers.NewHandle());
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, userId, email);
    }
}
