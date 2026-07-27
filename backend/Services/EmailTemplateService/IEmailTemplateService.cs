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

    /// <summary>Generate a team role-change email (feature 011). <paramref name="rolePhrase"/> is a
    /// natural phrase like "an admin"; <paramref name="roleLabel"/> is the badge, e.g. "Admin".</summary>
    Task<string> GenerateTeamRoleChangedEmailAsync(string teamName, string teamUrl, string? actorName, string roleLabel, string rolePhrase);

    /// <summary>Generate a team-news email (feature 011) with a short body excerpt.</summary>
    Task<string> GenerateTeamNewsEmailAsync(string teamName, string teamUrl, string? authorName, string excerpt);
}