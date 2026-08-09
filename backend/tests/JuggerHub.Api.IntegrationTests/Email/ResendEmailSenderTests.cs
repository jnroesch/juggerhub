using System.Net;
using System.Text.Json;
using JuggerHub.Common;
using JuggerHub.Services.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// The Resend payload is where the deliverability fixes actually reach the wire (the spam report):
/// a plain-text alternative on every message, a <c>List-Unsubscribe</c> header, and a <c>reply_to</c>
/// only when one is configured. These assert the JSON body a scripted transport captures, so a
/// regression in the sender — not just the helper — is caught.
/// </summary>
public sealed class ResendEmailSenderTests
{
    private const string SampleHtml =
        "<html><head><style>.b{color:red}</style></head><body><p>Hello there</p>"
        + "<a href=\"https://app.juggerhub.com/verify?userId=1&token=abc\">Verify your email</a></body></html>";

    [Fact]
    public async Task Every_message_carries_plaintext_and_a_list_unsubscribe_header()
    {
        var (handler, sender) = Build(new EmailOptions
        {
            FromAddress = "JuggerHub <hello@juggerhub.com>",
            FrontendBaseUrl = "https://app.juggerhub.com",
            Resend = new EmailOptions.ResendOptions { ApiKey = "re_test" },
        });

        await sender.SendAsync("player@example.com", "Verify your email", SampleHtml);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        var root = doc.RootElement;

        var text = root.GetProperty("text").GetString();
        Assert.Contains("Hello there", text!, StringComparison.Ordinal);
        Assert.Contains("https://app.juggerhub.com/verify?userId=1&token=abc", text!, StringComparison.Ordinal);
        Assert.DoesNotContain("color:red", text!, StringComparison.Ordinal);

        var listUnsubscribe = root.GetProperty("headers").GetProperty("List-Unsubscribe").GetString();
        Assert.Contains("<mailto:hello@juggerhub.com?subject=unsubscribe>", listUnsubscribe!, StringComparison.Ordinal);
        Assert.Contains("<https://app.juggerhub.com/settings/notifications>", listUnsubscribe!, StringComparison.Ordinal);

        // Reply-To stays off unless explicitly configured — From already routes to a monitored inbox.
        Assert.False(root.TryGetProperty("reply_to", out _));
    }

    [Fact]
    public async Task Reply_to_is_sent_only_when_configured()
    {
        var (handler, sender) = Build(new EmailOptions
        {
            FromAddress = "JuggerHub <no-reply@juggerhub.com>",
            ReplyToAddress = "JuggerHub Support <help@juggerhub.com>",
            FrontendBaseUrl = "https://app.juggerhub.com",
            Resend = new EmailOptions.ResendOptions { ApiKey = "re_test" },
        });

        await sender.SendAsync("player@example.com", "News from your team", SampleHtml);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("JuggerHub Support <help@juggerhub.com>", doc.RootElement.GetProperty("reply_to").GetString());
    }

    private static (CapturingHandler Handler, ResendEmailSender Sender) Build(EmailOptions options)
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var sender = new ResendEmailSender(
            http, Options.Create(options), NullLogger<ResendEmailSender>.Instance);
        return (handler, sender);
    }

    /// <summary>Captures the request body and answers 200, so the sender completes without a real Resend.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
