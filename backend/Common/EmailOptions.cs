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

    /// <summary>From address on outgoing auth emails.</summary>
    public string FromAddress { get; set; } = string.Empty;

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
