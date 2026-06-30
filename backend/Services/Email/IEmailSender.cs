namespace JuggerHub.Services.Email;

/// <summary>
/// Sends a fully-rendered HTML email. The implementation is selected by
/// <c>Email:Provider</c> (SMTP/Mailpit locally, Resend on Dev/Prod). Rendering the
/// HTML is the job of the template service; this only transports it.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
