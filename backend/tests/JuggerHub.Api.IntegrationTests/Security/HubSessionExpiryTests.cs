using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using JuggerHub.Api.IntegrationTests.Admin;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Common;
using JuggerHub.Services.Chat.Realtime;
using JuggerHub.Services.Notifications.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JuggerHub.Api.IntegrationTests.Security;

/// <summary>
/// GH #402 — a hub connection ends with the session it was made in. The access token is validated
/// once, at the handshake; these tests pin that the socket does not outlive it.
/// </summary>
/// <remarks>
/// <para>
/// Over a real <b>WebSocket</b>, deliberately. Long polling authenticates every poll, so it was never
/// affected and a test over it would pass with the fix removed.
/// </para>
/// <para>
/// The connection's own token is minted here with a lifetime of seconds — the configured lifetime is
/// whole minutes — signed with the test host's key, so it is an ordinary access token to the server.
/// </para>
/// <para>
/// In the "AdminArea" collection because the ban test needs the platform admin that collection owns.
/// </para>
/// </remarks>
[Collection("AdminArea")]
public sealed class HubSessionExpiryTests
{
    private const string ChatHubPath = "/hubs/chat";
    private const string NotificationHubPath = "/hubs/notifications";

    /// <summary>A client method no real handler listens for: what is asserted is the group, not a payload.</summary>
    private const string Probe = "probe";

    /// <summary>Long enough to connect and see a push on a slow runner, short enough to wait out.</summary>
    private static readonly TimeSpan ShortLife = TimeSpan.FromSeconds(4);

    /// <summary>The connection manager checks for expired connections once a second; this is generous.</summary>
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(20);

    private readonly JuggerHubApiFactory _factory;

    public HubSessionExpiryTests(JuggerHubApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(ChatHubPath)]
    [InlineData(NotificationHubPath)]
    public async Task A_connection_is_closed_when_the_token_it_was_made_with_expires(string hub)
    {
        var (userId, _) = await AuthTestHelpers.RegisterAndVerifyAsync(_factory.CreateClient(), _factory);

        await using var connection = await ConnectAsync(hub, AccessCookie(MintAccessToken(userId, ShortLife)));
        Assert.True(await connection.ReceivesAsync(() => PushAsync(hub, userId)), "The live connection should receive its user's stream.");

        // Nothing on the client asks for this: the server ends the connection.
        Assert.True(await connection.ClosedWithin(CloseTimeout), "The connection should be closed once its token has expired.");

        var seen = connection.Received;
        await PushAsync(hub, userId);
        await Task.Delay(300);
        Assert.Equal(seen, connection.Received);
    }

    /// <summary>
    /// The option is set per <c>MapHub</c> call and defaults to off, so a hub added later could be
    /// mapped without it and nothing would look wrong. This walks whatever is mapped.
    /// </summary>
    [Fact]
    public async Task Every_mapped_hub_ends_its_connections_with_the_session()
    {
        // MapHub puts HubMetadata on two endpoints per hub; the one without NegotiateMetadata is the
        // transport endpoint, whose pattern is the hub's path.
        var hubs = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HubMetadata>() is not null
                && e.Metadata.GetMetadata<NegotiateMetadata>() is null)
            .Select(e => e.RoutePattern.RawText!)
            .Distinct()
            .ToList();
        Assert.Contains(ChatHubPath, hubs);
        Assert.Contains(NotificationHubPath, hubs);

        var (userId, _) = await AuthTestHelpers.RegisterAndVerifyAsync(_factory.CreateClient(), _factory);
        var cookie = AccessCookie(MintAccessToken(userId, ShortLife));
        var connections = await Task.WhenAll(hubs.Select(hub => ConnectAsync(hub, cookie)));

        try
        {
            var closed = await Task.WhenAll(connections.Select(c => c.ClosedWithin(CloseTimeout)));

            Assert.All(hubs.Zip(closed), hub => Assert.True(hub.Second, $"{hub.First} kept its connection past the token's expiry."));
        }
        finally
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// The validator accepts a token for its clock skew past the expiry. A hub must not: it would
    /// admit a connection it closes a second later, over and over, for as long as the skew lasts.
    /// </summary>
    [Theory]
    [InlineData(ChatHubPath + "/negotiate?negotiateVersion=1", "POST")]
    [InlineData(ChatHubPath, "GET")]
    [InlineData(NotificationHubPath + "/negotiate?negotiateVersion=1", "POST")]
    [InlineData(NotificationHubPath, "GET")]
    public async Task A_handshake_with_a_token_past_its_expiry_is_refused(string path, string method)
    {
        var (userId, _) = await AuthTestHelpers.RegisterAndVerifyAsync(_factory.CreateClient(), _factory);
        var justExpired = AccessCookie(MintAccessToken(userId, TimeSpan.FromSeconds(-5)));
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        // The control: REST still honours the skew for this very token, so what refuses it below is
        // the hub rule and not an invalid token.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", justExpired)).StatusCode);

        var handshake = await SendAsync(client, new HttpMethod(method), path, justExpired);

        Assert.Equal(HttpStatusCode.Unauthorized, handshake.StatusCode);
    }

    [Fact]
    public async Task A_renewed_session_connects_again_after_the_expiry()
    {
        var (userId, refreshCookie) = await SignedInAsync();
        var firstToken = AccessCookie(MintAccessToken(userId, ShortLife));

        await using (var first = await ConnectAsync(ChatHubPath, firstToken))
        {
            Assert.True(await first.ClosedWithin(CloseTimeout));
        }

        // What the browser does next: the handshake is refused, it renews the session, and tries again.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var refused = await SendAsync(client, HttpMethod.Post, ChatHubPath + "/negotiate?negotiateVersion=1", firstToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        var refresh = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/refresh", refreshCookie);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renewed = AccessCookie(AuthTestHelpers.CookieValue(refresh, AuthCookieDefaults.AccessTokenCookie)!);

        await using var second = await ConnectAsync(ChatHubPath, renewed);
        Assert.True(await second.ReceivesAsync(() => PushAsync(ChatHubPath, userId)), "The renewed session should be live again.");
    }

    /// <summary>
    /// A ban revokes refresh tokens and leaves the access token to run out. Before #402 that bounded
    /// REST and nothing else: the open socket kept the banned account in its group until the tab closed.
    /// </summary>
    [Fact]
    public async Task A_banned_accounts_connection_ends_with_its_token_and_cannot_come_back()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var handle = AuthTestHelpers.NewHandle();
        var (userId, refreshCookie) = await SignedInAsync(handle);
        var token = AccessCookie(MintAccessToken(userId, ShortLife));

        await using var connection = await ConnectAsync(ChatHubPath, token);
        Assert.True(await connection.ReceivesAsync(() => PushAsync(ChatHubPath, userId)));

        (await admin.PostAsync($"/api/v1/admin/users/{handle}/ban", null)).EnsureSuccessStatusCode();

        Assert.True(await connection.ClosedWithin(CloseTimeout), "A banned account's connection should end when its token does.");

        // And there is no way back in: the token is refused at the handshake, and it cannot be renewed.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var handshake = await SendAsync(client, HttpMethod.Post, ChatHubPath + "/negotiate?negotiateVersion=1", token);
        Assert.Equal(HttpStatusCode.Unauthorized, handshake.StatusCode);
        var refresh = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/refresh", refreshCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    // --- Support --------------------------------------------------------------

    /// <summary>A verified account with a live session; returns its id and its refresh cookie.</summary>
    private async Task<(Guid UserId, string RefreshCookie)> SignedInAsync(string? handle = null)
    {
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(_factory.CreateClient(), _factory, handle: handle);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();

        var refresh = AuthTestHelpers.CookieValue(login, AuthCookieDefaults.RefreshTokenCookie)!;
        return (userId, $"{AuthCookieDefaults.RefreshTokenCookie}={refresh}");
    }

    private static string AccessCookie(string token) => $"{AuthCookieDefaults.AccessTokenCookie}={token}";

    /// <summary>
    /// An access token as <c>JwtTokenService</c> mints it, except for the lifetime. A negative
    /// lifetime gives a token that is already past its expiry but still inside the validator's skew.
    /// </summary>
    private string MintAccessToken(Guid userId, TimeSpan lifetime)
    {
        var jwt = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.Add(lifetime),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string cookie)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    /// <summary>Pushes to one user's group the way the realtime senders do.</summary>
    private Task PushAsync(string hub, Guid userId) => hub == ChatHubPath
        ? _factory.Services.GetRequiredService<IHubContext<ChatHub>>()
            .Clients.Group(ChatHub.GroupFor(userId)).SendAsync(Probe)
        : _factory.Services.GetRequiredService<IHubContext<NotificationHub>>()
            .Clients.Group(NotificationHub.GroupFor(userId)).SendAsync(Probe);

    /// <summary>
    /// Opens a hub connection the way the browser does — negotiate, then a WebSocket, the session
    /// cookie on both — against the in-memory test server.
    /// </summary>
    private async Task<LiveConnection> ConnectAsync(string hub, string cookie)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, hub), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers["Cookie"] = cookie;
                options.WebSocketFactory = async (context, ct) =>
                {
                    var sockets = _factory.Server.CreateWebSocketClient();
                    sockets.ConfigureRequest = request => request.Headers.Cookie = cookie;
                    return await sockets.ConnectAsync(context.Uri, ct);
                };
            })
            .Build();

        var live = new LiveConnection(connection);
        await connection.StartAsync();
        return live;
    }

    /// <summary>A started connection plus what the tests ask of it: did a push arrive, did the server close it.</summary>
    private sealed class LiveConnection : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<DateTime> _received = new();

        public LiveConnection(HubConnection connection)
        {
            _connection = connection;
            connection.On(Probe, () => _received.Enqueue(DateTime.UtcNow));
            connection.Closed += _ =>
            {
                _closed.TrySetResult();
                return Task.CompletedTask;
            };
        }

        public int Received => _received.Count;

        /// <summary>
        /// Pushes until one arrives. Repeated because a connection joins its user's group in
        /// <c>OnConnectedAsync</c>, which the server runs after it has answered the handshake — so a
        /// push sent the instant the client is "started" can legitimately miss.
        /// </summary>
        public async Task<bool> ReceivesAsync(Func<Task> push)
        {
            var before = Received;
            for (var attempt = 0; attempt < 20 && Received == before; attempt++)
            {
                await push();
                await Task.Delay(100);
            }

            return Received > before;
        }

        public async Task<bool> ClosedWithin(TimeSpan timeout) =>
            await Task.WhenAny(_closed.Task, Task.Delay(timeout)) == _closed.Task;

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
