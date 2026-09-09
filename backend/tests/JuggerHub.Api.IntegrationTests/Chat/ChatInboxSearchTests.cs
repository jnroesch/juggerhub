using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Inbox search by people and conversation names (feature 046, amending 019 User Story 6): the
/// inbox endpoint's optional <c>q</c>. Results are the inbox's own rows, narrowed to conversations
/// in which another current member's name matches, or whose shown name matches — never message text.
/// </summary>
/// <remarks>
/// <see cref="A_name_only_in_someone_elses_conversation_returns_nothing_and_no_count"/> is SC-003
/// and the reason the predicate is appended to the inbox query rather than run separately.
/// <see cref="Message_text_never_matches"/> is SC-002 — the owner's decision, proven by request.
/// Names carry a unique token because every chat test class shares one database.
/// </remarks>
[Collection("Chat")]
public sealed class ChatInboxSearchTests : ChatTestSupport
{
    public ChatInboxSearchTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- Helpers ----------------------------------------------------------------

    private static async Task<JsonElement> SearchInboxAsync(HttpClient client, string q, int take = 20)
    {
        var resp = await client.GetAsync(
            $"/api/v1/chat/conversations?q={Uri.EscapeDataString(q)}&skip=0&take={take}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static List<Guid> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

    private static int Total(JsonElement page) => page.GetProperty("totalCount").GetInt32();

    /// <summary>A short unique token so names never collide across tests sharing the database.</summary>
    private static string Token() => Guid.NewGuid().ToString("N")[..6];

    /// <summary><c>CreateTeamAsync</c> always names the team "Rheinfeuer"; a unique name makes the name match provable.</summary>
    private async Task SetTeamNameAsync(Guid teamId, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Teams
            .Where(t => t.Id == teamId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Name, name)
                .SetProperty(t => t.ModifiedDate, DateTime.UtcNow));
    }

    private static async Task<Guid> CreateGroupAsync(HttpClient client, string name, params Guid[] members)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = members, name });
        Assert.True(resp.IsSuccessStatusCode, $"group failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    /// <summary>The auto chat for a team exists after the first inbox view; find its id there.</summary>
    private static async Task<Guid> TeamChatIdAsync(HttpClient client, Guid teamId)
    {
        var inbox = await GetInboxAsync(client);
        return inbox.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("kind").GetString() == "Team" && i.GetProperty("teamId").GetGuid() == teamId)
            .GetProperty("id").GetGuid();
    }

    // --- User Story 1: find a conversation by a person's name --------------------

    /// <summary>SC-001: every conversation the person shares with the player, across kinds, and nothing else.</summary>
    [Fact]
    public async Task Finds_the_dm_group_and_team_chat_a_member_is_in()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, lenaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, kofiId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(lenaId, $"Lena {token}");

        var dm = await StartDirectAsync(ada, lenaId);
        var group = await CreateGroupAsync(ada, "Weekend crew", lenaId, benId);
        var (teamId, _) = await CreateTeamAsync(ada);
        await AddTeamMemberAsync(teamId, lenaId);
        var teamChat = await TeamChatIdAsync(ada, teamId);

        // Noise: a DM and a group without Lena.
        var otherDm = await StartDirectAsync(ada, kofiId);
        var otherGroup = await CreateGroupAsync(ada, "No Lena here", benId, kofiId);

        var page = await SearchInboxAsync(ada, token);
        var ids = Ids(page);

        Assert.Equal(3, Total(page));
        Assert.Equal(3, ids.Count);
        Assert.Contains(dm, ids);
        Assert.Contains(group, ids);
        Assert.Contains(teamChat, ids);
        Assert.DoesNotContain(otherDm, ids);
        Assert.DoesNotContain(otherGroup, ids);
    }

    [Fact]
    public async Task A_conversation_without_a_matching_member_is_not_listed()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, kofiId, _) = await NewUserAsync();
        await StartDirectAsync(ada, kofiId);

        var page = await SearchInboxAsync(ada, Token());

        Assert.Empty(Ids(page));
        Assert.Equal(0, Total(page));
    }

    [Fact]
    public async Task Matches_on_handle_as_well_as_display_name()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, benHandle) = await NewUserAsync();
        var dm = await StartDirectAsync(ada, benId);

        // A middle slice of the generated handle — unique, and not part of any display name.
        var page = await SearchInboxAsync(ada, benHandle.Substring(4, 10));

        Assert.Equal(new[] { dm }, Ids(page));
    }

    /// <summary>SC-004: the inbox shows one page; search reaches what the page does not.</summary>
    [Fact]
    public async Task Finds_a_conversation_beyond_the_first_inbox_page()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var token = Token();

        // The oldest conversation is the one we look for; 21 newer ones push it off the default page.
        var (_, oldestPartnerId, _) = await NewUserAsync();
        await SetDisplayNameAsync(oldestPartnerId, $"Oldest {token}");
        var oldest = await SeedConversationAsync(ConversationKind.Direct, null, adaId, oldestPartnerId);

        for (var i = 0; i < 21; i++)
        {
            var (_, partnerId, _) = await NewUserAsync();
            await SeedConversationAsync(ConversationKind.Direct, null, adaId, partnerId);
        }

        var firstPage = await GetInboxAsync(ada);
        Assert.Equal(22, Total(firstPage));
        Assert.DoesNotContain(oldest, Ids(firstPage));

        var found = await SearchInboxAsync(ada, token);
        Assert.Equal(new[] { oldest }, Ids(found));
    }

    /// <summary>SC-007: without a term the inbox is exactly what it was.</summary>
    [Fact]
    public async Task No_term_returns_the_identical_inbox()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, lenaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        await StartDirectAsync(ada, lenaId);
        await StartDirectAsync(ada, benId);
        await CreateGroupAsync(ada, "Weekend crew", lenaId, benId);

        var plain = Ids(await GetInboxAsync(ada));
        var empty = Ids(await SearchInboxAsync(ada, string.Empty));

        Assert.Equal(3, plain.Count);
        Assert.Equal(plain, empty);
    }

    [Fact]
    public async Task A_one_character_term_returns_the_plain_inbox()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, lenaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        await SetDisplayNameAsync(lenaId, $"Lena {Token()}");
        await StartDirectAsync(ada, lenaId);
        await StartDirectAsync(ada, benId);

        var plain = Ids(await GetInboxAsync(ada));
        var oneChar = Ids(await SearchInboxAsync(ada, "z"));

        Assert.Equal(2, plain.Count);
        Assert.Equal(plain, oneChar);
    }

    /// <summary>Owner decision: search mirrors the inbox, so a hidden conversation stays hidden.</summary>
    [Fact]
    public async Task A_hidden_conversation_stays_hidden()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, lenaId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(lenaId, $"Lena {token}");
        var dm = await StartDirectAsync(ada, lenaId);

        Assert.Equal(new[] { dm }, Ids(await SearchInboxAsync(ada, token)));

        var hide = await ada.PatchAsJsonAsync($"/api/v1/chat/conversations/{dm}/state", new { isHidden = true });
        hide.EnsureSuccessStatusCode();

        var after = await SearchInboxAsync(ada, token);
        Assert.Empty(Ids(after));
        Assert.Equal(0, Total(after));
    }

    /// <summary>A block hides the DM from the inbox (019 FR-031) and therefore from search; shared groups stay (FR-032).</summary>
    [Fact]
    public async Task A_blocked_dm_stays_out_but_a_shared_group_stays_in()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, lenaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(lenaId, $"Lena {token}");
        var dm = await StartDirectAsync(ada, lenaId);
        var group = await CreateGroupAsync(ada, "Weekend crew", lenaId, benId);

        await BlockAsync(adaId, lenaId);

        var ids = Ids(await SearchInboxAsync(ada, token));
        Assert.DoesNotContain(dm, ids);
        Assert.Equal(new[] { group }, ids);
    }

    /// <summary>Membership is as of now: a member who left no longer makes the group match.</summary>
    [Fact]
    public async Task A_former_group_member_no_longer_matches()
    {
        var (ada, _, _) = await NewUserAsync();
        var (lena, lenaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(lenaId, $"Lena {token}");
        var group = await CreateGroupAsync(ada, "Weekend crew", lenaId, benId);

        Assert.Equal(new[] { group }, Ids(await SearchInboxAsync(ada, token)));

        var leave = await lena.DeleteAsync($"/api/v1/chat/conversations/{group}/members/me");
        leave.EnsureSuccessStatusCode();

        Assert.Empty(Ids(await SearchInboxAsync(ada, token)));
    }

    /// <summary>The player is in every one of their conversations; their own name must not list the whole inbox.</summary>
    [Fact]
    public async Task Your_own_name_does_not_list_every_conversation()
    {
        var (ada, adaId, _) = await NewUserAsync();
        var (_, kofiId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(adaId, $"Ada {token}");
        await StartDirectAsync(ada, kofiId);
        await CreateGroupAsync(ada, "Weekend crew", kofiId, benId);

        var page = await SearchInboxAsync(ada, token);

        Assert.Empty(Ids(page));
        Assert.Equal(0, Total(page));
    }

    /// <summary>Accent- and case-insensitive, matching feature 007's convention.</summary>
    [Fact]
    public async Task Matching_is_accent_and_case_insensitive()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, joergId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(joergId, $"Jörg {token}");
        var dm = await StartDirectAsync(ada, joergId);

        Assert.Equal(new[] { dm }, Ids(await SearchInboxAsync(ada, "jorg")));
        Assert.Equal(new[] { dm }, Ids(await SearchInboxAsync(ada, "JÖRG")));
        Assert.Equal(new[] { dm }, Ids(await SearchInboxAsync(ada, token.ToUpperInvariant())));
    }

    /// <summary>
    /// <b>SC-003.</b> Driving the API directly: a name that exists only in a conversation the searcher
    /// is not in returns zero results — and no count that would hint it exists.
    /// </summary>
    [Fact]
    public async Task A_name_only_in_someone_elses_conversation_returns_nothing_and_no_count()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, lenaId, _) = await NewUserAsync();
        var (mallory, _, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(lenaId, $"Lena {token}");
        var dm = await StartDirectAsync(ada, lenaId);

        Assert.Equal(new[] { dm }, Ids(await SearchInboxAsync(ada, token)));

        var forMallory = await SearchInboxAsync(mallory, token);
        Assert.Empty(Ids(forMallory));
        Assert.Equal(0, Total(forMallory));
    }

    // --- User Story 2: message text is never searched ------------------------------

    /// <summary><b>SC-002.</b> A term that lives only inside a message matches nothing.</summary>
    [Fact]
    public async Task Message_text_never_matches()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, kofiId, _) = await NewUserAsync();
        var dm = await StartDirectAsync(ada, kofiId);
        var token = Token();
        await SendAsync(ada, dm, $"the code is {token}");

        var page = await SearchInboxAsync(ada, token);

        Assert.Empty(Ids(page));
        Assert.Equal(0, Total(page));
    }

    // --- User Story 3: find a conversation by its own name ---------------------------

    [Fact]
    public async Task Finds_a_group_by_its_name()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, kofiId, _) = await NewUserAsync();
        var token = Token();
        var group = await CreateGroupAsync(ada, $"Trip {token}", benId, kofiId);
        await CreateGroupAsync(ada, "Weekend crew", benId, kofiId);

        var page = await SearchInboxAsync(ada, token);

        Assert.Equal(new[] { group }, Ids(page));
        Assert.Equal(1, Total(page));
    }

    [Fact]
    public async Task Finds_a_team_chat_by_the_teams_name()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var token = Token();
        var (teamId, _) = await CreateTeamAsync(ada);
        await SetTeamNameAsync(teamId, $"Hamburg {token}");
        var teamChat = await TeamChatIdAsync(ada, teamId);
        await StartDirectAsync(ada, benId);

        var page = await SearchInboxAsync(ada, token);

        Assert.Equal(new[] { teamChat }, Ids(page));
    }

    /// <summary>
    /// Admin-contact threads (feature 027): the requester sees the team's name; the admin sees the
    /// requester's name with the team. Each side finds the thread by what it actually sees.
    /// </summary>
    [Fact]
    public async Task Finds_an_admin_contact_thread_by_team_name_and_by_requester_name()
    {
        var (ada, _, _) = await NewUserAsync();
        var (pat, patId, _) = await NewUserAsync();
        var teamToken = Token();
        var patToken = Token();
        await SetDisplayNameAsync(patId, $"Pat {patToken}");
        var (teamId, _) = await CreateTeamAsync(ada);
        await SetTeamNameAsync(teamId, $"Hamburg {teamToken}");
        var teamChat = await TeamChatIdAsync(ada, teamId);

        var contact = await pat.PostAsJsonAsync($"/api/v1/chat/contact/team/{teamId}/messages", new { body = "hi admins" });
        Assert.True(contact.IsSuccessStatusCode, $"contact failed: {(int)contact.StatusCode} {await contact.Content.ReadAsStringAsync()}");
        var thread = (await contact.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("conversation").GetProperty("id").GetGuid();

        // The admin: by the requester's name, and by the team's name (which also lists the team chat).
        Assert.Equal(new[] { thread }, Ids(await SearchInboxAsync(ada, patToken)));
        var byTeam = Ids(await SearchInboxAsync(ada, teamToken));
        Assert.Equal(2, byTeam.Count);
        Assert.Contains(thread, byTeam);
        Assert.Contains(teamChat, byTeam);

        // The requester: by the team's name.
        Assert.Equal(new[] { thread }, Ids(await SearchInboxAsync(pat, teamToken)));
    }

    [Fact]
    public async Task A_conversation_matching_by_name_and_member_is_listed_once()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, lenaId, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var token = Token();
        await SetDisplayNameAsync(lenaId, $"Lena {token}");
        var group = await CreateGroupAsync(ada, $"Lena's crew {token}", lenaId, benId);

        var page = await SearchInboxAsync(ada, token);

        Assert.Equal(new[] { group }, Ids(page));
        Assert.Equal(1, Total(page));
    }

    /// <summary>"Group" and "Team chat" are presentation defaults, not names anyone chose (research §2).</summary>
    [Fact]
    public async Task Fallback_labels_are_not_names()
    {
        var (ada, _, _) = await NewUserAsync();
        var (_, benId, _) = await NewUserAsync();
        var (_, kofiId, _) = await NewUserAsync();
        await CreateGroupAsync(ada, "Weekend crew", benId, kofiId);
        var (teamId, _) = await CreateTeamAsync(ada);
        await SetTeamNameAsync(teamId, $"Rheinfeuer {Token()}");
        await TeamChatIdAsync(ada, teamId);

        Assert.Empty(Ids(await SearchInboxAsync(ada, "group")));
        Assert.Empty(Ids(await SearchInboxAsync(ada, "team chat")));
    }
}
