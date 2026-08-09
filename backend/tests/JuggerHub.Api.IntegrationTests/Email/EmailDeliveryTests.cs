using JuggerHub.Common;
using JuggerHub.Services.Email;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// Unit coverage for the deliverability envelope shared by both senders (the spam-in-inbox report):
/// the plain-text alternative, the <c>List-Unsubscribe</c> header, and <c>Reply-To</c> parsing.
/// Pure — no web factory — because this is string-to-string logic.
/// </summary>
public sealed class EmailDeliveryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToPlainText_returns_empty_for_no_input(string? html)
    {
        Assert.Equal(string.Empty, EmailDelivery.ToPlainText(html));
    }

    [Fact]
    public void ToPlainText_drops_head_style_and_script_so_no_css_leaks_in()
    {
        const string html =
            "<html><head><title>Ignore</title><style>.button{color:#fff;background:#000}</style></head>"
            + "<body><p>Welcome to JuggerHub</p><script>alert(1)</script></body></html>";

        var text = EmailDelivery.ToPlainText(html);

        Assert.Contains("Welcome to JuggerHub", text, StringComparison.Ordinal);
        Assert.DoesNotContain("color:#fff", text, StringComparison.Ordinal);
        Assert.DoesNotContain("alert(1)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ignore", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<", text, StringComparison.Ordinal);
        Assert.DoesNotContain(">", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPlainText_keeps_the_action_link_url_which_is_the_point_of_the_mail()
    {
        // The href is not HTML-encoded in the templates (RawHtml), so the raw ampersand is literal —
        // exactly what a plain-text reader must be able to copy. If the URL were stripped with the
        // tag, a text-only client would get an unusable "click here".
        const string html =
            "<p>Confirm your address:</p>"
            + "<a href=\"https://juggerhub.com/verify?userId=42&token=abc123\" class=\"button\">Verify your email</a>";

        var text = EmailDelivery.ToPlainText(html);

        Assert.Contains("Verify your email", text, StringComparison.Ordinal);
        Assert.Contains("https://juggerhub.com/verify?userId=42&token=abc123", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPlainText_decodes_entities_and_normalises_whitespace()
    {
        const string html = "<p>Jan&nbsp;&amp;&nbsp;team</p>\n\n\n<div>See&nbsp;you</div>";

        var text = EmailDelivery.ToPlainText(html);

        // &nbsp; -> ordinary space, &amp; -> &, and no run of 3+ blank lines survives.
        Assert.Contains("Jan & team", text, StringComparison.Ordinal);
        Assert.Contains("See you", text, StringComparison.Ordinal);
        Assert.DoesNotContain("&nbsp;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListUnsubscribe_emits_mailto_and_settings_link_when_both_are_available()
    {
        var options = new EmailOptions
        {
            FromAddress = "JuggerHub <hello@juggerhub.com>",
            FrontendBaseUrl = "https://app.juggerhub.com/",
        };

        var value = EmailDelivery.BuildListUnsubscribe(options);

        Assert.Equal(
            "<mailto:hello@juggerhub.com?subject=unsubscribe>, <https://app.juggerhub.com/settings/notifications>",
            value);
    }

    [Fact]
    public void BuildListUnsubscribe_falls_back_to_mailto_only_without_a_frontend_url()
    {
        var options = new EmailOptions { FromAddress = "hello@juggerhub.com", FrontendBaseUrl = string.Empty };

        var value = EmailDelivery.BuildListUnsubscribe(options);

        Assert.Equal("<mailto:hello@juggerhub.com?subject=unsubscribe>", value);
    }

    [Fact]
    public void BuildListUnsubscribe_is_null_when_nothing_can_be_advertised()
    {
        var options = new EmailOptions { FromAddress = "not-an-address", FrontendBaseUrl = string.Empty };

        Assert.Null(EmailDelivery.BuildListUnsubscribe(options));
    }

    [Theory]
    [InlineData("hello@juggerhub.com", "hello@juggerhub.com")]
    [InlineData("JuggerHub <hello@juggerhub.com>", "hello@juggerhub.com")]
    [InlineData("  hello@juggerhub.com  ", "hello@juggerhub.com")]
    public void BareAddress_extracts_the_address(string input, string expected)
    {
        Assert.Equal(expected, EmailDelivery.BareAddress(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-address")]
    [InlineData("two words")]
    public void BareAddress_is_null_for_non_addresses(string? input)
    {
        Assert.Null(EmailDelivery.BareAddress(input));
    }
}
