using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

namespace JuggerHub.Security.RateLimiting;

/// <summary>
/// Named rate-limit policies (introduced by feature 019). The constitution admits rate limiting as
/// core security middleware under Principle II's "lean middleware" rule; nothing registered one until
/// chat needed it.
/// </summary>
/// <remarks>
/// <para>
/// Chat's direct-message reach is deliberately <b>open</b> — any signed-in player may message any
/// other, with no shared-team precondition (spec FR-049). Blocking is the recourse, but blocking is
/// <em>per recipient and reactive</em>: it cannot stop one account from messaging a thousand people
/// once each. These limits are what bound that (spec FR-049a), and they are enforced server-side, so
/// driving the API directly instead of the UI does not get around them.
/// </para>
/// <para>
/// The counters live in <b>Redis</b>, not in process memory, because the deployment runs more than one
/// replica — see <see cref="RedisFixedWindowRateLimiter"/> for why an in-memory limiter is silently
/// wrong there. Redis is a hard requirement outside Development; <c>Program.cs</c> fails fast without it.
/// </para>
/// <para>
/// Partitioned on the <b>authenticated user id</b>, not the IP: every limited endpoint requires auth,
/// and IP-keying would throttle a whole clubhouse or tournament venue behind one NAT as if it were a
/// single abuser.
/// </para>
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>Starting new conversations — the mass-DM vector.</summary>
    public const string ChatStart = "chat-start";

    /// <summary>Sending messages.</summary>
    public const string ChatSend = "chat-send";

    /// <summary>The typing signal (already debounced client-side to ~1 per 3s).</summary>
    public const string ChatTyping = "chat-typing";

    /// <summary>10/min: comfortably above a person starting a few chats in a sitting, far below a script working the member list.</summary>
    internal const int ChatStartPerMinute = 10;

    /// <summary>30/min: a fast conversation is a handful of messages a minute; 30 leaves headroom for an animated group chat while still capping a flood.</summary>
    internal const int ChatSendPerMinute = 30;

    /// <summary>30/min: matches the send limit — the client debounces typing to ~1 per 3s, so a legitimate typist never approaches this.</summary>
    internal const int ChatTypingPerMinute = 30;

    /// <summary>Media reads — avatars and catalogue icons (feature 035 / #97).</summary>
    public const string MediaRead = "media-read";

    /// <summary>
    /// 300/min. Media reads are unlike the chat limits in kind: they are reads, they are cheap, and
    /// a single page legitimately issues one per displayed member — a directory page can be dozens
    /// at once, and a member browsing quickly multiplies that. The limit exists because every byte
    /// now flows through the backend (media is proxied, never served from public storage), so an
    /// unbounded reader could occupy request capacity that other members need. It is set to bound
    /// that, not to police normal browsing.
    /// </summary>
    internal const int MediaReadPerMinute = 300;

    /// <summary>
    /// Actions that make the server contact Tugeny: linking a tournament and previewing or committing
    /// an import (feature 050).
    /// </summary>
    /// <remarks>
    /// Two opposite meanings of <c>429</c> meet here (constitution Principle VII). A <c>429</c> this
    /// policy returns is <b>our own</b> fail-closed limit: the browser never retries it (the retry
    /// interceptor skips 429) and the page asks the admin to wait. A <c>429</c> <b>Tugeny</b> returns to
    /// us is the provider throttling us, and the shared outbound pipeline retries it with backoff,
    /// honouring <c>Retry-After</c>. This limit keeps one admin from hammering Tugeny through us; the
    /// outbound circuit breaker caps what reaches Tugeny across all admins.
    /// </remarks>
    public const string Tugeny = "tugeny";

    /// <summary>10/min: an import session is one link, one preview and one commit; ten leaves room for a retry or a wrong address.</summary>
    internal const int TugenyPerMinute = 10;

    /// <summary>
    /// Asking to join a team (feature 058). Only the ask — withdrawing and answering are never
    /// limited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it exists.</b> A join request has open reach, the same property that made chat's
    /// limits load-bearing (019): any signed-in player may ask any team, with no relationship
    /// needed. That was harmless while a request reached nobody. Since feature 058 every request
    /// reaches each of the team's admins in their inbox, by email and on their phone, so without a
    /// bound, asking and withdrawing is a way to message those admins as often as one likes.
    /// </para>
    /// <para>
    /// <b>A fixed window</b>, like every limit here: ten per player per clock hour, across all
    /// teams. A burst straddling the turn of an hour can therefore reach twenty, and no more (spec
    /// FR-023 states exactly this).
    /// </para>
    /// <para>
    /// <b>This <c>429</c> is our own limit</b> (constitution Principle VII): it is never retried on
    /// either hop — the browser's retry interceptor already skips <c>429</c> — and the client maps
    /// the status, not the body, to a "try again later" in the player's language.
    /// </para>
    /// </remarks>
    public const string JoinRequest = "join-request";

    /// <summary>10/hour: a new player asking two or three teams during onboarding never comes close; a loop of asks and withdrawals stops at ten.</summary>
    internal const int JoinRequestsPerHour = 10;

    public static IServiceCollection AddJuggerHubRateLimiting(
        this IServiceCollection services,
        string? redisConnection)
    {
        // A single multiplexer, shared: StackExchange.Redis is built to be used this way, and a
        // connection per request would cost more than the limit check it guards.
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection));
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ChatStart, PartitionByUser(ChatStart, ChatStartPerMinute));
            options.AddPolicy(ChatSend, PartitionByUser(ChatSend, ChatSendPerMinute));
            options.AddPolicy(ChatTyping, PartitionByUser(ChatTyping, ChatTypingPerMinute));
            options.AddPolicy(MediaRead, PartitionByCaller(MediaRead, MediaReadPerMinute));
            options.AddPolicy(Tugeny, PartitionByUser(Tugeny, TugenyPerMinute));
            options.AddPolicy(JoinRequest, PartitionByUser(JoinRequest, JoinRequestsPerHour, TimeSpan.FromHours(1)));
        });

        return services;
    }

    private static Func<HttpContext, RateLimitPartition<string>> PartitionByUser(string policy, int limit) =>
        PartitionByUser(policy, limit, TimeSpan.FromMinutes(1));

    private static Func<HttpContext, RateLimitPartition<string>> PartitionByUser(string policy, int limit, TimeSpan window) =>
        httpContext =>
        {
            var subject = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // An unauthenticated caller cannot reach these endpoints ([Authorize] runs first), but if
            // one ever did, bucket them together rather than handing out an unlimited partition.
            return Limiter(httpContext, $"{policy}:{subject ?? "anonymous"}", limit, window);
        };

    /// <summary>
    /// Partition by authenticated user when there is one, and by client IP otherwise.
    /// </summary>
    /// <remarks>
    /// <see cref="PartitionByUser(string, int)"/> does not fit the media endpoints: they serve anonymous callers
    /// <b>by design</b> — public profiles and catalogue icons — so bucketing every signed-out
    /// visitor into a single "anonymous" partition would let one of them exhaust the limit for all
    /// of them, turning a safeguard into a denial of service against legitimate visitors. Falling
    /// back to the client IP keeps signed-out callers apart. The shared-NAT objection that rules
    /// IP-keying out for chat bites far less here: it applies to a 300/min read budget rather than
    /// chat's 10/min write budget.
    /// </remarks>
    private static Func<HttpContext, RateLimitPartition<string>> PartitionByCaller(string policy, int limit) =>
        httpContext =>
        {
            var subject = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var caller = subject is not null
                ? $"u:{subject}"
                : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return Limiter(httpContext, $"{policy}:{caller}", limit, TimeSpan.FromMinutes(1));
        };

    /// <summary>Build the partition's limiter — Redis-backed everywhere it matters.</summary>
    private static RateLimitPartition<string> Limiter(HttpContext httpContext, string key, int limit, TimeSpan window)
    {
        var redis = httpContext.RequestServices.GetService<IConnectionMultiplexer>();

        if (redis is null)
        {
            // Development without Redis only (Program.cs makes this fatal everywhere else). The
            // in-memory limiter is correct on a single instance and would be silently wrong on
            // several — which is exactly why it is not allowed to reach a deployed environment.
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        }

        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<RedisFixedWindowRateLimiter>();

        return RateLimitPartition.Get(key, k => new RedisFixedWindowRateLimiter(
            redis, k, limit, window, logger));
    }
}
