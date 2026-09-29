using JuggerHub.Services.Email;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>One captured outbound email.</summary>
public sealed record CapturedEmail(string To, string Subject, string HtmlBody);

/// <summary>
/// Test double for <see cref="IEmailSender"/> — captures outbound mail in memory so
/// tests can extract the verification/reset link (and token) and complete the flow
/// without a real SMTP server.
/// </summary>
public sealed class TestEmailSender : IEmailSender
{
    private readonly object _lock = new();
    private readonly List<CapturedEmail> _sent = new();
    private readonly HashSet<string> _failing = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CapturedEmail> Sent
    {
        get
        {
            lock (_lock)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_failing.Contains(to))
            {
                throw new InvalidOperationException("Simulated delivery failure.");
            }

            _sent.Add(new CapturedEmail(to, subject, htmlBody));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Make every send to <paramref name="to"/> throw until <see cref="Heal"/> — proves a failure for
    /// one recipient neither fails the producing action nor stops the others (feature 064, FR-020).
    /// The sender is shared by a whole collection, so always heal in a <c>finally</c>.
    /// </summary>
    public void FailFor(string to)
    {
        lock (_lock)
        {
            _failing.Add(to);
        }
    }

    public void Heal(string to)
    {
        lock (_lock)
        {
            _failing.Remove(to);
        }
    }

    /// <summary>The most recent email sent to <paramref name="to"/>, or null.</summary>
    public CapturedEmail? LatestFor(string to)
    {
        lock (_lock)
        {
            return _sent.LastOrDefault(e => string.Equals(e.To, to, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _sent.Clear();
            _failing.Clear();
        }
    }
}
