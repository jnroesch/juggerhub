using System.Collections.Concurrent;
using JuggerHub.Services.Notifications.Push;

namespace JuggerHub.Api.IntegrationTests.Push;

/// <summary>
/// Records push dispatches so tests can assert who was and — crucially — who was <em>not</em> sent
/// to, without any outbound call (feature 055). Mirrors the <c>FakeChatRealtime</c> pattern.
/// </summary>
public sealed class FakePushDispatcher : IPushDispatcher
{
    private readonly ConcurrentQueue<Dispatch> _dispatches = new();

    /// <summary>Set to make the next dispatch throw, proving a failure cannot fail the producing action.</summary>
    public bool ThrowOnDispatch { get; set; }

    public IReadOnlyCollection<Dispatch> Dispatches => _dispatches.ToArray();

    /// <summary>Every recipient across every dispatch, flattened.</summary>
    public IReadOnlyCollection<Guid> Recipients =>
        _dispatches.SelectMany(d => d.RecipientUserIds).ToArray();

    public void Clear()
    {
        _dispatches.Clear();
        ThrowOnDispatch = false;
    }

    public Task DispatchAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        PushContent content,
        CancellationToken ct = default)
    {
        if (ThrowOnDispatch)
        {
            throw new InvalidOperationException("Simulated push failure.");
        }

        _dispatches.Enqueue(new Dispatch(recipientUserIds.ToArray(), content));
        return Task.CompletedTask;
    }

    public sealed record Dispatch(IReadOnlyCollection<Guid> RecipientUserIds, PushContent Content);
}
