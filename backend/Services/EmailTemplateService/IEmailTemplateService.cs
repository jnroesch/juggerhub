namespace Templixir.Services;

public interface IEmailTemplateService
{
    /// <summary>
    /// Generate a password reset email
    /// </summary>
    Task<string> GeneratePasswordResetEmailAsync(string resetUrl, string resetToken, string userEmail);

    /// <summary>
    /// Generate a password change notification email
    /// </summary>
    Task<string> GeneratePasswordChangeNotificationEmailAsync(string recipientName, string recipientEmail, DateTime changeDate, string ipAddress);

    /// <summary>
    /// Generate an invitation email
    /// </summary>
    Task<string> GenerateInvitationEmailAsync(string recipientName, string inviterName, string inviterEmail, string organizationName, string invitationUrl, string role, DateTime expirationDate);
    Task<string> GenerateSubscriptionWelcomeEmailAsync(string recipientName, string planName, List<string> features);
    Task<string> GenerateEmailVerificationEmailAsync(string recipientName, string recipientEmail, string verificationUrl);
    Task<string> GenerateWelcomeEmailAsync(string recipientName, string recipientEmail, string companyName, DateTime createdDate);
    
    
} 