using JuggerHub.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace JuggerHub.Services.Email;

/// <summary>
/// SMTP sender (MailKit) used for local development against Mailpit, which accepts
/// plain SMTP with no auth/TLS on port 1025. Selected when <c>Email:Provider=Smtp</c>.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient
        {
            // Nothing waits forever (constitution VII). MailKit talks over a raw socket, so the
            // shared HTTP resilience handler cannot reach this sender — but the hang-protection
            // half of the rule still applies, and a wedged SMTP server must not hold a request
            // open indefinitely.
            //
            // Retry and circuit breaking are deliberately ABSENT here: this sender is selected only
            // when Email:Provider=Smtp, which per the constitution's stack table means local
            // development against Mailpit on the same compose network, where transient network
            // faults are not a real failure mode. Recorded as an accepted deviation in
            // specs/028-network-resilience/plan.md (Complexity Tracking). If SMTP ever becomes a
            // deployed path, it needs the full policy — via a provider-agnostic pipeline around
            // IEmailSender, not by bolting a retry loop on here.
            Timeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds,
        };
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.None, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Sent '{Subject}' email via SMTP {Host}:{Port}", subject, _options.SmtpHost, _options.SmtpPort);
    }
}
