using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// A team's showcase gallery (feature 046, GH #99): five pictures, managed by the team's admins,
/// visible to every signed-in member of the platform.
/// </summary>
/// <remarks>
/// The permission split is the point of this suite. An ordinary member sees the gallery and is
/// offered no controls — but "offered no controls" is a client-side statement, so what actually has
/// to hold is that the API refuses them (constitution Principle I).
/// </remarks>
[Collection("Teams")]
public sealed class TeamShowcaseTests
{
    private const int MaxImages = 5;

    private readonly JuggerHubApiFactory _factory;

    public TeamShowcaseTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_adds_pictures_and_they_appear_on_the_team()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);

        Assert.Empty(await ListAsync(admin, slug));

        var created = await AddImageAsync(admin, slug);
        Assert.Equal(0, created.GetProperty("position").GetInt32());

        var images = await ListAsync(admin, slug);
        var only = Assert.Single(images);

        var bytes = await admin.GetAsync($"/api/v1/teams/{slug}/showcase/{only.GetProperty("id").GetGuid()}/image");
        Assert.Equal(HttpStatusCode.OK, bytes.StatusCode);
        Assert.Equal("image/webp", bytes.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Sixth_picture_is_refused_at_the_cap()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);

        for (var i = 0; i < MaxImages; i++)
        {
            await AddImageAsync(admin, slug);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await PostImageAsync(admin, slug)).StatusCode);
        Assert.Equal(MaxImages, (await ListAsync(admin, slug)).Count);
    }

    [Fact]
    public async Task An_ordinary_member_can_look_but_not_touch()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);
        var id = (await AddImageAsync(admin, slug)).GetProperty("id").GetGuid();

        var (member, memberId, _) = await NewUserAsync();
        await AddMemberAsync(teamId, memberId);

        // Reads: yes.
        Assert.Single(await ListAsync(member, slug));
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/v1/teams/{slug}/showcase/{id}/image")).StatusCode);

        // Writes: no — and 403 rather than 404, because a member already knows the team exists.
        Assert.Equal(HttpStatusCode.Forbidden, (await PostImageAsync(member, slug)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetCaptionAsync(member, slug, id, "mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/api/v1/teams/{slug}/showcase/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReorderAsync(member, slug, [id])).StatusCode);

        var after = await ListAsync(admin, slug);
        Assert.Equal(id, Assert.Single(after).GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, after[0].GetProperty("caption").ValueKind);
    }

    [Fact]
    public async Task A_signed_in_non_member_sees_the_gallery_and_cannot_change_it()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);
        var id = (await AddImageAsync(admin, slug)).GetProperty("id").GetGuid();

        var (outsider, _, _) = await NewUserAsync();

        // The gallery does not narrow for a non-member (FR-020).
        Assert.Single(await ListAsync(outsider, slug));
        Assert.Equal(HttpStatusCode.OK, (await outsider.GetAsync($"/api/v1/teams/{slug}/showcase/{id}/image")).StatusCode);

        // But a non-member cannot distinguish this team from one that does not exist when writing.
        Assert.Equal(HttpStatusCode.NotFound, (await PostImageAsync(outsider, slug)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.DeleteAsync($"/api/v1/teams/{slug}/showcase/{id}")).StatusCode);
    }

    [Fact]
    public async Task The_team_gallery_is_never_anonymous()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);
        var id = (await AddImageAsync(admin, slug)).GetProperty("id").GetGuid();

        // There is no anonymous team surface at all (feature 026), so unlike the profile gallery
        // this one never opens up, whatever the team does.
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/v1/teams/{slug}/showcase")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync($"/api/v1/teams/{slug}/showcase/{id}/image")).StatusCode);
    }

    [Fact]
    public async Task Profile_and_team_caps_are_counted_separately()
    {
        // FR-003: a member's own five and each of their teams' five never share a budget.
        var (admin, adminId, handle) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);

        for (var i = 0; i < MaxImages; i++)
        {
            var response = await PostBytesAsync(admin, "/api/v1/profiles/me/showcase", Png(120, 90));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        for (var i = 0; i < MaxImages; i++)
        {
            await AddImageAsync(admin, slug);
        }

        var profileImages = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/profiles/{handle}/showcase");
        Assert.Equal(MaxImages, profileImages!.Count);
        Assert.Equal(MaxImages, (await ListAsync(admin, slug)).Count);
    }

    [Fact]
    public async Task Deleting_the_team_removes_its_pictures_from_the_store()
    {
        // The cascade removes the rows inside PostgreSQL with no application code running, so the
        // objects survive unless their keys are harvested before the delete (FR-012, SC-010).
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);

        await AddImageAsync(admin, slug);
        await AddImageAsync(admin, slug);

        var keys = await WithDbAsync(db => db.TeamShowcaseImages
            .Where(g => g.TeamId == teamId)
            .Select(g => g.ObjectKey)
            .ToListAsync());
        Assert.Equal(2, keys.Count);

        var store = _factory.Services.GetRequiredService<IMediaStore>();
        foreach (var key in keys)
        {
            Assert.True(await store.ExistsAsync(key));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/teams/{slug}")).StatusCode);

        Assert.False(await WithDbAsync(db => db.TeamShowcaseImages.AnyAsync(g => g.TeamId == teamId)));
        foreach (var key in keys)
        {
            Assert.False(await store.ExistsAsync(key), "a team picture survived the team it belonged to");
        }
    }

    [Fact]
    public async Task A_banned_uploader_does_not_hide_the_teams_gallery()
    {
        // Unlike the profile gallery, a team's pictures do not inherit anybody's account standing:
        // the team is not punished for one member's conduct (research R3).
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);
        var id = (await AddImageAsync(admin, slug)).GetProperty("id").GetGuid();

        var (viewer, viewerId, _) = await NewUserAsync();
        await AddMemberAsync(teamId, viewerId);

        await BanAsync(adminId);

        Assert.Single(await ListAsync(viewer, slug));
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/v1/teams/{slug}/showcase/{id}/image")).StatusCode);
    }

    [Fact]
    public async Task Reorder_and_removal_keep_positions_dense()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);

        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await AddImageAsync(admin, slug)).GetProperty("id").GetGuid());
        }

        var reversed = ids.AsEnumerable().Reverse().ToList();
        Assert.Equal(HttpStatusCode.NoContent, (await ReorderAsync(admin, slug, reversed)).StatusCode);
        Assert.Equal(reversed, (await ListAsync(admin, slug)).Select(i => i.GetProperty("id").GetGuid()));

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/v1/teams/{slug}/showcase/{reversed[1]}")).StatusCode);

        var remaining = await ListAsync(admin, slug);
        Assert.Equal([reversed[0], reversed[2]], remaining.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal([0, 1], remaining.Select(i => i.GetProperty("position").GetInt32()));
    }

    [Fact]
    public async Task Reorder_that_is_not_a_permutation_is_refused_with_nothing_written()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);

        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await AddImageAsync(admin, slug)).GetProperty("id").GetGuid());
        }

        Assert.Equal(HttpStatusCode.Conflict, (await ReorderAsync(admin, slug, [ids[0], ids[1]])).StatusCode);
        Assert.Equal(ids, (await ListAsync(admin, slug)).Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task No_response_discloses_the_stored_objects_location()
    {
        var (admin, adminId, _) = await NewUserAsync();
        var (teamId, slug) = await SeedTeamAsync();
        await AddMemberAsync(teamId, adminId, TeamRole.Admin);
        var id = (await AddImageAsync(admin, slug)).GetProperty("id").GetGuid();

        var objectKey = await WithDbAsync(db => db.TeamShowcaseImages
            .Where(g => g.Id == id).Select(g => g.ObjectKey).FirstAsync());

        var listing = await admin.GetAsync($"/api/v1/teams/{slug}/showcase");
        var body = await listing.Content.ReadAsStringAsync();
        var image = await admin.GetAsync($"/api/v1/teams/{slug}/showcase/{id}/image");
        var headers = string.Join('\n', image.Headers.Concat(image.Content.Headers)
            .Select(h => $"{h.Key}: {string.Join(',', h.Value)}"));

        Assert.DoesNotContain(objectKey, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("team-showcase", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(objectKey, headers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("team-showcase", headers, StringComparison.OrdinalIgnoreCase);
    }

    // --- helpers ---------------------------------------------------------------

    private async Task<(HttpClient Client, Guid UserId, string Handle)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle);
    }

    private static string NewSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    private Task WithDbAsync(Func<AppDbContext, Task> action) =>
        WithDbAsync(async db => { await action(db); return true; });

    private Task<(Guid Id, string Slug)> SeedTeamAsync() =>
        WithDbAsync(async db =>
        {
            var team = new Team { Slug = NewSlug(), Name = "Rheinfeuer", Type = TeamType.Mixteam };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
            return (team.Id, team.Slug);
        });

    private Task AddMemberAsync(Guid teamId, Guid userId, TeamRole role = TeamRole.Member) =>
        WithDbAsync(async db =>
        {
            db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = teamId,
                UserId = userId,
                Role = role,
                JoinedDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

    private Task BanAsync(Guid userId) =>
        WithDbAsync(db => db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Status, AccountStatus.Banned)
                .SetProperty(u => u.StatusChangedAt, DateTime.UtcNow)));

    private static async Task<List<JsonElement>> ListAsync(HttpClient client, string slug)
    {
        var response = await client.GetAsync($"/api/v1/teams/{slug}/showcase");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<JsonElement>>())!;
    }

    private static Task<HttpResponseMessage> PostImageAsync(HttpClient client, string slug) =>
        PostBytesAsync(client, $"/api/v1/teams/{slug}/showcase", Png(120, 90));

    private static async Task<HttpResponseMessage> PostBytesAsync(HttpClient client, string url, byte[] payload)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(payload), "file", "showcase.png");
        return await client.PostAsync(url, content);
    }

    private static async Task<JsonElement> AddImageAsync(HttpClient client, string slug)
    {
        var response = await PostImageAsync(client, slug);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private static Task<HttpResponseMessage> SetCaptionAsync(HttpClient client, string slug, Guid id, string? caption) =>
        client.PatchAsJsonAsync($"/api/v1/teams/{slug}/showcase/{id}", new { caption });

    private static Task<HttpResponseMessage> ReorderAsync(HttpClient client, string slug, IReadOnlyList<Guid> ids) =>
        client.PutAsJsonAsync($"/api/v1/teams/{slug}/showcase/order", new { imageIds = ids });

    private static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }
}
