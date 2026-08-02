namespace JuggerHub.Services;

public interface IEmailTemplateService
{
    /// <summary>
    /// Generate a password reset email. <paramref name="culture"/> selects the localized template
    /// and subject-adjacent copy (feature 031); defaults to English.
    /// </summary>
    Task<string> GeneratePasswordResetEmailAsync(string resetUrl, string resetToken, string userEmail, string culture = Common.SupportedLanguages.Default);

    /// <summary>
    /// Generate a password change notification email (localized by <paramref name="culture"/>).
    /// </summary>
    Task<string> GeneratePasswordChangeNotificationEmailAsync(string recipientName, string recipientEmail, DateTime changeDate, string ipAddress, string culture = Common.SupportedLanguages.Default);

    /// <summary>
    /// Generate an invitation email
    /// </summary>
    Task<string> GenerateInvitationEmailAsync(string recipientName, string inviterName, string inviterEmail, string organizationName, string invitationUrl, string role, DateTime expirationDate);
    Task<string> GenerateSubscriptionWelcomeEmailAsync(string recipientName, string planName, List<string> features);

    /// <summary>Generate an email-verification email (localized by <paramref name="culture"/>).</summary>
    Task<string> GenerateEmailVerificationEmailAsync(string recipientName, string recipientEmail, string verificationUrl, string culture = Common.SupportedLanguages.Default);

    /// <summary>Generate a welcome email (localized by <paramref name="culture"/>).</summary>
    Task<string> GenerateWelcomeEmailAsync(string recipientName, string recipientEmail, string companyName, DateTime createdDate, string culture = Common.SupportedLanguages.Default);

    /// <summary>
    /// Generate the account-erased confirmation (feature 037). Sent while the address still exists,
    /// because erasure releases it. Carries no link back into the product — there is no account to
    /// return to — and no restore offer, because erasure is terminal (spec FR-029).
    /// </summary>
    Task<string> GenerateAccountDeletedEmailAsync(string recipientName, string recipientEmail, DateTime deletedAt, string culture = Common.SupportedLanguages.Default);

    /// <summary>Generate a team role-change email (feature 011). <paramref name="rolePhrase"/> is a
    /// natural phrase like "an admin"; <paramref name="roleLabel"/> is the badge, e.g. "Admin".</summary>
    Task<string> GenerateTeamRoleChangedEmailAsync(string teamName, string teamUrl, string? actorName, string roleLabel, string rolePhrase);

    /// <summary>Generate a team-news email (feature 011) with a short body excerpt.</summary>
    Task<string> GenerateTeamNewsEmailAsync(string teamName, string teamUrl, string? authorName, string excerpt);

    // --- Feature 039 -----------------------------------------------------------------------
    // The four emails that were composed as inline HTML until this feature. All take the
    // recipient's culture, because they are addressed to a recipient rather than to the caller.

    /// <summary>Generate an event-cancellation email (feature 039), localized by <paramref name="culture"/>.</summary>
    Task<string> GenerateEventCancelledEmailAsync(string eventName, string eventUrl, string culture = Common.SupportedLanguages.Default);

    /// <summary>
    /// Generate a party participation-request email (feature 039). Serves both the initial request
    /// and the nudge — the message is the same either way.
    /// </summary>
    Task<string> GeneratePartyRequestEmailAsync(string recipientName, string teamName, string eventName, string partyUrl, string culture = Common.SupportedLanguages.Default);

    /// <summary>
    /// Generate a party-news email (feature 039). <paramref name="excerpt"/> is expected to be
    /// already truncated by the caller, matching the team-news treatment.
    /// </summary>
    Task<string> GeneratePartyNewsEmailAsync(string recipientName, string teamName, string eventName, string excerpt, string partyUrl, string culture = Common.SupportedLanguages.Default);

    /// <summary>Generate a marketplace-invite email (feature 039), localized by <paramref name="culture"/>.</summary>
    Task<string> GenerateMarketInviteEmailAsync(string recipientName, string teamName, string eventName, string inviterName, string eventUrl, string culture = Common.SupportedLanguages.Default);
}