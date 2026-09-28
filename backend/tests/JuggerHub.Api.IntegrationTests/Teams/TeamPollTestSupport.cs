using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Data;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Shared helpers for the team-poll suites (feature 062): players, a team with members, and one method
/// per poll endpoint. Lists come back as both the parsed JSON and the <b>raw body</b>, because the
/// privacy tests search the body itself — a typed DTO would hide a field the server should never have
/// sent.
/// </summary>
public abstract class TeamPollTestSupport
{
    protected readonly JuggerHubApiFactory Factory;

    protected TeamPollTestSupport(JuggerHubApiFactory factory) => Factory = factory;

    protected sealed record Player(HttpClient Client, Guid Id, string Handle, string Email);

    /// <summary>A team: its address, unique name, the admin who created it, and the members who joined it.</summary>
    protected sealed record PollTeam(string Slug, string Name, Player Admin, IReadOnlyList<Player> Members);

    /// <summary>A list response: the polls, and the body exactly as it arrived.</summary>
    protected sealed record PollList(List<JsonElement> Items, string RawBody);

    protected async Task<Player> NewUserAsync()
    {
        var client = Factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, Factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return new Player(client, userId, handle, email);
    }

    /// <summary>A new team with its admin and <paramref name="members"/> plain members, all joined through the invite link.</summary>
    protected async Task<PollTeam> TeamWithMembersAsync(int members)
    {
        var admin = await NewUserAsync();
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var name = "Poll Team " + Guid.NewGuid().ToString("N")[..6];
        (await admin.Client.PostAsJsonAsync("/api/v1/teams", new
        {
            name,
            slug,
            type = "CityTeam",
            location = new { cityExternalId = "TEST:berlin" },
        })).EnsureSuccessStatusCode();

        var joined = new List<Player>();
        for (var i = 0; i < members; i++)
        {
            var member = await NewUserAsync();
            await JoinAsync(admin, slug, member);
            joined.Add(member);
        }

        return new PollTeam(slug, name, admin, joined);
    }

    protected static async Task JoinAsync(Player admin, string slug, Player joiner)
    {
        var link = await admin.Client.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        (await joiner.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).EnsureSuccessStatusCode();
    }

    protected static async Task LeaveAsync(Player player, string slug) =>
        (await player.Client.DeleteAsync($"/api/v1/teams/{slug}/members/{player.Id}")).EnsureSuccessStatusCode();

    protected static async Task SetRoleAsync(Player admin, string slug, Player target, string role) =>
        (await admin.Client.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{target.Id}/role", new { role }))
            .EnsureSuccessStatusCode();

    protected static async Task SetLanguageAsync(Player player, string language) =>
        (await player.Client.PutAsJsonAsync("/api/v1/account/language", new { language })).EnsureSuccessStatusCode();

    protected static async Task SetPreferenceAsync(Player player, string category, string channel, bool enabled) =>
        (await player.Client.PutAsJsonAsync($"/api/v1/notification-preferences/{category}/{channel}", new { enabled }))
            .EnsureSuccessStatusCode();

    // --- Poll endpoints -------------------------------------------------------------------------------

    protected static Task<HttpResponseMessage> PostPollAsync(
        Player actor,
        string slug,
        string? question = "Thursday instead of Tuesday this week?",
        IReadOnlyList<string?>? options = null,
        bool allowsMultiple = false,
        bool isAnonymous = false,
        bool resultsAfterAnswer = false,
        DateTimeOffset? closesAt = null) =>
        actor.Client.PostAsJsonAsync($"/api/v1/teams/{slug}/polls", new
        {
            question,
            options = options ?? ["Thursday works", "Stay on Tuesday", "Either is fine"],
            allowsMultiple,
            isAnonymous,
            resultsAfterAnswer,
            closesAt,
        });

    /// <summary>Start a poll that must succeed; returns the poll as its author sees it.</summary>
    protected static async Task<JsonElement> CreatePollAsync(
        Player admin,
        string slug,
        string question = "Thursday instead of Tuesday this week?",
        IReadOnlyList<string?>? options = null,
        bool allowsMultiple = false,
        bool isAnonymous = false,
        bool resultsAfterAnswer = false,
        DateTimeOffset? closesAt = null)
    {
        var resp = await PostPollAsync(admin, slug, question, options, allowsMultiple, isAnonymous, resultsAfterAnswer, closesAt);
        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected static async Task<PollList> ListAsync(Player reader, string slug, string state = "open", int take = 10)
    {
        var resp = await reader.Client.GetAsync($"/api/v1/teams/{slug}/polls?state={state}&take={take}");
        resp.EnsureSuccessStatusCode();
        var raw = await resp.Content.ReadAsStringAsync();
        var items = JsonDocument.Parse(raw).RootElement.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
        return new PollList(items, raw);
    }

    /// <summary>One poll as <paramref name="reader"/> sees it in the open or closed list.</summary>
    protected static async Task<JsonElement> ViewAsync(Player reader, string slug, Guid pollId, string state = "open") =>
        (await ListAsync(reader, slug, state, 100)).Items.Single(p => PollId(p) == pollId);

    protected static Task<HttpResponseMessage> AnswerAsync(Player voter, string slug, Guid pollId, params Guid[] optionIds) =>
        voter.Client.PutAsJsonAsync($"/api/v1/teams/{slug}/polls/{pollId}/answer", new { optionIds });

    protected static Task<HttpResponseMessage> WithdrawAsync(Player voter, string slug, Guid pollId) =>
        voter.Client.DeleteAsync($"/api/v1/teams/{slug}/polls/{pollId}/answer");

    protected static Task<HttpResponseMessage> CloseAsync(Player actor, string slug, Guid pollId) =>
        actor.Client.PostAsync($"/api/v1/teams/{slug}/polls/{pollId}/close", null);

    protected static Task<HttpResponseMessage> UpdateAsync(Player actor, string slug, Guid pollId, object body) =>
        actor.Client.PutAsJsonAsync($"/api/v1/teams/{slug}/polls/{pollId}", body);

    protected static Task<HttpResponseMessage> DeleteAsync(Player actor, string slug, Guid pollId) =>
        actor.Client.DeleteAsync($"/api/v1/teams/{slug}/polls/{pollId}");

    // --- Reading responses ------------------------------------------------------------------------------

    protected static Guid PollId(JsonElement poll) => Guid.Parse(poll.GetProperty("id").GetString()!);

    /// <summary>The poll's option ids, in the admin's order.</summary>
    protected static Guid[] OptionIds(JsonElement poll) =>
        poll.GetProperty("options").EnumerateArray().Select(o => Guid.Parse(o.GetProperty("id").GetString()!)).ToArray();

    /// <summary>The count shown for the option at <paramref name="index"/>, or null when it is hidden.</summary>
    protected static int? CountOf(JsonElement poll, int index)
    {
        var count = poll.GetProperty("options")[index].GetProperty("count");
        return count.ValueKind == JsonValueKind.Null ? null : count.GetInt32();
    }

    /// <summary>The handles listed under the option at <paramref name="index"/>, or null when none may be shown.</summary>
    protected static string?[]? VoterHandles(JsonElement poll, int index)
    {
        var voters = poll.GetProperty("options")[index].GetProperty("voters");
        return voters.ValueKind == JsonValueKind.Null
            ? null
            : voters.EnumerateArray().Select(v => v.GetProperty("handle").GetString()).ToArray();
    }

    protected static string?[]? NotAnsweredHandles(JsonElement poll)
    {
        var list = poll.GetProperty("notAnswered");
        return list.ValueKind == JsonValueKind.Null
            ? null
            : list.EnumerateArray().Select(v => v.GetProperty("handle").GetString()).ToArray();
    }

    protected static async Task<string?> ProblemCodeAsync(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    protected static async Task<string?> ProblemTitleAsync(HttpResponseMessage resp) =>
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();

    protected Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action) => HomeTestSupport.WithDbAsync(Factory, action);

    protected Task WithDbAsync(Func<AppDbContext, Task> action) => HomeTestSupport.WithDbAsync(Factory, action);
}
