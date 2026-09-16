using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Team logos (feature 051 / #305): who may set one, who may see one, what the surfaces report,
/// and what happens to the stored object when a logo or a team goes away.
/// </summary>
/// <remarks>
/// The write rules are the point of this suite. A logo is uploaded on a team's behalf, so "admin
/// of this team" is the whole authorization story, and a non-member must be refused in a way that
/// does not tell them whether the team exists — the same no-membership-oracle rule every other
/// team mutation follows.
/// </remarks>
[Collection("Teams")]
public sealed class TeamLogoTests
{
    private readonly JuggerHubApiFactory _factory;

    public TeamLogoTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- Setting a logo --------------------------------------------------------

    [Fact]
    public async Task Admin_can_upload_a_logo_and_it_is_stored_as_webp()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        var upload = await UploadLogoAsync(admin, slug, MinimalPng());
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        var logo = await admin.GetAsync($"/api/v1/teams/{slug}/logo");
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        // Feature 034: whatever is uploaded is re-encoded before storage.
        Assert.Equal("image/webp", logo.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Uploaded_logo_is_cropped_to_a_square()
    {
        // The owner chose centre-crop over letterboxing (spec Clarifications, FR-005): every
        // surface renders into a square tile. Pinning it here means a later change of the
        // processing profile cannot flip it silently.
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        (await UploadLogoAsync(admin, slug, WidePng())).EnsureSuccessStatusCode();

        var bytes = await (await admin.GetAsync($"/api/v1/teams/{slug}/logo")).Content.ReadAsByteArrayAsync();
        using var stored = Image.Load(bytes);
        Assert.Equal(stored.Width, stored.Height);
    }

    [Fact]
    public async Task Member_who_is_not_an_admin_cannot_set_the_logo()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var member = await JoinTeamAsync(admin, slug);

        var upload = await UploadLogoAsync(member, slug, MinimalPng());

        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }

    [Fact]
    public async Task Non_member_gets_404_rather_than_403_when_setting_a_logo()
    {
        // No membership oracle: a stranger must not be able to tell "this team exists and I am not
        // allowed" from "no such team" (spec FR-003).
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var (stranger, _, _, _) = await NewUserAsync();

        var real = await UploadLogoAsync(stranger, slug, MinimalPng());
        var imaginary = await UploadLogoAsync(stranger, "t" + Guid.NewGuid().ToString("N")[..12], MinimalPng());

        Assert.Equal(HttpStatusCode.NotFound, real.StatusCode);
        Assert.Equal(imaginary.StatusCode, real.StatusCode);
    }

    [Fact]
    public async Task Upload_without_a_file_is_rejected()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        using var empty = new MultipartFormDataContent();
        empty.Add(new ByteArrayContent([]), "file", "logo.png");
        var response = await admin.PutAsync($"/api/v1/teams/{slug}/logo", empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejected_upload_leaves_the_existing_logo_untouched()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();

        var before = await (await admin.GetAsync($"/api/v1/teams/{slug}/logo")).Content.ReadAsByteArrayAsync();

        var rejected = await UploadLogoAsync(admin, slug, "this is not an image"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var after = await (await admin.GetAsync($"/api/v1/teams/{slug}/logo")).Content.ReadAsByteArrayAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Replacing_a_logo_deletes_the_object_it_replaces()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();
        var firstKey = await LogoKeyAsync(slug);

        (await UploadLogoAsync(admin, slug, WidePng())).EnsureSuccessStatusCode();
        var secondKey = await LogoKeyAsync(slug);

        Assert.NotEqual(firstKey, secondKey);
        Assert.False(await Store.ExistsAsync(firstKey!), "the superseded logo object was left behind");
        Assert.True(await Store.ExistsAsync(secondKey!));
    }

    // --- Reading a logo --------------------------------------------------------

    [Fact]
    public async Task Any_signed_in_player_can_read_a_teams_logo()
    {
        // Deliberately NOT member-gated (spec FR-012): browse lists teams to non-members and those
        // rows carry logos. Every other read on this controller is member-gated, so this is the
        // rule most likely to be "corrected" by accident.
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();

        var (stranger, _, _, _) = await NewUserAsync();
        var response = await stranger.GetAsync($"/api/v1/teams/{slug}/logo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signed_out_caller_cannot_read_a_logo()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();

        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/teams/{slug}/logo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_team_without_a_logo_and_an_unknown_team_both_answer_404()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        var noLogo = await admin.GetAsync($"/api/v1/teams/{slug}/logo");
        var noTeam = await admin.GetAsync($"/api/v1/teams/t{Guid.NewGuid():N}/logo");

        Assert.Equal(HttpStatusCode.NotFound, noLogo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noTeam.StatusCode);
    }

    // --- The surfaces report it ------------------------------------------------

    [Fact]
    public async Task Team_detail_public_page_browse_and_my_teams_all_report_the_logo()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        Assert.False(await HasLogoOnDetailAsync(admin, slug));

        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();

        Assert.True(await HasLogoOnDetailAsync(admin, slug));

        var pub = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/public");
        Assert.True(pub.GetProperty("hasLogo").GetBoolean());

        var browse = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams?q={slug}");
        var card = browse.GetProperty("items").EnumerateArray()
            .Single(t => t.GetProperty("slug").GetString() == slug);
        Assert.True(card.GetProperty("hasLogo").GetBoolean());
        // The letter fallback is still computed — it is what a team without a logo renders.
        Assert.Equal("R", card.GetProperty("logoInitial").GetString());

        var mine = await admin.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/teams");
        var row = mine.GetProperty("items").EnumerateArray()
            .Single(t => t.GetProperty("slug").GetString() == slug);
        Assert.True(row.GetProperty("hasLogo").GetBoolean());
    }

    // --- Removing ---------------------------------------------------------------

    [Fact]
    public async Task Admin_can_remove_the_logo_and_the_object_is_deleted()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();
        var key = await LogoKeyAsync(slug);

        var removed = await admin.DeleteAsync($"/api/v1/teams/{slug}/logo");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/teams/{slug}/logo")).StatusCode);
        Assert.False(await HasLogoOnDetailAsync(admin, slug));
        Assert.False(await Store.ExistsAsync(key!), "removing a logo left its object in the store");
    }

    [Fact]
    public async Task Removing_a_logo_that_is_not_there_succeeds()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        var removed = await admin.DeleteAsync($"/api/v1/teams/{slug}/logo");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_remove_the_logo()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();
        var member = await JoinTeamAsync(admin, slug);

        var removed = await member.DeleteAsync($"/api/v1/teams/{slug}/logo");

        Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/teams/{slug}/logo")).StatusCode);
    }

    [Fact]
    public async Task Deleting_the_team_deletes_its_logo_object()
    {
        // The descriptor row cascades away inside PostgreSQL with no application code running, so
        // nothing would know where the bytes are afterwards. The delete path reads the key first
        // for exactly that reason (spec FR-019).
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await UploadLogoAsync(admin, slug, MinimalPng())).EnsureSuccessStatusCode();
        var key = await LogoKeyAsync(slug);

        (await admin.DeleteAsync($"/api/v1/teams/{slug}")).EnsureSuccessStatusCode();

        Assert.False(await Store.ExistsAsync(key!), "deleting a team stranded its logo object");
    }

    // --- helpers ----------------------------------------------------------------

    private IMediaStore Store => _factory.Services.GetRequiredService<IMediaStore>();

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    private static async Task<string> CreateTeamAsync(HttpClient admin)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        // The name carries the slug so browse's free-text search (which matches name or city,
        // never the slug) can find this exact team among everything the shared database holds.
        var resp = await admin.PostAsJsonAsync("/api/v1/teams",
            new { name = $"Rheinfeuer {slug}", slug, type = "CityTeam", location = new { cityExternalId = "TEST:berlin" } });
        resp.EnsureSuccessStatusCode();
        return slug;
    }

    /// <summary>Register a second player and put them on the team as an ordinary member.</summary>
    private async Task<HttpClient> JoinTeamAsync(HttpClient admin, string slug)
    {
        var link = await admin.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        var (member, _, _, _) = await NewUserAsync();
        (await member.PostAsync($"/api/v1/invitations/{token}/accept", null)).EnsureSuccessStatusCode();
        return member;
    }

    private static Task<HttpResponseMessage> UploadLogoAsync(HttpClient client, string slug, byte[] bytes)
    {
        var content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "logo.png" } };
        return client.PutAsync($"/api/v1/teams/{slug}/logo", content);
    }

    private static async Task<bool> HasLogoOnDetailAsync(HttpClient client, string slug)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}");
        return detail.GetProperty("hasLogo").GetBoolean();
    }

    private async Task<string?> LogoKeyAsync(string slug)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TeamLogos.AsNoTracking()
            .Where(l => l.Team.Slug == slug)
            .Select(l => l.ObjectKey)
            .FirstOrDefaultAsync();
    }

    private static byte[] MinimalPng()
    {
        using var img = new Image<Rgba32>(8, 8, new Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>A deliberately non-square image — the shape the crop decision is about.</summary>
    private static byte[] WidePng()
    {
        using var img = new Image<Rgba32>(240, 80, new Rgba32(200, 60, 30));
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        return ms.ToArray();
    }
}
