using System.Net;
using System.Net.Http.Json;
using JuggerHub.Common;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>
/// Feature 037 T043/T044 — User Story 3, verified from a <b>different account's</b> point of view:
/// everything that used to show the departed member still renders, under the neutral placeholder.
/// </summary>
/// <remarks>
/// Erasure that leaves other people's screens broken is not shippable, and these are the surfaces
/// where the risk actually lives — each one projects the author's profile through a left join that
/// now returns nothing. The assertions are deliberately "renders AND shows the placeholder AND does
/// not contain the name", because any one of those alone would pass while the page was wrong.
/// </remarks>
[Collection("AccountDeletion")]
public sealed class PostErasureReadPathTests : AccountDeletionTestSupport
{
    public PostErasureReadPathTests(JuggerHubApiFactory factory) : base(factory) { }

    private const string LeaverName = "Ada Kowalczyk";

    /// <summary>A member with a distinctive display name, plus a second member who stays behind.</summary>
    private async Task<(HttpClient Leaver, Guid LeaverId, HttpClient Keeper, Guid KeeperId)> PairAsync()
    {
        var (leaver, leaverId, _, _) = await NewMemberAsync();
        var (keeper, keeperId, _, _) = await NewMemberAsync();
        (await leaver.PutAsJsonAsync("/api/v1/profiles/me", new { displayName = LeaverName }))
            .EnsureSuccessStatusCode();
        return (leaver, leaverId, keeper, keeperId);
    }

    [Fact]
    public async Task A_team_news_post_survives_its_author_and_reads_under_the_placeholder()
    {
        var (leaver, leaverId, keeper, keeperId) = await PairAsync();

        var teamId = await CreateTeamWithSoleAdminAsync(leaverId);
        await AddTeamAdminAsync(teamId, keeperId);

        var slug = await WithDbAsync(db => db.Teams.AsNoTracking()
            .Where(t => t.Id == teamId).Select(t => t.Slug).SingleAsync());

        (await leaver.PostAsJsonAsync($"/api/v1/teams/{slug}/news",
            new { body = "Pitch is booked for Saturday." })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        var feed = await keeper.GetAsync($"/api/v1/teams/{slug}/news");
        feed.EnsureSuccessStatusCode();
        var body = await feed.Content.ReadAsStringAsync();

        // The post is a record the team relies on, so it stays verbatim…
        Assert.Contains("Pitch is booked for Saturday.", body);
        // …with no author, and no way back to who wrote it.
        Assert.Contains(MemberPlaceholder.English, body);
        Assert.DoesNotContain(LeaverName, body);
    }

    [Fact]
    public async Task A_team_roster_renders_and_no_longer_lists_the_departed_member()
    {
        var (leaver, leaverId, keeper, keeperId) = await PairAsync();

        var teamId = await CreateTeamWithSoleAdminAsync(leaverId);
        await AddTeamAdminAsync(teamId, keeperId);

        var slug = await WithDbAsync(db => db.Teams.AsNoTracking()
            .Where(t => t.Id == teamId).Select(t => t.Slug).SingleAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        var roster = await keeper.GetAsync($"/api/v1/teams/{slug}/members");
        roster.EnsureSuccessStatusCode();

        var body = await roster.Content.ReadAsStringAsync();
        Assert.DoesNotContain(LeaverName, body);
    }

    [Fact]
    public async Task A_notification_whose_actor_left_still_renders()
    {
        var (leaver, leaverId, keeper, keeperId) = await PairAsync();

        await WithDbAsync(async db =>
        {
            db.Notifications.Add(new Notification
            {
                RecipientUserId = keeperId,
                ActorUserId = leaverId,
                Type = NotificationType.TeamNews,
                Payload = """{"teamName":"Rheinfeuer","excerpt":"posted team news"}""",
            });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // Renders at all — the point is that losing the actor does not break the recipient's list.
        var list = await keeper.GetAsync("/api/v1/notifications");
        list.EnsureSuccessStatusCode();

        // How the payload is rendered belongs to feature 011; what belongs to 037 is that the
        // departed member is not in it.
        Assert.DoesNotContain(LeaverName, await list.Content.ReadAsStringAsync());
        Assert.Equal(1, await WithDbAsync(db => db.Notifications.CountAsync(n => n.RecipientUserId == keeperId)));

        // The actor id SURVIVES, and that is correct even though the FK is declared SetNull.
        // SetNull fires when the referenced ROW is deleted, and erasure never deletes the account row
        // — it neutralises it. So this behaves exactly like ChatMessage.SenderId: a live reference to
        // an account that identifies nobody. Asserted rather than assumed, because the plan
        // originally (wrongly) described this FK as nulling itself.
        var actorId = await WithDbAsync(db => db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == keeperId)
            .Select(n => n.ActorUserId)
            .FirstAsync());

        Assert.Equal(leaverId, actorId);

        // …and following it reaches nothing identifying, which is the requirement (FR-023).
        Assert.False(await WithDbAsync(db => db.PlayerProfiles.IgnoreQueryFilters()
            .AnyAsync(p => p.UserId == actorId)));
    }

    [Fact]
    public async Task The_departed_members_public_profile_and_search_return_nothing()
    {
        var (leaver, leaverId, keeper, _) = await PairAsync();

        var handle = await WithDbAsync(db => db.PlayerProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.UserId == leaverId).Select(p => p.Handle).SingleAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // Direct link: gone, and indistinguishable from a handle that never existed.
        var profile = await keeper.GetAsync($"/api/v1/profiles/{handle}");
        Assert.Equal(HttpStatusCode.NotFound, profile.StatusCode);

        // Search: gone.
        var search = await keeper.GetAsync($"/api/v1/profiles?q={Uri.EscapeDataString(LeaverName)}");
        search.EnsureSuccessStatusCode();
        Assert.DoesNotContain(LeaverName, await search.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Nothing_survives_from_which_the_author_could_be_recovered()
    {
        var (leaver, leaverId, _, keeperId) = await PairAsync();

        var teamId = await CreateTeamWithSoleAdminAsync(leaverId);
        await AddTeamAdminAsync(teamId, keeperId);

        await WithDbAsync(async db =>
        {
            db.TeamNewsPosts.Add(new TeamNewsPost
            {
                TeamId = teamId,
                AuthorUserId = leaverId,
                Body = "See you all Saturday.",
            });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // The retained post still points at the account row — that is what keeps referential
        // integrity — but following the reference reaches nothing that identifies a person (SC-005).
        await WithDbAsync(async db =>
        {
            var author = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == leaverId)
                .Select(u => new { u.Email, u.NormalizedEmail, u.UserName, u.PhoneNumber })
                .SingleAsync();

            Assert.Null(author.Email);
            Assert.Null(author.NormalizedEmail);
            Assert.Null(author.PhoneNumber);
            Assert.DoesNotContain("Ada", author.UserName!, StringComparison.OrdinalIgnoreCase);

            // And no profile row anywhere, filtered or not.
            Assert.False(await db.PlayerProfiles.IgnoreQueryFilters().AnyAsync(p => p.UserId == leaverId));
        });
    }
}
