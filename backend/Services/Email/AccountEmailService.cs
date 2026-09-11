using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Localization;

namespace JuggerHub.Services.Email;

/// <summary>
/// Transactional email for account-lifecycle events the member performs on themselves
/// (feature 037). Currently one: the confirmation that an account was erased.
/// </summary>
/// <remarks>
/// <b>Timing is a correctness constraint, not a nicety.</b> Erasure releases the email address, so
/// this must be composed and sent <em>before</em> the address stops existing (spec FR-040). The
/// caller does that deliberately — see <c>AccountDeletionService.SendFarewellAsync</c>, which also
/// records why a delivery failure must not roll the erasure back.
/// </remarks>
public sealed class AccountEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailSender _sender;
    private readonly IRecipientCultureResolver _culture;
    private readonly IEmailLocalizer _localizer;
    private readonly AppDbContext _db;

    public AccountEmailService(
        IEmailTemplateService templates,
        IEmailSender sender,
        IRecipientCultureResolver culture,
        IEmailLocalizer localizer,
        AppDbContext db)
    {
        _templates = templates;
        _sender = sender;
        _culture = culture;
        _localizer = localizer;
        _db = db;
    }

    /// <summary>
    /// Confirm that the account was erased. Addressed to the member's own stored language — they are
    /// the recipient and this is the last thing we will ever send them.
    /// </summary>
    public async Task SendAccountDeletedNotificationAsync(User user, CancellationToken ct = default)
    {
        if (user.Email is not { Length: > 0 } address)
        {
            // Already released, or never set. Nothing to send to; not an error.
            return;
        }

        var culture = _culture.Resolve(user);
        // Read while the profile still exists: the caller sends this before the erasure runs.
        var name = await EmailRecipientName.ForAsync(_db, user, ct);
        var html = await _templates.GenerateAccountDeletedEmailAsync(
            name, address, DateTime.UtcNow, culture);

        await _sender.SendAsync(address, _localizer.Get("subject.accountDeleted", culture), html, ct);
    }
}
