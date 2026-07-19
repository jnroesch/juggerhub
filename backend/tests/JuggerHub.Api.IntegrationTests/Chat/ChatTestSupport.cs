using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>Shares one Testcontainers Postgres + host across all chat test classes.</summary>
[CollectionDefinition("Chat")]
public sealed class ChatCollection : ICollectionFixture<JuggerHubApiFactory>;

/// <summary>
/// Shared helpers for the chat (019) integration tests: user/team seeding and the common
/// start-a-chat / send-a-message calls against the real API + Postgres container.
/// </summary>
public abstract class ChatTestSupport
{
    protected JuggerHubApiFactory Factory { get; }

    protected ChatTestSupport(JuggerHubApiFactory factory) => Factory = factory;

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected async Task<(HttpClient Client, Guid UserId, string Handle)> NewUserAsync()
    {
        var client = Factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, Factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle);
    }

    protected async Task<(Guid TeamId, string Slug)> CreateTeamAsync(HttpClient adminClient)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var resp = await adminClient.PostAsJsonAsync("/api/v1/teams",
            new { name = "Rheinfeuer", slug, type = "CityTeam", city = "Köln" });
        Assert.True(resp.IsSuccessStatusCode, $"create team failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teamId = await db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).FirstAsync();
        return (teamId, slug);
    }

    protected async Task AddTeamMemberAsync(Guid teamId, Guid userId, TeamRole role = TeamRole.Member)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = userId, Role = role, JoinedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    protected async Task RemoveTeamMemberAsync(Guid teamId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TeamMemberships
            .Where(m => m.TeamId == teamId && m.UserId == userId)
            .ExecuteDeleteAsync();
    }

    /// <summary>Create a conversation directly in the database (bypassing rate limits for setup).</summary>
    protected async Task<Guid> SeedConversationAsync(ConversationKind kind, Guid? teamId, params Guid[] userIds)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversation = new Conversation
        {
            Kind = kind,
            Name = kind == ConversationKind.Group ? "Weekend crew" : null,
            TeamId = teamId,
            DirectPairKey = kind == ConversationKind.Direct && userIds.Length == 2
                ? Conversation.BuildDirectPairKey(userIds[0], userIds[1])
                : null,
        };
        db.Conversations.Add(conversation);

        foreach (var id in userIds)
        {
            db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = id,
                JoinedDate = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return conversation.Id;
    }

    protected static async Task<Guid> StartDirectAsync(HttpClient client, Guid otherUserId)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { otherUserId }, name = (string?)null });
        Assert.True(resp.IsSuccessStatusCode, $"start failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
    }

    protected static async Task<Guid> SendAsync(HttpClient client, Guid conversationId, string body)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/messages", new { body });
        Assert.True(resp.IsSuccessStatusCode, $"send failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return created.GetProperty("id").GetGuid();
    }

    protected static async Task<JsonElement> GetMessagesAsync(HttpClient client, Guid conversationId)
    {
        var resp = await client.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    protected static async Task<int> GetUnreadTotalAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/v1/chat/conversations/unread-count");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("unreadCount").GetInt32();
    }

    protected static async Task<JsonElement> GetInboxAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/v1/chat/conversations");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    protected static async Task MarkReadAsync(HttpClient client, Guid conversationId, Guid messageId)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/chat/conversations/{conversationId}/read",
            new { lastReadMessageId = messageId });
        resp.EnsureSuccessStatusCode();
    }

    protected async Task BlockAsync(Guid blockerUserId, Guid blockedUserId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserBlocks.Add(new UserBlock { BlockerUserId = blockerUserId, BlockedUserId = blockedUserId });
        await db.SaveChangesAsync();
    }
}
