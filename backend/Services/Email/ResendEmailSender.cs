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

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend email send failed with status {Status}", (int)response.StatusCode);
            throw new InvalidOperationException("Email send failed.");
        }

        _logger.LogInformation("Sent '{Subject}' email via Resend", subject);
    }
}
