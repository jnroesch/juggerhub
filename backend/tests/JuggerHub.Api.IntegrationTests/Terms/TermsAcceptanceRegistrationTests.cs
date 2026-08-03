using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Terms;

/// <summary>
/// Registration requires an active, server-validated agreement to the Terms of Use, and records
/// it (feature 041, US1).
///
/// Every refusal here is exercised by posting to the endpoint directly rather than through the
/// registration form. That is the point of the requirement: the disabled submit button is a
/// usability aid, and the server is the boundary (spec FR-017 vs FR-018).
/// </summary>
[Collection("Auth")]
public sealed class TermsAcceptanceRegistrationTests
{
    private readonly JuggerHubApiFactory _factory;

    public TermsAcceptanceRegistrationTests(JuggerHubApiFactory factory) => _factory = factory;

    private static object Payload(
        string email,
        string handle,
        bool? acceptsTerms = true,
        string? version = null,
        string? language = "en") =>
        new
        {
            email,
            password = AuthTestHelpers.ValidPassword,
            handle,
            acceptsTerms,
            termsVersion = version ?? AuthTestHelpers.CurrentTermsVersion,
            termsLanguage = language,
        };

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Nothing at all may survive a refused registration (FR-019).</summary>
    private async Task AssertNothingCreatedAsync(string email, string handle)
    {
        var (users, profiles, acceptances) = await WithDbAsync(async db => (
            await db.Users.CountAsync(u => u.Email == email),
            await db.PlayerProfiles.CountAsync(p => p.Handle == handle),
            await db.TermsAcceptances.CountAsync(a => a.User.Email == email)));

        Assert.Equal(0, users);
        Assert.Equal(0, profiles);
        Assert.Equal(0, acceptances);
    }

    // ---------------------------------------------------------------- refusals (FR-018, SC-002)

    [Fact]
    public async Task Registration_without_acceptance_is_refused_and_creates_nothing()
    {
        var client = _factory.CreateClient();
        var (email, handle) = (AuthTestHelpers.NewEmail(), AuthTestHelpers.NewHandle());

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(email, handle, acceptsTerms: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingCreatedAsync(email, handle);
    }

    /// <summary>
    /// The field omitted entirely, not merely false — the shape a hand-rolled client or a replayed
    /// pre-041 request would have.
    /// </summary>
    [Fact]
    public async Task Registration_omitting_acceptance_entirely_is_refused_and_creates_nothing()
    {
        var client = _factory.CreateClient();
        var (email, handle) = (AuthTestHelpers.NewEmail(), AuthTestHelpers.NewHandle());

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = AuthTestHelpers.ValidPassword,
            handle,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingCreatedAsync(email, handle);
    }

    /// <summary>
    /// A stale cached catalogue, or a tab left open across a deploy. 409 rather than 400 because
    /// the request is well-formed and the fix is to reload, not to correct a field.
    /// </summary>
    [Fact]
    public async Task Registration_quoting_a_stale_terms_version_is_refused_and_creates_nothing()
    {
        var client = _factory.CreateClient();
        var (email, handle) = (AuthTestHelpers.NewEmail(), AuthTestHelpers.NewHandle());

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(email, handle, version: "2020-01-01"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertNothingCreatedAsync(email, handle);
    }

    /// <summary>
    /// Without this check a client could write arbitrary text into an evidence row's language
    /// column.
    /// </summary>
    [Fact]
    public async Task Registration_with_an_unsupported_display_language_is_refused()
    {
        var client = _factory.CreateClient();
        var (email, handle) = (AuthTestHelpers.NewEmail(), AuthTestHelpers.NewHandle());

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(email, handle, language: "fr"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingCreatedAsync(email, handle);
    }

    /// <summary>
    /// A terms refusal must not be absorbed into registration's enumeration-neutral 200. Someone
    /// told to "check your email" for an account that was never created has no way forward.
    /// </summary>
    [Fact]
    public async Task Terms_refusal_is_reported_rather_than_absorbed_into_the_neutral_response()
    {
        var client = _factory.CreateClient();
        var (email, handle) = (AuthTestHelpers.NewEmail(), AuthTestHelpers.NewHandle());

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(email, handle, acceptsTerms: false));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(_factory.EmailSender.LatestFor(email));
    }

    // ---------------------------------------------------------------- the record (FR-020, SC-001)

    [Fact]
    public async Task Accepted_registration_records_exactly_one_acceptance()
    {
        var client = _factory.CreateClient();
        var (email, handle) = (AuthTestHelpers.NewEmail(), AuthTestHelpers.NewHandle());
        var before = DateTime.UtcNow.AddSeconds(-5);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(email, handle, language: "de"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var acceptances = await WithDbAsync(db => db.TermsAcceptances
            .Where(a => a.User.Email == email)
            .ToListAsync());

        var acceptance = Assert.Single(acceptances);
        Assert.Equal(AuthTestHelpers.CurrentTermsVersion, acceptance.Version);
        Assert.Equal("de", acceptance.DisplayLanguage);
        Assert.InRange(acceptance.CreatedDate, before, DateTime.UtcNow.AddSeconds(5));
    }

    /// <summary>
    /// The stored version is the server's own, never the client's. Proven by submitting a version
    /// that differs only in surrounding whitespace: it passes validation after trimming, and what
    /// lands in the row must still be the canonical value.
    /// </summary>
    [Fact]
    public async Task Recorded_version_comes_from_the_server_not_the_request()
    {
        var client = _factory.CreateClient();
        var (email, handle) = (AuthTestHelpers.NewEmail(), AuthTestHelpers.NewHandle());

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(email, handle, version: $"  {AuthTestHelpers.CurrentTermsVersion}  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var version = await WithDbAsync(db => db.TermsAcceptances
            .Where(a => a.User.Email == email)
            .Select(a => a.Version)
            .SingleAsync());

        Assert.Equal(AuthTestHelpers.CurrentTermsVersion, version);
    }

    /// <summary>
    /// FR-022 — and the reason the acceptance rides the user graph rather than a second
    /// SaveChanges. The handle collision fails registration *after* the terms have been validated,
    /// so a row written ahead of account creation would be orphaned here.
    /// </summary>
    [Fact]
    public async Task Registration_that_fails_after_valid_acceptance_leaves_no_orphan_record()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();

        var first = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(AuthTestHelpers.NewEmail(), handle));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var totalAfterFirst = await WithDbAsync(db => db.TermsAcceptances.CountAsync());

        var secondEmail = AuthTestHelpers.NewEmail();
        var second = await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(secondEmail, handle));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(totalAfterFirst, await WithDbAsync(db => db.TermsAcceptances.CountAsync()));
        Assert.Equal(0, await WithDbAsync(db => db.Users.CountAsync(u => u.Email == secondEmail)));
    }

    /// <summary>
    /// The acceptance and the account are one write. If the account exists, its evidence exists —
    /// there is no ordering in which one is visible without the other.
    /// </summary>
    [Fact]
    public async Task Every_account_created_through_registration_has_an_acceptance()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();

        await client.PostAsJsonAsync(
            "/api/v1/auth/register", Payload(email, AuthTestHelpers.NewHandle()));

        var user = await WithDbAsync(db => db.Users
            .Include(u => u.TermsAcceptances)
            .SingleAsync(u => u.Email == email));

        Assert.Single(user.TermsAcceptances);
    }

    /// <summary>
    /// An account cannot accept the same version twice — the unique index backing a future
    /// re-acceptance flow, pinned now so it is not dropped as "unused".
    /// </summary>
    [Fact]
    public async Task Same_version_cannot_be_accepted_twice_by_one_account()
    {
        var client = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();
        await client.PostAsJsonAsync("/api/v1/auth/register", Payload(email, AuthTestHelpers.NewHandle()));

        var userId = await WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => WithDbAsync(async db =>
        {
            db.TermsAcceptances.Add(new TermsAcceptance
            {
                UserId = userId,
                Version = AuthTestHelpers.CurrentTermsVersion,
                DisplayLanguage = "en",
            });
            return await db.SaveChangesAsync();
        }));
    }
}
