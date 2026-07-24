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
                to);
            throw new InvalidOperationException("Email send failed.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Status only, never the body — the body may carry provider detail we must not log
                // (constitution Principle I; FR-028). Recipient and subject ARE logged: without them
                // the entry is undiagnosable, and neither is a secret.
                _logger.LogError(
                    "Email give-up: '{Subject}' to {Recipient} was rejected by Resend with status {Status}. "
                    + "The message is LOST — there is no retry queue.",
                    subject,
                    to,
                    (int)response.StatusCode);
                throw new InvalidOperationException("Email send failed.");
            }
        }

        _logger.LogInformation("Sent '{Subject}' email via Resend", subject);
    }
}
