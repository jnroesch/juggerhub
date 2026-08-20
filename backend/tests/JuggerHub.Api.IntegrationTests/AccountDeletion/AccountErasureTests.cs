using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>
/// Feature 037 T016 — the disposition inventory. For every table that references an account, assert
/// it reaches the state <c>specs/037-account-deletion/data-model.md §2</c> says it should.
/// </summary>
/// <remarks>
/// The three groups are asserted separately on purpose, because getting one right tells you nothing
/// about the others: rows that <b>cascade</b> off the profile, rows deleted <b>explicitly</b> (some
/// of which are <c>Restrict</c> and would throw if missed), and rows that must <b>survive</b>
/// because they belong to somebody else.
/// </remarks>
[Collection("AccountDeletion")]
public sealed class AccountErasureTests : AccountDeletionTestSupport
{
    public AccountErasureTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Erasure_deletes_the_profile_and_neutralises_the_account_row()
    {
        var (client, userId, handle, email) = await NewMemberAsync();

        var resp = await DeleteAccountAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // The account row SURVIVES — ~20 Restrict FKs make deleting it impossible — but identifies
        // nobody.
        var user = await WithDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.Email, u.NormalizedEmail, u.UserName, u.PasswordHash, u.PreferredLanguage })
            .SingleAsync());

        Assert.Equal(AccountStatus.Deleted, user.Status);
        Assert.Null(user.Email);
        Assert.Null(user.NormalizedEmail);
        Assert.Null(user.PasswordHash);
        Assert.Null(user.PreferredLanguage);

        // The username is released too, and derives from nothing that could re-identify them.
        Assert.NotNull(user.UserName);
        Assert.DoesNotContain(handle, user.UserName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(email.Split('@')[0], user.UserName, StringComparison.OrdinalIgnoreCase);

        // The profile is genuinely gone, not hidden behind a filter.
        Assert.False(await HasProfileAsync(userId));
    }

    [Fact]
    public async Task Erasure_cascades_everything_owned_through_the_profile()
    {
        var (client, userId, _, _) = await NewMemberAsync();

        // Give them a pompfe selection and an event participation to cascade.
        (await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Ada K.",
            pompfen = new[] { "Langpompfe" },
        })).EnsureSuccessStatusCode();

        await WithDbAsync(async db =>
        {
            var profileId = await db.PlayerProfiles.IgnoreQueryFilters()
                .Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync();
            var ev = new Event { Name = "Turnier", StartsAt = DateTime.UtcNow.AddDays(5) };
            db.Events.Add(ev);
            db.EventParticipations.Add(new EventParticipation { ProfileId = profileId, Event = ev, TeamLabel = "Guest" });
            db.ProfileAvatars.Add(new ProfileAvatar
            {
                ProfileId = profileId,
                ContentType = "image/webp",
                ObjectKey = $"avatars/{Guid.NewGuid():N}.webp",
                SizeBytes = 512,
            });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);

        await WithDbAsync(async db =>
        {
            Assert.False(await db.ProfilePompfen.IgnoreQueryFilters().AnyAsync(pp => pp.Profile.UserId == userId));
            Assert.False(await db.ProfileAvatars.IgnoreQueryFilters().AnyAsync(a => a.Profile.UserId == userId));
            Assert.False(await db.EventParticipations.IgnoreQueryFilters().AnyAsync(ep => ep.Profile.UserId == userId));
        });
    }

    [Fact]
    public async Task Erasure_removes_the_showcase_gallery_rows_and_their_stored_objects()
    {
        // Feature 046 (#99). The rows cascade with the profile — but a cascade deletes descriptors
        // inside PostgreSQL with no application code running, so the pictures themselves survive
        // unless the keys are harvested before the transaction and reclaimed after it. Up to five
        // photographs of a member who asked to be erased is not a rounding error.
        var (client, userId, handle, _) = await NewMemberAsync();

        var keys = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(ShowcasePng()), "file", "showcase.png");
            (await client.PostAsync("/api/v1/profiles/me/showcase", content)).EnsureSuccessStatusCode();
        }

        keys.AddRange(await WithDbAsync(db => db.ProfileShowcaseImages.IgnoreQueryFilters()
            .Where(g => g.Profile.UserId == userId)
            .Select(g => g.ObjectKey)
            .ToListAsync()));
        Assert.Equal(3, keys.Count);

        var store = Factory.Services.GetRequiredService<JuggerHub.Services.Media.IMediaStore>();
        foreach (var key in keys)
        {
            Assert.True(await store.ExistsAsync(key));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);

        Assert.False(await WithDbAsync(db => db.ProfileShowcaseImages.IgnoreQueryFilters()
            .AnyAsync(g => g.Profile.UserId == userId)));

        foreach (var key in keys)
        {
            Assert.False(await store.ExistsAsync(key), "a showcase picture survived the erasure that removed its row");
        }

        // And the gallery is not reachable through the read path either.
        var anon = Factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}/showcase")).StatusCode);
    }

    private static byte[] ShowcasePng()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(
            120, 90, new SixLabors.ImageSharp.PixelFormats.Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        image.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return ms.ToArray();
    }

    [Fact]
    public async Task Erasure_deletes_sessions_participation_and_preferences()
    {
        var (client, userId, _, _) = await NewMemberAsync();

        // Signing in produced at least one refresh token, which carries an originating IP address.
        Assert.True(await WithDbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == userId)));

        var teamId = await CreateTeamWithSoleAdminAsync(userId);
        // A second admin, so the last-admin guard does not block this test's subject.
        var (_, otherId, _, _) = await NewMemberAsync();
        await AddTeamAdminAsync(teamId, otherId);

        await WithDbAsync(async db =>
        {
            db.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = userId,
                Category = NotificationCategory.TeamNews,
                Channel = NotificationChannel.Email,
                Enabled = false,
            });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);

        await WithDbAsync(async db =>
        {
            // Sessions are DELETED, not merely revoked — the row carries a per-session IP (FR-016).
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == userId));
            Assert.False(await db.TeamMemberships.AnyAsync(m => m.UserId == userId));
            Assert.False(await db.NotificationPreferences.AnyAsync(p => p.UserId == userId));
            Assert.False(await db.Notifications.AnyAsync(n => n.RecipientUserId == userId));
            Assert.False(await db.ConversationParticipants.AnyAsync(p => p.UserId == userId));
            Assert.False(await db.UserRoles.AnyAsync(r => r.UserId == userId));
        });
    }

    [Fact]
    public async Task Erasure_removes_blocks_in_both_directions()
    {
        var (leaver, leaverId, _, _) = await NewMemberAsync();
        var (_, otherId, _, _) = await NewMemberAsync();

        await WithDbAsync(async db =>
        {
            db.UserBlocks.Add(new UserBlock { BlockerUserId = leaverId, BlockedUserId = otherId });
            db.UserBlocks.Add(new UserBlock { BlockerUserId = otherId, BlockedUserId = leaverId });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // Both directions go. The FK is Restrict on both sides, so a miss here would have thrown
        // rather than silently left a row — but assert the outcome, not the mechanism.
        Assert.False(await WithDbAsync(db => db.UserBlocks
            .AnyAsync(b => b.BlockerUserId == leaverId || b.BlockedUserId == leaverId)));
    }

    [Fact]
    public async Task Records_belonging_to_other_members_survive()
    {
        var (leaver, leaverId, _, _) = await NewMemberAsync();
        var (_, keeperId, _, _) = await NewMemberAsync();

        // Something the leaver did TO somebody else: a decision on their join request, plus a news
        // post the team still depends on.
        var teamId = await CreateTeamWithSoleAdminAsync(leaverId);
        await AddTeamAdminAsync(teamId, keeperId);

        var newsId = await WithDbAsync(async db =>
        {
            var post = new TeamNewsPost
            {
                TeamId = teamId,
                AuthorUserId = leaverId,
                Body = "We're on the west pitch this week.",
            };
            db.TeamNewsPosts.Add(post);
            await db.SaveChangesAsync();
            return post.Id;
        });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // The post survives VERBATIM, still pointing at an account that now identifies nobody
        // (FR-021, FR-024).
        var post = await WithDbAsync(db => db.TeamNewsPosts.AsNoTracking()
            .Where(p => p.Id == newsId)
            .Select(p => new { p.Body, p.AuthorUserId })
            .SingleAsync());

        Assert.Equal("We're on the west pitch this week.", post.Body);
        Assert.Equal(leaverId, post.AuthorUserId);
    }

    [Fact]
    public async Task Moderation_history_survives_the_account_it_is_about()
    {
        var (leaver, leaverId, _, _) = await NewMemberAsync();
        var (_, actorId, _, _) = await NewMemberAsync();

        await WithDbAsync(async db =>
        {
            db.AdminActionRecords.Add(new AdminActionRecord
            {
                ActorUserId = actorId,
                TargetUserId = leaverId,
                Action = AdminAccountAction.PasswordResetSent,
            });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // Retained — principally as a record of the ADMINISTRATOR's conduct, which is another
        // person's data (spec FR-022). The target now resolves to nobody.
        Assert.True(await WithDbAsync(db => db.AdminActionRecords
            .AnyAsync(r => r.TargetUserId == leaverId && r.ActorUserId == actorId)));
    }
}
