namespace JuggerHub.Common;

/// <summary>
/// Configuration for chat push notifications (feature 056 / GH #309). Bound from the
/// <c>ChatPush</c> config section with defaults that work with zero configuration. No secrets here
/// — the VAPID key pair belongs to <see cref="WebPushOptions"/> and is untouched by this feature.
/// </summary>
/// <remarks>
/// Chat is the one part of the product with real-time urgency and, until this feature, no way to
/// reach anybody who was not already looking at the site. Delivery is deliberately <em>not</em> on
/// the send path: a background pass picks messages up once they have gone unread for
/// <see cref="QuietDelaySeconds"/> and hands them to the push channel feature 055 built.
/// </remarks>
public sealed class ChatPushOptions
{
    public const string SectionName = "ChatPush";

    /// <summary>
    /// Whether the background pass runs at all. On everywhere by default; the integration tests
    /// turn it off so a timer cannot race assertions that drive a pass directly — the same reason
    /// <see cref="RetentionOptions.Enabled"/> exists.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long a message must stay unread before it is worth a notification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the presence check.</b> The product cannot see whether a window is open — a
    /// connection-state check would depend on the Redis backplane, which is in
    /// <c>docker-compose.yml</c> and deployed by nothing in <c>infra/</c> (GH #219), and would not
    /// help anyway because browsers require every push to be visible. "Still unread after this
    /// long" is the stand-in: read it on any device inside the window and nothing is sent.
    /// </para>
    /// <para>
    /// <b>30 seconds is an owner decision</b> (spec Clarifications), chosen over two and five
    /// minutes because chat's whole claim on a notification is immediacy. The accepted cost is
    /// more notifications reaching somebody who was about to open the conversation on another
    /// screen.
    /// </para>
    /// </remarks>
    public int QuietDelaySeconds { get; set; } = 30;

    /// <summary>
    /// How often a pass runs. Felt latency is <see cref="QuietDelaySeconds"/> <em>plus</em> this,
    /// so it stays well under the delay rather than near it.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// A message older than this is marked considered and never dispatched.
    /// </summary>
    /// <remarks>
    /// The point is the backlog after an interruption: a restored service must not buzz every
    /// phone on the platform about yesterday's conversation. Same spirit as the six-hour
    /// time-to-live <c>PushDispatcher</c> puts on an undelivered message — everything here is
    /// time-sensitive, and a conversation from this morning is not worth a notification tonight.
    /// </remarks>
    public int MaxMessageAgeMinutes { get; set; } = 60;

    /// <summary>
    /// Hard ceiling on how many messages one pass considers.
    /// </summary>
    /// <remarks>
    /// <b>Not a tuning knob.</b> Principle VII forbids unbounded work, and an unbounded
    /// <c>ToListAsync</c> over a backlog is the same defect as an unbounded wait wearing different
    /// clothes. Whatever a pass does not reach is picked up by the next one, because the work is
    /// selected by a flag rather than by a window.
    /// </remarks>
    public int MaxMessagesPerPass { get; set; } = 500;

    /// <summary>
    /// Hard ceiling on a single pass. Principle VII: nothing waits forever, background loops
    /// included — a stuck pass must surface instead of holding a connection until the process dies.
    /// </summary>
    public int PassTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// How much of a message's text a notification may carry.
    /// </summary>
    /// <remarks>
    /// Messages run to <c>ChatConstants.MaxMessageLength</c> (2000 characters) and no lock screen
    /// shows anything like that, so sending one whole would reproduce a long private message
    /// outside the platform for no benefit to the reader. That the payload carries the text at all
    /// is an owner decision (spec Clarifications, FR-019) and is why the privacy policy describes
    /// it.
    /// </remarks>
    public int PreviewLength { get; set; } = 120;
}
