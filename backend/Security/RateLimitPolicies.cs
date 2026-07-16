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
        });

        return services;
    }

    private static Func<HttpContext, RateLimitPartition<string>> PartitionByUser(string policy, int limit) =>
        httpContext =>
        {
            var subject = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // An unauthenticated caller cannot reach these endpoints ([Authorize] runs first), but if
            // one ever did, bucket them together rather than handing out an unlimited partition.
            var key = $"{policy}:{subject ?? "anonymous"}";

            var redis = httpContext.RequestServices.GetService<IConnectionMultiplexer>();

            if (redis is null)
            {
                // Development without Redis only (Program.cs makes this fatal everywhere else). The
                // in-memory limiter is correct on a single instance and would be silently wrong on
                // several — which is exactly why it is not allowed to reach a deployed environment.
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            }

            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger<RedisFixedWindowRateLimiter>();

            return RateLimitPartition.Get(key, k => new RedisFixedWindowRateLimiter(
                redis, k, limit, TimeSpan.FromMinutes(1), logger));
        };
}
