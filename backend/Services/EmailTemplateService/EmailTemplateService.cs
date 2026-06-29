using System.Text;

namespace JuggerHub.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EmailTemplateService> _logger;
    private readonly IConfiguration _configuration;
    
    // Cache templates to avoid reading files repeatedly
    private static readonly Dictionary<string, string> _templateCache = new();
    private static readonly object _cacheLock = new();

    public EmailTemplateService(
        IWebHostEnvironment environment, 
        ILogger<EmailTemplateService> logger,
        IConfiguration configuration)
    {
        _environment = environment;
        _logger = logger;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<string> GeneratePasswordResetEmailAsync(string resetUrl, string resetToken, string userEmail)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = "Reset Your Password - JuggerHub",
            ["RESET_URL"] = resetUrl,
            ["RESET_TOKEN"] = resetToken,
            ["USER_EMAIL"] = userEmail,
            ["DASHBOARD_URL"] = GetConfigValue("EmailSettings:FrontendUrl", "https://app.juggerhub.com")
        };

        return await GenerateEmailAsync("password-reset", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateInvitationEmailAsync(string recipientName, string inviterName, string inviterEmail, string organizationName, string invitationUrl, string role, DateTime expirationDate)
    {
        var variables = new Dictionary<string, object>
        {
            {"RECIPIENT_NAME", recipientName},
            {"INVITER_NAME", inviterName},
            {"INVITER_EMAIL", inviterEmail},
            {"ORGANIZATION_NAME", organizationName},
            {"INVITATION_URL", invitationUrl},
            {"USER_ROLE", role},
            {"EXPIRATION_DATE", expirationDate.ToString("MMMM dd, yyyy")},
            {"EXPIRATION_TIME", expirationDate.ToString("HH:mm")}
        };

        return await GenerateEmailAsync("invitation", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateSubscriptionWelcomeEmailAsync(string recipientName, string planName, List<string> features)
    {
        var variables = new Dictionary<string, object>
        {
            {"RECIPIENT_NAME", recipientName},
            {"PLAN_NAME", planName},
            {"PLAN_FEATURES", string.Join("<br/>", features.Select(f => $"• {f}"))}
        };

        return await GenerateEmailAsync("subscription-welcome", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateEmailVerificationEmailAsync(string recipientName, string recipientEmail, string verificationUrl)
    {
        var variables = new Dictionary<string, object>
        {
            {"USER_NAME", recipientName},
            {"USER_EMAIL", recipientEmail},
            {"VERIFICATION_URL", verificationUrl}
        };

        return await GenerateEmailAsync("email-verification", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateWelcomeEmailAsync(string recipientName, string recipientEmail, string companyName, DateTime createdDate)
    {
        var variables = new Dictionary<string, object>
        {
            {"USER_NAME", recipientName},
            {"USER_EMAIL", recipientEmail},
            {"COMPANY_NAME", companyName},
            {"DASHBOARD_URL", GetConfigValue("EmailSettings:FrontendUrl", "https://app.juggerhub.com")},
            {"CREATED_DATE", createdDate.ToString("MMMM dd, yyyy")}
        };

        return await GenerateEmailAsync("welcome-email", variables);
    }

    /// <inheritdoc />
    public async Task<string> GeneratePasswordChangeNotificationEmailAsync(string recipientName, string recipientEmail, DateTime changeDate, string ipAddress)
    {
        var variables = new Dictionary<string, object>
        {
            ["RECIPIENT_NAME"] = recipientName,
            ["RECIPIENT_EMAIL"] = recipientEmail,
            ["CHANGE_DATE"] = changeDate.ToString("MMMM dd, yyyy"),
            ["CHANGE_TIME"] = changeDate.ToString("HH:mm:ss UTC"),
            ["IP_ADDRESS"] = ipAddress,
            ["DASHBOARD_URL"] = GetConfigValue("EmailSettings:FrontendUrl", "https://app.juggerhub.com")
        };

        return await GenerateEmailAsync("password-change-notification", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateUnusualLoginNotificationEmailAsync(string recipientName, string recipientEmail, DateTime loginTime, string ipAddress, string location, string deviceInfo, bool isSuccessful, string unusualReasons)
    {
        var statusStyle = isSuccessful 
            ? "background-color: #d4edda; color: #155724; padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500;"
            : "background-color: #f8d7da; color: #721c24; padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500;";

        var variables = new Dictionary<string, object>
        {
            ["RECIPIENT_NAME"] = recipientName,
            ["RECIPIENT_EMAIL"] = recipientEmail,
            ["LOGIN_TIME"] = loginTime.ToString("MMMM dd, yyyy at HH:mm:ss UTC"),
            ["IP_ADDRESS"] = ipAddress,
            ["LOCATION"] = location,
            ["DEVICE_INFO"] = deviceInfo,
            ["LOGIN_STATUS"] = isSuccessful ? "Successful" : "Failed",
            ["STATUS_STYLE"] = statusStyle,
            ["UNUSUAL_REASONS"] = unusualReasons,
            ["DASHBOARD_URL"] = GetConfigValue("EmailSettings:FrontendUrl", "https://app.juggerhub.com")
        };

        return await GenerateEmailAsync("unusual-login", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateAccessRequestEmailAsync(string ownerName, string ownerEmail, string templateName, string requesterEmail, string message)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = "Access Request - JuggerHub",
            ["OWNER_NAME"] = ownerName,
            ["OWNER_EMAIL"] = ownerEmail,
            ["TEMPLATE_NAME"] = templateName,
            ["REQUESTER_EMAIL"] = string.IsNullOrWhiteSpace(requesterEmail) ? "anonymous" : requesterEmail,
            ["REQUEST_MESSAGE"] = message,
            ["DASHBOARD_URL"] = GetConfigValue("EmailSettings:FrontendUrl", "https://app.juggerhub.com"),
            ["CURRENT_YEAR"] = DateTime.Now.Year
        };

        return await GenerateEmailAsync("access-request", variables);
    }

    private async Task<string> GenerateEmailAsync(string templateName, Dictionary<string, object> variables)
    {
        try
        {
            // Load templates
            var baseTemplate = await LoadTemplateAsync("base-styles.html");
            var headerTemplate = await LoadTemplateAsync("header.html");
            var contentTemplate = await LoadTemplateAsync($"{templateName}.html");
            var footerTemplate = await LoadTemplateAsync("footer.html");

            // Combine templates
            var fullContent = headerTemplate + contentTemplate + footerTemplate;
            var emailHtml = baseTemplate.Replace("{{EMAIL_CONTENT}}", fullContent);

            // Replace variables
            emailHtml = ReplaceVariables(emailHtml, variables);

            return emailHtml;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating email template: {TemplateName}", templateName);
            throw;
        }
    }

    private async Task<string> LoadTemplateAsync(string templateName)
    {
        lock (_cacheLock)
        {
            if (_templateCache.TryGetValue(templateName, out var cachedTemplate))
            {
                return cachedTemplate;
            }
        }

        var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", templateName);
        
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Email template not found: {templatePath}");
        }

        var template = await File.ReadAllTextAsync(templatePath);

        lock (_cacheLock)
        {
            _templateCache[templateName] = template;
        }

        return template;
    }

    private string ReplaceVariables(string template, Dictionary<string, object> variables)
    {
        var result = template;

        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            var value = variable.Value?.ToString() ?? string.Empty;
            
            // Handle conditional blocks like {{#if CONDITION}}...{{/if}}
            if (variable.Value is bool boolValue)
            {
                var ifBlock = $"{{{{#if {variable.Key}}}}}";
                var endBlock = $"{{{{/if}}}}";
                
                var startIndex = result.IndexOf(ifBlock);
                if (startIndex >= 0)
                {
                    var endIndex = result.IndexOf(endBlock, startIndex);
                    if (endIndex >= 0)
                    {
                        var blockContent = result.Substring(startIndex + ifBlock.Length, 
                            endIndex - startIndex - ifBlock.Length);
                        
                        var replacement = boolValue ? blockContent : string.Empty;
                        result = result.Substring(0, startIndex) + replacement + 
                                result.Substring(endIndex + endBlock.Length);
                    }
                }
            }
            
            result = result.Replace(placeholder, value);
        }

        // Clean up any remaining conditional blocks
        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\{\{#if\s+\w+\}\}.*?\{\{/if\}\}", 
            string.Empty, 
            System.Text.RegularExpressions.RegexOptions.Singleline);

        return result;
    }

    private string GetConfigValue(string key, string defaultValue)
    {
        return _configuration[key] ?? defaultValue;
    }
} 