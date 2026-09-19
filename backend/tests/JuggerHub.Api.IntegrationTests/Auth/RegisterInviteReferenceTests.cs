using System.Net;
using System.Net.Http.Json;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>
/// Feature 053 — the shared invite link a person registered from survives the email hop: the
/// verification link carries <c>inviteSlug</c> + <c>inviteToken</c> when the pair is well-formed,
/// and nothing else about registration changes. The pair is never stored — there is no column to
/// assert on, which is the point (spec FR-001a).
/// </summary>
[Collection("Auth")]
public sealed class RegisterInviteReferenceTests
{
    private const string Slug = "berlin-jugger";

    // 43 base64url chars — what TeamInvitationService.NewToken() produces.
    private const string Token = "Xy9_abcDEF-ghiJKL012mnoPQR345stuVWX678yzAB_";

    private readonly JuggerHubApiFactory _factory;

    public RegisterInviteReferenceTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Well_formed_pair_rides_on_the_verification_link()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        var response = await AuthTestHelpers.RegisterAsync(client, email, inviteSlug: Slug, inviteToken: Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = _factory.EmailSender.LatestFor(email)!.HtmlBody;
        Assert.Contains("verify-email?userId=", html);
        Assert.Contains($"inviteSlug={Slug}", html);
        Assert.Contains($"inviteToken={Token}", html);
    }

    [Theory]
    [InlineData("Bad_Slug", Token)]          // slug breaks the team-slug format
    [InlineData("ab", Token)]                // slug below the minimum length
    [InlineData(Slug, "spaces are not ok")]  // token outside the charset
    [InlineData(Slug, "tooshort")]           // token below the length bound
    [InlineData(Slug, null)]                 // one without the other
    [InlineData(null, Token)]
    public async Task Malformed_pair_is_dropped_and_registration_is_otherwise_identical(string? slug, string? token)
    {
        var client = _factory.CreateClient();
        var plainEmail = AuthTestHelpers.NewEmail();
        var invitedEmail = AuthTestHelpers.NewEmail();

        var plain = await AuthTestHelpers.RegisterAsync(client, plainEmail);
        var withPair = await AuthTestHelpers.RegisterAsync(client, invitedEmail, inviteSlug: slug, inviteToken: token);

        // Same status, same neutral body — the pair can never change what registration says.
        Assert.Equal(HttpStatusCode.OK, withPair.StatusCode);
        Assert.Equal(await plain.Content.ReadAsStringAsync(), await withPair.Content.ReadAsStringAsync());

        var html = _factory.EmailSender.LatestFor(invitedEmail)!.HtmlBody;
        Assert.Contains("verify-email?userId=", html);
        Assert.DoesNotContain("inviteSlug", html);
        Assert.DoesNotContain("inviteToken", html);
    }

    [Fact]
    public async Task Registration_without_the_pair_builds_the_link_it_always_did()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        await AuthTestHelpers.RegisterAsync(client, email);

        var html = _factory.EmailSender.LatestFor(email)!.HtmlBody;
        Assert.Contains("verify-email?userId=", html);
        Assert.DoesNotContain("invite", html);
    }

    [Fact]
    public async Task Registering_the_same_unverified_email_again_resends_a_link_that_still_carries_the_pair()
    {
        // The realistic route into the existing-email branch: the invited person registers twice
        // from the same link because the first mail did not turn up.
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        await AuthTestHelpers.RegisterAsync(client, email, inviteSlug: Slug, inviteToken: Token);
        var firstMail = _factory.EmailSender.LatestFor(email)!;

        var second = await AuthTestHelpers.RegisterAsync(client, email, inviteSlug: Slug, inviteToken: Token);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondMail = _factory.EmailSender.LatestFor(email)!;
        Assert.NotSame(firstMail, secondMail);
        Assert.Contains($"inviteSlug={Slug}", secondMail.HtmlBody);
        Assert.Contains($"inviteToken={Token}", secondMail.HtmlBody);
    }

    [Fact]
    public async Task Resend_verification_carries_the_pair_when_given_and_not_otherwise()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();
        await AuthTestHelpers.RegisterAsync(client, email);

        var withPair = await AuthTestHelpers.ResendVerificationAsync(client, email, Slug, Token);
        Assert.Equal(HttpStatusCode.OK, withPair.StatusCode);
        Assert.Contains($"inviteSlug={Slug}", _factory.EmailSender.LatestFor(email)!.HtmlBody);

        var without = await AuthTestHelpers.ResendVerificationAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, without.StatusCode);
        Assert.DoesNotContain("inviteSlug", _factory.EmailSender.LatestFor(email)!.HtmlBody);

        // Neutral either way: the two responses are indistinguishable.
        Assert.Equal(await withPair.Content.ReadAsStringAsync(), await without.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_pair_never_reaches_the_account_and_the_invitation_is_never_touched()
    {
        // Registering with a pair that names no real invitation must succeed and verify exactly
        // like any other registration: nothing about the reference is read, written, or checked
        // beyond its shape. (There is deliberately no column to assert on.)
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        var register = await AuthTestHelpers.RegisterAsync(client, email, inviteSlug: "no-such-team", inviteToken: Token);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var (userId, token) = AuthTestHelpers.ParseVerificationLink(_factory.EmailSender.LatestFor(email)!.HtmlBody);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId, token });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }
}
