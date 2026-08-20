using System.Text;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JuggerHub.Api.IntegrationTests.Media;

/// <summary>
/// Orphan reclamation (feature 035 / #97, FR-030).
/// </summary>
/// <remarks>
/// The sweep is the only thing that ever reclaims an object orphaned by a database-level cascade
/// delete, which removes a descriptor row inside PostgreSQL with no application code running. That
/// makes it a correctness guarantee rather than housekeeping — and makes the grace period
/// load-bearing in the other direction: an object is written before its descriptor commits, so a
/// sweep that ignored the grace window would race live uploads and destroy them.
/// </remarks>
public sealed class MediaReconciliationTests : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory;

    public MediaReconciliationTests(JuggerHubApiFactory factory) => _factory = factory;

    private IMediaStore Store => _factory.Services.GetRequiredService<IMediaStore>();

    [Fact]
    public async Task Sweep_leaves_a_freshly_written_object_alone()
    {
        // The in-flight upload case. This object has no descriptor yet — exactly what a live upload
        // looks like in the window between storing bytes and committing the row.
        var key = MediaObjectKey.Create(MediaKind.Avatar);
        await Store.PutAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("in flight")), "image/webp");

        var result = await SweepAsync();

        Assert.True(await Store.ExistsAsync(key), "the sweep reclaimed an object still inside its grace period");
        Assert.True(result.Skipped >= 1);
    }

    [Fact]
    public async Task Sweep_reclaims_an_unreferenced_object_past_the_grace_period()
    {
        var key = MediaObjectKey.Create(MediaKind.Avatar);
        await Store.PutAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("orphan")), "image/webp");

        // Zero grace: treat everything already written as old enough. Configuring the boundary is
        // the honest way to test it — the alternative is waiting an hour or faking a clock.
        var result = await SweepAsync(graceMinutes: 0);

        Assert.False(await Store.ExistsAsync(key), "an unreferenced object survived the sweep");
        Assert.True(result.Reclaimed >= 1);
    }

    [Fact]
    public async Task Sweep_never_reclaims_a_referenced_object()
    {
        // The failure this test exists to catch would be silent and irreversible: a sweep that
        // deleted live media would leave every affected member with a broken picture and no way
        // back, because there is no backfill.
        var key = MediaObjectKey.Create(MediaKind.BadgeIcon);
        await Store.PutAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("referenced")), "image/webp");
        var definitionId = await SeedBadgeDefinitionWithIconAsync(key);

        await SweepAsync(graceMinutes: 0);

        Assert.True(await Store.ExistsAsync(key), "the sweep deleted an object a descriptor still references");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.BadgeIcons.AnyAsync(i => i.BadgeDefinitionId == definitionId));
    }

    // --- Feature 046 / #99: the showcase galleries ----------------------------

    [Fact]
    public async Task Sweep_never_reclaims_a_referenced_showcase_object_even_for_a_banned_owner()
    {
        // The single highest-consequence regression in feature 046. This sweep deletes every object
        // no descriptor references, so a gallery table missing from its referenced-key set means the
        // next sweep destroys every showcase image in the environment — irreversibly, with no
        // backfill. The banned owner is the second half of the trap: ProfileShowcaseImages carries a
        // ban query filter, so a read without IgnoreQueryFilters would hide this row and the sweep
        // would treat a suspended member's live media as garbage.
        var profileKey = MediaObjectKey.Create(MediaKind.ProfileShowcase);
        var bannedKey = MediaObjectKey.Create(MediaKind.ProfileShowcase);
        var teamKey = MediaObjectKey.Create(MediaKind.TeamShowcase);

        foreach (var key in new[] { profileKey, bannedKey, teamKey })
        {
            await Store.PutAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("showcase")), "image/webp");
        }

        await SeedProfileShowcaseAsync(profileKey, banned: false);
        await SeedProfileShowcaseAsync(bannedKey, banned: true);
        await SeedTeamShowcaseAsync(teamKey);

        await SweepAsync(graceMinutes: 0);

        Assert.True(await Store.ExistsAsync(profileKey), "the sweep deleted a live profile showcase image");
        Assert.True(await Store.ExistsAsync(bannedKey), "the sweep deleted a banned member's showcase image (missing IgnoreQueryFilters)");
        Assert.True(await Store.ExistsAsync(teamKey), "the sweep deleted a live team showcase image");
    }

    [Fact]
    public async Task Sweep_still_reclaims_a_showcase_object_whose_row_is_gone()
    {
        // The other direction: knowing about the galleries must not make the sweep toothless. An
        // object left behind by a cascade (which deletes rows with no application code running) is
        // exactly what this job exists to reclaim.
        var key = MediaObjectKey.Create(MediaKind.TeamShowcase);
        await Store.PutAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("orphaned by cascade")), "image/webp");
        var imageId = await SeedTeamShowcaseAsync(key);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.TeamShowcaseImages.Where(g => g.Id == imageId).ExecuteDeleteAsync();
        }

        await SweepAsync(graceMinutes: 0);

        Assert.False(await Store.ExistsAsync(key), "an unreferenced showcase object survived the sweep");
    }

    /// <summary>
    /// Run a sweep, optionally overriding the grace period for this call only.
    /// </summary>
    private async Task<MediaReconciliationResult> SweepAsync(int? graceMinutes = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<MediaReconciliationService>();

        var options = new MediaStorageOptions
        {
            OrphanGraceMinutes = graceMinutes ?? new MediaStorageOptions().OrphanGraceMinutes,
        };

        // A zero grace period is a deliberate test-only setting, so bypass Normalize's repair of it.
        var service = new MediaReconciliationService(
            db, store, new TestOptions<MediaStorageOptions>(options), logger);

        return await service.SweepAsync();
    }

    private async Task<Guid> SeedBadgeDefinitionWithIconAsync(string objectKey)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var definition = new JuggerHub.Entities.BadgeDefinition
        {
            Name = $"Sweep fixture {Guid.NewGuid():n}"[..40],
            Description = "Reconciliation test fixture.",
        };
        db.BadgeDefinitions.Add(definition);
        db.BadgeIcons.Add(new JuggerHub.Entities.BadgeIcon
        {
            BadgeDefinitionId = definition.Id,
            ObjectKey = objectKey,
            ContentType = "image/webp",
            SizeBytes = 11,
        });
        await db.SaveChangesAsync();

        return definition.Id;
    }

    /// <summary>Seed a profile (optionally banned) holding one showcase image at <paramref name="objectKey"/>.</summary>
    private async Task<Guid> SeedProfileShowcaseAsync(string objectKey, bool banned)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new JuggerHub.Entities.User
        {
            UserName = $"sweep-{Guid.NewGuid():n}@example.com",
            Email = $"sweep-{Guid.NewGuid():n}@example.com",
            Status = banned ? JuggerHub.Entities.AccountStatus.Banned : JuggerHub.Entities.AccountStatus.Active,
            Profile = new JuggerHub.Entities.PlayerProfile
            {
                Handle = $"s{Guid.NewGuid():n}"[..20],
                DisplayName = "Sweep fixture",
            },
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var image = new JuggerHub.Entities.ProfileShowcaseImage
        {
            ProfileId = user.Profile!.Id,
            Position = 0,
            ObjectKey = objectKey,
            ContentType = "image/webp",
            SizeBytes = 8,
        };
        db.ProfileShowcaseImages.Add(image);
        await db.SaveChangesAsync();

        return image.Id;
    }

    /// <summary>Seed a team holding one showcase image at <paramref name="objectKey"/>.</summary>
    private async Task<Guid> SeedTeamShowcaseAsync(string objectKey)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var team = new JuggerHub.Entities.Team
        {
            Slug = $"t{Guid.NewGuid():n}"[..13],
            Name = "Sweep fixture",
            Type = JuggerHub.Entities.TeamType.Mixteam,
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var image = new JuggerHub.Entities.TeamShowcaseImage
        {
            TeamId = team.Id,
            Position = 0,
            ObjectKey = objectKey,
            ContentType = "image/webp",
            SizeBytes = 8,
        };
        db.TeamShowcaseImages.Add(image);
        await db.SaveChangesAsync();

        return image.Id;
    }

    private sealed class TestOptions<T> : Microsoft.Extensions.Options.IOptions<T> where T : class
    {
        public TestOptions(T value) => Value = value;

        public T Value { get; }
    }
}
