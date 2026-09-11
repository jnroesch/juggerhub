using System.Net;
using System.Net.Http.Json;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>
/// GH #250 — email-verification and password-reset links are Data Protection payloads, so they are
/// only as durable as the key ring that signed them. Deployed, the backend runs several replicas and
/// restarts on every rollout; a link is minted by one process and clicked into another.
/// </summary>
/// <remarks>
/// A second host built from the same factory is a separate process in every way that matters here —
/// its own DI container, its own key manager — sharing only the database, exactly like a second pod.
/// It is built AFTER the first has issued the token, so it also stands in for a restarted pod.
/// <para>
/// The cross-host tests alone would pass against the old code on a developer machine: two hosts in
/// one process fall back to the same user-profile key directory. The key-ring-location test is the
/// one that can only pass with the ring in the database.
/// </para>
/// </remarks>
[Collection("Auth")]
public sealed class DataProtectionKeyRingTests
{
    private const string NewPassword = "N3w!Passw0rd#";

    private readonly JuggerHubApiFactory _factory;

    public DataProtectionKeyRingTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Key_ring_is_stored_in_the_database()
    {
        var client = _factory.CreateClient();
        // Registering sends a verification email, which forces the key ring to exist.
        await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var keys = await db.DataProtectionKeys.AsNoTracking().ToListAsync();

        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.Contains("<key ", k.Xml, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reset_link_issued_by_one_replica_is_accepted_by_another()
    {
        var clientA = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(clientA, _factory);
        await clientA.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        var (userId, token) = AuthTestHelpers.ParseResetLink(_factory.EmailSender.LatestFor(email)!.HtmlBody);

        await using var replicaB = _factory.WithWebHostBuilder(_ => { });
        var clientB = replicaB.CreateClient();

        var reset = await clientB.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId, token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var login = await AuthTestHelpers.LoginAsync(clientB, email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Verification_link_issued_by_one_replica_is_accepted_by_another()
    {
        var clientA = _factory.CreateClient();
        var email = AuthTestHelpers.NewEmail();
        (await AuthTestHelpers.RegisterAsync(clientA, email)).EnsureSuccessStatusCode();
        var (userId, token) = AuthTestHelpers.ParseVerificationLink(_factory.EmailSender.LatestFor(email)!.HtmlBody);

        await using var replicaB = _factory.WithWebHostBuilder(_ => { });
        var clientB = replicaB.CreateClient();

        var verify = await clientB.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId, token });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }
}
