namespace JuggerHub.Common;

/// <summary>
/// Transactional-email settings bound from the <c>Email</c> configuration section
/// (sourced from environment / .env — never hard-coded). <see cref="Provider"/>
/// selects the <c>IEmailSender</c> implementation: <c>Smtp</c> (Mailpit locally) or
/// <c>Resend</c> (Dev/Prod).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary><c>Smtp</c> | <c>Resend</c>.</summary>
    public string Provider { get; set; } = "Smtp";

    /// <summary>
    /// From header on outgoing mail. May carry a display name in the standard
    /// <c>Name &lt;addr&gt;</c> form — both senders accept it (MailKit parses it,
    /// Resend passes it through), so the display name is configuration, not code.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Optional <c>Reply-To</c> address. Left empty by default: <see cref="FromAddress"/> is a real
    /// monitored inbox (<c>hello@juggerhub.com</c>), so replies already reach a person and a
    /// <c>Reply-To</c> equal to <c>From</c> would be a redundant header. Set this only if the
    /// <c>From</c> is ever changed to a no-reply sender; both senders accept the
    /// <c>Display Name &lt;addr&gt;</c> form here too.
    /// </summary>
    public string ReplyToAddress { get; set; } = string.Empty;

    /// <summary>SMTP host (Mailpit = <c>mailpit</c> in compose). Used when Provider=Smtp.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP port (Mailpit = 1025). Used when Provider=Smtp.</summary>
    public int SmtpPort { get; set; } = 1025;

    /// <summary>Base URL of the SPA; used to build verification / reset links in emails.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    public ResendOptions Resend { get; set; } = new();

    public sealed class ResendOptions
    {
        /// <summary>Resend API key (Dev/Prod only). Never committed.</summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}
