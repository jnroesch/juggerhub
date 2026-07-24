using System.Net.Http.Headers;
using System.Net.Http.Json;
using JuggerHub.Common;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Email;

/// <summary>
/// Resend sender (typed <see cref="HttpClient"/> over the Resend REST API) used on
/// Dev/Prod. Selected when <c>Email:Provider=Resend</c>. No SDK dependency. Provider
/// failures are logged by status only (no body — it may carry detail) and surfaced
/// as a generic exception; callers in enumeration-neutral flows swallow it.
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = _options.FromAddress,
                to = new[] { to },
                subject,
                html = htmlBody,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Every attempt, retry and backoff the shared policy allows has already been spent by
            // the time this is reached — see AddJuggerHubResilience. Log LOUDLY, because this email
            // is now permanently lost: durable delivery is deliberately out of scope for feature
            // 028, so an operator noticing is the only recovery path (FR-021).
            _logger.LogError(
                ex,
                "Email give-up: '{Subject}' to {Recipient} could not be delivered via Resend after all "
                + "attempts. The message is LOST — there is no retry queue. The recipient must request it again.",
                subject,
                MaskForLog(to));
            throw new InvalidOperationException("Email send failed.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Status only, never the body — the body may carry provider detail we must not log
                // (constitution Principle I; FR-028). The recipient is logged MASKED: enough to act
                // on, without putting a full address into log storage.
                _logger.LogError(
                    "Email give-up: '{Subject}' to {Recipient} was rejected by Resend with status {Status}. "
                    + "The message is LOST — there is no retry queue.",
                    subject,
                    MaskForLog(to),
                    (int)response.StatusCode);
                throw new InvalidOperationException("Email send failed.");
            }
        }

        _logger.LogInformation("Sent '{Subject}' email via Resend", subject);
    }

    /// <summary>
    /// Renders a recipient address safe to write to a log: local part masked, control characters
    /// stripped. <c>player@example.com</c> becomes <c>p***@example.com</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two separate problems, one helper (both raised by CodeQL on this file):
    /// </para>
    /// <para>
    /// <b>Log forging</b> (<c>cs/log-forging</c>) — the address is user-supplied, so a value
    /// containing CR/LF could inject fabricated lines into the log and make the audit trail lie.
    /// Control characters are removed rather than escaped, because nothing legitimate needs them.
    /// </para>
    /// <para>
    /// <b>PII exposure</b> (<c>cs/exposure-of-sensitive-information</c>) — a full address is
    /// personal data, and log storage is the wrong place for it. Masking keeps the entry
    /// actionable (an operator sees the domain and the message kind, and can recover the full
    /// address from the users table) without persisting the address itself. This narrows FR-021 /
    /// SC-006, which originally called for naming the recipient outright; the spec was updated to
    /// match rather than left contradicting the code.
    /// </para>
    /// </remarks>
    public static string MaskForLog(string address)
    {
        var clean = new string(address.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length == 0)
        {
            return "(none)";
        }

        var at = clean.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0)
        {
            // Not an address shape — reveal nothing rather than guess where to cut.
            return "***";
        }

        return $"{clean[0]}***{clean[at..]}";
    }
}
