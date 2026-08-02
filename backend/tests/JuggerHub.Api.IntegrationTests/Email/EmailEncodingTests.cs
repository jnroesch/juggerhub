using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// The template layer substitutes values HTML-encoded by default (feature 039, FR-006).
///
/// These pin a regression that predates the four migrated emails: <c>ReplaceVariables</c> used a
/// raw <c>string.Replace</c>, so member-authored text — a team name, a news body — was injected
/// into the rendered email as live markup. The four hand-rolled emails escaped their own values,
/// which is exactly why moving them onto the shared templates had to be preceded by making the
/// template layer safe: the migration would otherwise have <em>removed</em> their escaping.
///
/// Subjects are deliberately excluded (FR-010) — they are plain-text headers that no client parses
/// as markup, so encoding them would show entities in the recipient's inbox.
/// </summary>
[Collection("Teams")]
public sealed class EmailEncodingTests
{
    private const string MarkupName = "<b>Ravens</b>";
    private const string AmpersandName = "Ravens & Co";

    private readonly JuggerHubApiFactory _factory;

    public EmailEncodingTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Team_name_with_markup_is_escaped_in_the_email_body()
    {
        var (adminClient, _) = await NewUserAsync();
        var (memberClient, memberEmail) = await NewUserAsync();
        _ = memberClient;

        var slug = await CreateTeamAsync(adminClient, MarkupName);
        await AddMemberAsync(slug, memberEmail);

        _factory.EmailSender.Clear();
        var post = await adminClient.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "Training moves to Thursday." });
        post.EnsureSuccessStatusCode();

        var mail = _factory.EmailSender.LatestFor(memberEmail);
        Assert.NotNull(mail);

        // The literal tag must not survive into the body; the encoded form must.
        Assert.DoesNotContain("<b>Ravens</b>", mail!.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;Ravens&lt;/b&gt;", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task News_body_with_markup_is_escaped_in_the_email_body()
    {
        var (adminClient, _) = await NewUserAsync();
        var (memberClient, memberEmail) = await NewUserAsync();
        _ = memberClient;

        var slug = await CreateTeamAsync(adminClient, "Rheinfeuer");
        await AddMemberAsync(slug, memberEmail);

        _factory.EmailSender.Clear();
        var post = await adminClient.PostAsJsonAsync(
            $"/api/v1/teams/{slug}/news",
            new { body = "Check <a href=\"https://evil.example\">this link</a> now." });
        post.EnsureSuccessStatusCode();

        var mail = _factory.EmailSender.LatestFor(memberEmail);
        Assert.NotNull(mail);

        // A member-authored anchor must never become a live link inside JuggerHub-branded mail.
        Assert.DoesNotContain("<a href=\"https://evil.example\"", mail!.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;a href=", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subject_line_is_not_html_encoded()
    {
        var (adminClient, _) = await NewUserAsync();
        var (memberClient, memberEmail) = await NewUserAsync();
        _ = memberClient;

        var slug = await CreateTeamAsync(adminClient, AmpersandName);
        await AddMemberAsync(slug, memberEmail);

        _factory.EmailSender.Clear();
        var post = await adminClient.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "Kit order closes Friday." });
        post.EnsureSuccessStatusCode();

        var mail = _factory.EmailSender.LatestFor(memberEmail);
        Assert.NotNull(mail);

        // FR-010: the subject is a plain-text header. Encoding it would render "&amp;" to the reader.
        Assert.Contains(AmpersandName, mail!.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;", mail.Subject, StringComparison.Ordinal);
    }

    // --- helpers --------------------------------------------------------------

    private async Task<(HttpClient Client, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: AuthTestHelpers.NewHandle());
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, email);
    }

    private static async Task<string> CreateTeamAsync(HttpClient adminClient, string name)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var resp = await adminClient.PostAsJsonAsync("/api/v1/teams",
            new { name, slug, type = "Mixteam", location = (object?)null });
        resp.EnsureSuccessStatusCode();
        return slug;
    }

    /// <summary>Seed the membership directly — the invite dance is not what these tests are about.</summary>
    private async Task AddMemberAsync(string slug, string memberEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teamId = await db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).FirstAsync();
        var userId = await db.Users.Where(u => u.Email == memberEmail).Select(u => u.Id).FirstAsync();

        db.TeamMemberships.Add(new TeamMembership
        {
            TeamId = teamId,
            UserId = userId,
            Role = TeamRole.Member,
            JoinedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
