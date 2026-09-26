namespace JuggerHub.Services.Chat.Push;

/// <summary>
/// One pass of the chat push work: find messages that have gone unread for the quiet delay, decide
/// per recipient whether they should hear about it, and hand what survives to the push channel
/// (feature 056 / GH #309).
/// </summary>
/// <remarks>
/// <para>
/// <b>Separate from the background service that runs it, on purpose.</b> A test drives one
/// deterministic pass and then asserts on what the dispatcher received; a live timer would consider
/// the same messages first and those assertions would depend on timing rather than on behaviour.
/// The same reason <c>IRetentionSweep</c> is separable from <c>RetentionBackgroundService</c>.
/// </para>
/// <para>
/// <b>Nothing here ever throws at its caller.</b> A chat notification is a convenience with a
/// durable equivalent — the message is in the conversation either way — so a failure is logged and
/// the pass moves on.
/// </para>
/// </remarks>
public interface IChatPushScanner
{
    /// <summary>
    /// Runs one pass. Returns how many messages were <em>considered</em>, which is deliberately not
    /// how many notifications were sent: most considered messages send nothing, because everyone
    /// had read them, muted the conversation, or had no device enabled.
    /// </summary>
    Task<int> RunOnceAsync(CancellationToken ct = default);
}
