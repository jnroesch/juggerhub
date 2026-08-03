using System.Net;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>
/// The record of what an account agreed to must outlive every state that account can reach
/// (feature 041, US3 — FR-023 and FR-024).
///
/// This lives in the erasure suite rather than the terms suite on purpose: the failure it guards
/// against is a change to <c>AccountDeletionService.EraseOwnedDataAsync</c>. That method is a list
/// of <c>ExecuteDeleteAsync</c> calls over every table keyed by <c>UserId</c>, and
/// <c>TermsAcceptances</c> is keyed by <c>UserId</c> — so it reads like it belongs on the list.
/// It does not. Adding it would destroy the consent evidence for precisely the accounts most
/// likely to dispute something later, and nothing else in the suite would notice.
/// </summary>
[Collection("AccountDeletion")]
public sealed class TermsAcceptanceSurvivalTests : AccountDeletionTestSupport
{
    public TermsAcceptanceSurvivalTests(JuggerHubApiFactory factory) : base(factory) { }

    private Task<List<TermsAcceptance>> AcceptancesFor(Guid userId) =>
        WithDbAsync(db => db.TermsAcceptances
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync());

    /// <summary>A suspension is temporary; the agreement behind it is untouched.</summary>
    [Fact]
    public async Task Acceptance_survives_a_suspension()
    {
        var (_, userId, _, _) = await NewMemberAsync();
        var before = Assert.Single(await AcceptancesFor(userId));

        await SetStatusAsync(userId, AccountStatus.Suspended);

        var after = Assert.Single(await AcceptancesFor(userId));
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.CreatedDate, after.CreatedDate);
    }

    /// <summary>
    /// A ban is a retained soft-delete, and the evidence of what the banned account agreed to is
    /// the entire reason to keep the row rather than remove it.
    /// </summary>
    [Fact]
    public async Task Acceptance_survives_a_ban()
    {
        var (_, userId, _, _) = await NewMemberAsync();
        var before = Assert.Single(await AcceptancesFor(userId));

        await SetStatusAsync(userId, AccountStatus.Banned);

        var after = Assert.Single(await AcceptancesFor(userId));
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.CreatedDate, after.CreatedDate);
    }

    /// <summary>
    /// ⚠ The one most likely to regress. Erasure (feature 037) neutralises the identifying columns
    /// on the surviving <c>User</c> row rather than deleting it, so the acceptance continues to
    /// evidence that an agreement was made while pointing at a row that identifies nobody
    /// (FR-024).
    /// </summary>
    [Fact]
    public async Task Acceptance_survives_self_erasure_and_then_identifies_nobody()
    {
        var (client, userId, _, _) = await NewMemberAsync();
        var before = Assert.Single(await AcceptancesFor(userId));

        var response = await DeleteAccountAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = Assert.Single(await AcceptancesFor(userId));
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.CreatedDate, after.CreatedDate);
        Assert.Equal(before.DisplayLanguage, after.DisplayLanguage);

        // The row the evidence points at has been stripped of everything identifying.
        var user = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == userId));
        Assert.Equal(AccountStatus.Deleted, user.Status);
        Assert.Null(user.Email);
        Assert.Null(user.PasswordHash);
    }

    /// <summary>
    /// The erased account's email is released, so the person can register again. That produces a
    /// NEW account with its own acceptance; the original is neither reused nor rewritten, so the
    /// two agreements stay distinguishable.
    /// </summary>
    [Fact]
    public async Task Re_registration_after_erasure_creates_a_second_independent_acceptance()
    {
        var (client, userId, _, email) = await NewMemberAsync();
        var original = Assert.Single(await AcceptancesFor(userId));

        (await DeleteAccountAsync(client)).EnsureSuccessStatusCode();

        var fresh = Factory.CreateClient();
        (await AuthTestHelpers.RegisterAsync(fresh, email)).EnsureSuccessStatusCode();

        var newUserId = await WithDbAsync(db => db.Users
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .SingleAsync());

        Assert.NotEqual(userId, newUserId);
        Assert.Single(await AcceptancesFor(newUserId));

        // The original is untouched and still attached to the erased row.
        var kept = Assert.Single(await AcceptancesFor(userId));
        Assert.Equal(original.Id, kept.Id);
        Assert.Equal(original.CreatedDate, kept.CreatedDate);
    }

    /// <summary>
    /// The structural guard behind all of the above: the FK is <c>Restrict</c>, so a naive
    /// "delete everything with this UserId" fails loudly at the database instead of succeeding
    /// quietly. Asserted directly, because it is what turns a future mistake into a red build
    /// rather than silent data loss.
    /// </summary>
    /// <remarks>
    /// The exception is a raw <see cref="Npgsql.PostgresException"/> rather than a
    /// <c>DbUpdateException</c>: <c>ExecuteDeleteAsync</c> bypasses the change tracker, so EF does
    /// not wrap the provider error. Asserted on SQLSTATE <c>23001</c> (<c>restrict_violation</c>)
    /// rather than the more familiar <c>23503</c> (<c>foreign_key_violation</c>): Postgres raises
    /// the former only for an explicit <c>RESTRICT</c>, so this proves the delete behaviour is
    /// actually <c>Restrict</c> and not <c>NoAction</c>, which would still fail here but leaves
    /// the intent unverified.
    /// </remarks>
    [Fact]
    public async Task Deleting_the_account_row_is_refused_while_an_acceptance_references_it()
    {
        var (_, userId, _, _) = await NewMemberAsync();

        var error = await Assert.ThrowsAnyAsync<PostgresException>(() => WithDbAsync(db =>
            db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync()));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        Assert.Equal("FK_TermsAcceptances_AspNetUsers_UserId", error.ConstraintName);
    }
}
