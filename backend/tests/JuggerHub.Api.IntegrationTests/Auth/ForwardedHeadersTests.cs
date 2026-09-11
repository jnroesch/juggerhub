using System.Net;
using System.Net.Http.Json;
using JuggerHub.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>
/// GH #244 — which address does the backend attribute a request to? Asserted through what a login
/// actually STORES (<c>RefreshToken.CreatedByIp</c>), so the test follows the value end to end rather
/// than inspecting middleware state.
/// </summary>
/// <remarks>
/// The test server has no TCP peer, so a startup filter plays the part of the proxy in front of the
/// backend: it sets <c>RemoteIpAddress</c> from an <c>X-Test-Peer</c> header before the app's own
/// pipeline — and therefore before <c>UseForwardedHeaders</c> — runs.
/// </remarks>
[Collection("Auth")]
public sealed class ForwardedHeadersTests
{
    private const string PodCidr = "10.244.0.0/16";
    private const string TrustedPeer = "10.244.1.23";   // the frontend nginx / ingress pod
    private const string UntrustedPeer = "198.51.100.9"; // anything that bypassed the proxies
    private const string Visitor = "203.0.113.7";

    private readonly JuggerHubApiFactory _factory;

    public ForwardedHeadersTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Forwarded_client_address_from_a_trusted_proxy_is_recorded()
    {
        var stored = await LoginAndReadStoredIpAsync(knownNetworks: PodCidr, peer: TrustedPeer, forwardedFor: Visitor);
        Assert.Equal(Visitor, stored);
    }

    [Fact]
    public async Task Forwarded_header_from_an_untrusted_peer_is_ignored()
    {
        // A caller that is not one of our proxies cannot claim an address.
        var stored = await LoginAndReadStoredIpAsync(knownNetworks: PodCidr, peer: UntrustedPeer, forwardedFor: Visitor);
        Assert.Equal(UntrustedPeer, stored);
    }

    [Fact]
    public async Task Only_the_hop_in_front_is_trusted_so_a_client_prepended_address_cannot_win()
    {
        // A client can send its own X-Forwarded-For; each proxy puts the address IT saw last.
        // ForwardLimit = 1 reads only that last entry.
        var stored = await LoginAndReadStoredIpAsync(knownNetworks: PodCidr, peer: TrustedPeer, forwardedFor: $"192.0.2.66, {Visitor}");
        Assert.Equal(Visitor, stored);
    }

    [Fact]
    public async Task Without_configured_networks_nothing_is_trusted()
    {
        var stored = await LoginAndReadStoredIpAsync(knownNetworks: null, peer: TrustedPeer, forwardedFor: Visitor);
        Assert.Equal(TrustedPeer, stored);
    }

    private async Task<string?> LoginAndReadStoredIpAsync(string? knownNetworks, string peer, string forwardedFor)
    {
        // Register through the shared host, then log in through a host configured for the case.
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(_factory.CreateClient(), _factory);

        await using var host = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ForwardedHeaders:KnownNetworks"] = knownNetworks }));
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, TestPeerStartupFilter>());
        });
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = AuthTestHelpers.ValidPassword, rememberMe = false }),
        };
        request.Headers.Add("X-Test-Peer", peer);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        var login = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedDate)
            .Select(t => t.CreatedByIp)
            .FirstAsync();
    }

    private sealed class TestPeerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, Func<Task> nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Peer", out var peer))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(peer!);
                }
                return nextMiddleware();
            });
            next(app);
        };
    }
}
