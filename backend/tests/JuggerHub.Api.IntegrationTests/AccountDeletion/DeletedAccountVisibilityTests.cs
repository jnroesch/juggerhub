using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Chat;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>
/// Feature 037 T012 — <b>the Phase 2 gate.</b> One test per predicate site that decides whether
/// an account is visible or contactable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this suite exists.</b> Seven predicates in the codebase were written as
/// <c>Status != AccountStatus.Banned</c>. Adding <see cref="AccountStatus.Deleted"/> silently
/// satisfies every one of them — the code compiles, every pre-existing test stays green, and an
/// erased account reads as a live one. Four of the seven turned out to be safe only
/// <i>incidentally</i> (they filter a row that cascades away with the profile); three queried
/// <c>Users</c> directly and genuinely failed open. Inspection cannot tell those groups apart,
/// which is why each site gets its own assertion here rather than a single happy-path test.
/// </para>
/// <para>
/// <b>These tests do not use the deletion service.</b> They construct the erased end-state
/// directly — delete the <see cref="PlayerProfile"/>, set the status — because they assert the
/// behaviour of the <i>predicates</i>, not of the service that will arrive in Phase 3. That also
/// makes them runnable before any of that code exists, which is the point of a gate.
/// </para>
/// <para>
/// <b>Suspended is the control.</b> Every case pairs the erased assertion with a suspended one.
/// A predicate rewritten as a blunt "Active only" would exclude suspended accounts too and pass
/// the erased half of each test while quietly breaking feature 013, which states that suspension
/// refuses sign-in and touches nothing else.
/// </para>
/// </remarks>
[Collection("Chat")]
public sealed class DeletedAccountVisibilityTests : ChatTestSupport
{
    public DeletedAccountVisibilityTests(JuggerHubApiFactory factory) : base(factory) { }

    // --- helpers ---------------------------------------------------------------

    /// <summary>
    /// Puts an account into the erased end-state the same way the Phase 3 service will leave it:
    /// profile row gone, identity columns released, status terminal.
    /// </summary>
    private async Task EraseAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.PlayerProfiles.IgnoreQueryFilters()
            .Where(p => p.UserId == userId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Status, AccountStatus.Deleted)
                .SetProperty(u => u.StatusChangedAt, DateTime.UtcNow)
                .SetProperty(u => u.Email, (string?)null)
                .SetProperty(u => u.NormalizedEmail, (string?)null));
    }

    private async Task SetStatusAsync(Guid userId, AccountStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Status, status)
                .SetProperty(u => u.StatusChangedAt, DateTime.UtcNow));
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // --- sites 1-4: the four global query filters ------------------------------

    /// <summary>
    /// Site 1 — <c>PlayerProfile</c> query filter (AppDbContext). Safe unchanged, but only
    /// because erasure deletes the row the filter would evaluate. Assert the consequence.
    /// </summary>
    [Fact]
    public async Task Site1_player_profile_is_absent_after_erasure()
    {
        var (_, userId, handle) = await NewUserAsync();
        Assert.True(await WithDbAsync(db => db.PlayerProfiles.AnyAsync(p => p.UserId == userId)));

        await EraseAsync(userId);

        // Gone from the filtered set AND from the unfiltered one — this is a deletion, not a hide.
        Assert.False(await WithDbAsync(db => db.PlayerProfiles.AnyAsync(p => p.UserId == userId)));
        Assert.False(await WithDbAsync(db =>
            db.PlayerProfiles.IgnoreQueryFilters().AnyAsync(p => p.UserId == userId)));

        // The public profile route no longer resolves the handle.
        var anon = Factory.CreateClient();
        var resp = await anon.GetAsync($"/api/v1/profiles/{handle}");
        Assert.False(resp.IsSuccessStatusCode);
    }

    /// <summary>Site 2 — <c>ProfilePompfe</c> filter: rows cascade with the profile.</summary>
    [Fact]
    public async Task Site2_profile_pompfen_are_absent_after_erasure()
    {
        var (client, userId, handle) = await NewUserAsync();
        // DisplayName is [Required] on UpdateProfileRequest — a pompfen-only body is a 400.
        var update = await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = handle,
            pompfen = new[] { "Langpompfe", "Schild" },
        });
        Assert.True(update.IsSuccessStatusCode,
            $"profile update failed: {(int)update.StatusCode} {await update.Content.ReadAsStringAsync()}");
        Assert.True(await WithDbAsync(db =>
            db.ProfilePompfen.IgnoreQueryFilters().AnyAsync(pp => pp.Profile.UserId == userId)));

        await EraseAsync(userId);

        Assert.False(await WithDbAsync(db =>
            db.ProfilePompfen.IgnoreQueryFilters().AnyAsync(pp => pp.Profile.UserId == userId)));
    }

    /// <summary>Site 3 — <c>ProfileAvatar</c> filter: the avatar cascades with the profile.</summary>
    [Fact]
    public async Task Site3_profile_avatar_is_absent_after_erasure()
    {
        var (_, userId, _) = await NewUserAsync();
        await WithDbAsync(async db =>
        {
            var profileId = await db.PlayerProfiles.IgnoreQueryFilters()
                .Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync();
            // Feature 035 is merged: the row is a DESCRIPTOR pointing at an object in blob
            // storage, not the bytes. Deleting it therefore erases the pointer and NOT the
            // image — which is exactly why FR-015 needs a real reclaim step (T026), not the
            // no-op seam the plan originally assumed.
            db.ProfileAvatars.Add(new ProfileAvatar
            {
                ProfileId = profileId,
                ContentType = "image/webp",
                ObjectKey = $"avatars/{Guid.NewGuid():N}.webp",
                SizeBytes = 1024,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await EraseAsync(userId);

        Assert.False(await WithDbAsync(db =>
            db.ProfileAvatars.IgnoreQueryFilters().AnyAsync(a => a.Profile.UserId == userId)));
    }

    /// <summary>Site 4 — <c>EventParticipation</c> filter: participations cascade with the profile.</summary>
    [Fact]
    public async Task Site4_event_participation_is_absent_after_erasure()
    {
        var (_, userId, _) = await NewUserAsync();
        await WithDbAsync(async db =>
        {
            var profileId = await db.PlayerProfiles.IgnoreQueryFilters()
                .Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync();
            var ev = new Event { Name = "Turnier", StartsAt = DateTime.UtcNow.AddDays(7) };
            db.Events.Add(ev);
            db.EventParticipations.Add(new EventParticipation
            {
                ProfileId = profileId,
                Event = ev,
                TeamLabel = "Guest",
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await EraseAsync(userId);

        Assert.False(await WithDbAsync(db =>
            db.EventParticipations.IgnoreQueryFilters().AnyAsync(ep => ep.Profile.UserId == userId)));
    }

    // --- sites 5-7: the three that queried Users directly and FAILED OPEN ------

    /// <summary>
    /// Site 5 — <c>ChatConversationService.StartAsync</c> participant validation. This one had no
    /// query-filter backstop: it reads <c>Users</c> directly, so an erased account read as a
    /// perfectly good conversation participant until the predicate was made positive.
    /// </summary>
    [Fact]
    public async Task Site5_erased_account_cannot_be_added_to_a_group_conversation()
    {
        var (starter, _, _) = await NewUserAsync();
        var (_, keeperId, _) = await NewUserAsync();
        var (_, erasedId, _) = await NewUserAsync();

        await EraseAsync(erasedId);

        var resp = await starter.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { keeperId, erasedId }, name = "Weekend crew" });

        Assert.False(resp.IsSuccessStatusCode);
    }

    /// <summary>
    /// Site 5 control — a <b>suspended</b> account must still be reachable. Feature 013 says
    /// suspension refuses sign-in and touches nothing else; a predicate narrowed to "Active only"
    /// would pass the erased case above while silently breaking that.
    /// </summary>
    [Fact]
    public async Task Site5_control_suspended_account_is_still_a_valid_participant()
    {
        var (starter, _, _) = await NewUserAsync();
        var (_, keeperId, _) = await NewUserAsync();
        var (_, suspendedId, _) = await NewUserAsync();

        await SetStatusAsync(suspendedId, AccountStatus.Suspended);

        var resp = await starter.PostAsJsonAsync("/api/v1/chat/conversations",
            new { participantUserIds = new[] { keeperId, suspendedId }, name = "Weekend crew" });

        Assert.True(resp.IsSuccessStatusCode,
            $"suspended must stay reachable: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Site 6 — the lazy direct-send path (feature 022). Also read <c>Users</c> directly, so
    /// without the positive test a member could open a DM with an account that no longer exists.
    /// </summary>
    [Fact]
    public async Task Site6_cannot_direct_message_an_erased_account()
    {
        var (sender, _, _) = await NewUserAsync();
        var (_, erasedId, _) = await NewUserAsync();

        await EraseAsync(erasedId);

        var resp = await sender.PostAsJsonAsync(
            $"/api/v1/chat/direct/{erasedId}/messages", new { body = "hello?" });

        Assert.False(resp.IsSuccessStatusCode);

        // And nothing was persisted as a side effect of the attempt.
        Assert.False(await WithDbAsync(db =>
            db.ConversationParticipants.AnyAsync(p => p.UserId == erasedId)));
    }

    /// <summary>Site 6 control — a suspended account can still be sent a direct message.</summary>
    [Fact]
    public async Task Site6_control_can_direct_message_a_suspended_account()
    {
        var (sender, _, _) = await NewUserAsync();
        var (_, suspendedId, _) = await NewUserAsync();

        await SetStatusAsync(suspendedId, AccountStatus.Suspended);

        var resp = await sender.PostAsJsonAsync(
            $"/api/v1/chat/direct/{suspendedId}/messages", new { body = "still reachable" });

        Assert.True(resp.IsSuccessStatusCode,
            $"suspended must stay reachable: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Site 7 — group-participant resolution (adding people to an existing conversation). Same
    /// direct <c>Users</c> read, same fail-open, same fix.
    /// </summary>
    [Fact]
    public async Task Site7_cannot_add_an_erased_account_to_an_existing_conversation()
    {
        var (owner, ownerId, _) = await NewUserAsync();
        var (_, memberId, _) = await NewUserAsync();
        var (_, erasedId, _) = await NewUserAsync();

        var conversationId = await SeedConversationAsync(ConversationKind.Group, null, ownerId, memberId);
        await EraseAsync(erasedId);

        var resp = await owner.PostAsJsonAsync(
            $"/api/v1/chat/conversations/{conversationId}/members",
            new { userIds = new[] { erasedId } });

        Assert.False(resp.IsSuccessStatusCode);
        Assert.False(await WithDbAsync(db => db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == erasedId)));
    }

    /// <summary>Site 7 control — a suspended account can still be added.</summary>
    [Fact]
    public async Task Site7_control_can_add_a_suspended_account_to_a_conversation()
    {
        var (owner, ownerId, _) = await NewUserAsync();
        var (_, memberId, _) = await NewUserAsync();
        var (_, suspendedId, _) = await NewUserAsync();

        var conversationId = await SeedConversationAsync(ConversationKind.Group, null, ownerId, memberId);
        await SetStatusAsync(suspendedId, AccountStatus.Suspended);

        var resp = await owner.PostAsJsonAsync(
            $"/api/v1/chat/conversations/{conversationId}/members",
            new { userIds = new[] { suspendedId } });

        Assert.True(resp.IsSuccessStatusCode,
            $"suspended must stay reachable: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    // --- the placeholder, which is what makes retention readable ---------------

    /// <summary>
    /// Not a predicate site, but the behaviour they protect: an erased sender's history must still
    /// read, under the neutral placeholder. This is the mechanism feature 037 reuses rather than
    /// rebuilds — it keys on the profile projecting to null, which erasure causes by deleting it.
    /// </summary>
    [Fact]
    public async Task Erased_sender_reads_as_the_neutral_placeholder()
    {
        var (leaver, leaverId, _) = await NewUserAsync();
        var (keeper, keeperId, _) = await NewUserAsync();

        var conversationId = await SeedConversationAsync(ConversationKind.Direct, null, leaverId, keeperId);
        await SendAsync(leaver, conversationId, "see you around");

        await EraseAsync(leaverId);

        var page = await GetMessagesAsync(keeper, conversationId);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        var message = Assert.Single(items);

        // The words survive verbatim (FR-024) …
        Assert.Equal("see you around", message.GetProperty("body").GetString());
        // … and the author does not (FR-023, FR-026).
        Assert.Equal("A former player", message.GetProperty("senderName").GetString());
    }
}
