using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Parties;

/// <summary>
/// A targeted party co-admin invitation is bound to its recipient (GH #401): holding the token does
/// not make another team member a party admin, and does not let anyone use the invitation up. A
/// shared link stays a bearer credential among the party's team. Exercises the real API + Postgres
/// container.
/// </summary>
[Collection("Parties")]
public sealed class PartyInviteRecipientTests : PartyTestSupport
{
    public PartyInviteRecipientTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Another_team_member_cannot_accept_a_targeted_invitation()
    {
        var seed = await SeedAsync();
        var (other, otherId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(seed.TeamId, otherId);

        var accept = await other.PostAsync($"/api/v1/party-invitations/{seed.Token}/accept", null);

        // Refused exactly as a token that does not exist is, so the answer confirms nothing.
        var unknown = await other.PostAsync($"/api/v1/party-invitations/{Guid.NewGuid():N}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        await AssertSameProblemAsync(unknown, accept);

        Assert.False(await IsPartyAdminAsync(seed.PartyId, otherId));
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(seed.Token));

        // The recipient is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await seed.Target.PostAsync($"/api/v1/party-invitations/{seed.Token}/accept", null)).StatusCode);
        Assert.True(await IsPartyAdminAsync(seed.PartyId, seed.TargetId));
        Assert.Equal(InvitationStatus.Accepted, await StatusAsync(seed.Token));
    }

    [Fact]
    public async Task An_outsider_is_told_the_invitation_does_not_exist_not_that_they_are_off_the_team()
    {
        // "Only a member of the party's team can co-run it" (403) would confirm the token is real.
        var seed = await SeedAsync();
        var (outsider, _, _, _) = await NewUserAsync();

        var accept = await outsider.PostAsync($"/api/v1/party-invitations/{seed.Token}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(seed.Token));
    }

    [Fact]
    public async Task Another_account_cannot_decline_a_targeted_invitation()
    {
        var seed = await SeedAsync();
        var (other, otherId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(seed.TeamId, otherId);

        var decline = await other.PostAsync($"/api/v1/party-invitations/{seed.Token}/decline", null);

        var unknown = await other.PostAsync($"/api/v1/party-invitations/{Guid.NewGuid():N}/decline", null);
        Assert.Equal(HttpStatusCode.NotFound, decline.StatusCode);
        await AssertSameProblemAsync(unknown, decline);

        // Not used up: the recipient can still turn it down.
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(seed.Token));
        Assert.Equal(HttpStatusCode.NoContent, (await seed.Target.PostAsync($"/api/v1/party-invitations/{seed.Token}/decline", null)).StatusCode);
        Assert.Equal(InvitationStatus.Declined, await StatusAsync(seed.Token));
        Assert.False(await IsPartyAdminAsync(seed.PartyId, seed.TargetId));
    }

    [Fact]
    public async Task A_party_admin_who_is_not_the_recipient_cannot_use_a_targeted_invitation_up()
    {
        // The "already a party admin" shortcut used to mark a targeted invitation accepted for
        // whoever asked. The recipient check has to come before it.
        var seed = await SeedAsync();

        var accept = await seed.Admin.PostAsync($"/api/v1/party-invitations/{seed.Token}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(seed.Token));
    }

    [Fact]
    public async Task A_shared_link_is_still_usable_by_any_team_member_who_holds_it()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);
        var link = await admin.PostAsync($"/api/v1/parties/{partyId}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        var (first, firstId, _, _) = await NewUserAsync();
        var (second, secondId, _, _) = await NewUserAsync();
        var (third, thirdId, _, _) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, firstId);
        await AddTeamMemberAsync(teamId, secondId);
        await AddTeamMemberAsync(teamId, thirdId);

        Assert.Equal(HttpStatusCode.OK, (await first.PostAsync($"/api/v1/party-invitations/{token}/accept", null)).StatusCode);
        // Declining a link is a no-op for everyone else.
        Assert.Equal(HttpStatusCode.NoContent, (await third.PostAsync($"/api/v1/party-invitations/{token}/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsync($"/api/v1/party-invitations/{token}/accept", null)).StatusCode);

        Assert.True(await IsPartyAdminAsync(partyId, firstId));
        Assert.True(await IsPartyAdminAsync(partyId, secondId));
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));
    }

    // --- helpers --------------------------------------------------------------

    private sealed record Seed(HttpClient Admin, HttpClient Target, Guid TargetId, Guid TeamId, Guid PartyId, string Token);

    /// <summary>A party whose admin has sent one team member a targeted co-admin invitation.</summary>
    private async Task<Seed> SeedAsync()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (teamId, _) = await CreateTeamAsync(admin);
        var (target, targetId, _, targetEmail) = await NewUserAsync();
        await AddTeamMemberAsync(teamId, targetId);
        var eventId = await CreateTeamsEventAsync(admin);
        var partyId = await FormPartyAsync(admin, eventId, teamId);

        var invite = await admin.PostAsJsonAsync($"/api/v1/parties/{partyId}/invitations", new { userId = targetId });
        invite.EnsureSuccessStatusCode();
        var token = ExtractPartyInviteToken(Factory.EmailSender.LatestFor(targetEmail)!.HtmlBody);
        return new Seed(admin, target, targetId, teamId, partyId, token);
    }

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
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PartyAdminInvitations.AsNoTracking().Where(i => i.Token == token).Select(i => i.Status).SingleAsync();
    }

    private async Task<bool> IsPartyAdminAsync(Guid partyId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PartyMembers.AnyAsync(m => m.PartyId == partyId && m.UserId == userId && m.Role == PartyMemberRole.Admin);
    }
}
