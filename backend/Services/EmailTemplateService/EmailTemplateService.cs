using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using JuggerHub.Common;
using JuggerHub.Services.Email;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services;

/// <summary>
/// Marks a template value as markup the product itself authored, so <see cref="EmailTemplateService"/>
/// substitutes it verbatim instead of HTML-encoding it (feature 039, FR-007).
///
/// Encoding is the default precisely because the opposite default failed: values were substituted
/// raw, and a member-authored team news post could inject live markup into a JuggerHub-branded
/// email. Opting out has to be a visible, deliberate act at the call site — never inferred from a
/// variable name or a template.
/// </summary>
public sealed record RawHtml(string Value)
{
    public override string ToString() => Value;
}

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EmailTemplateService> _logger;
    private readonly EmailOptions _emailOptions;
    private readonly IEmailLocalizer _localizer;

    // Cache templates to avoid reading files repeatedly. Keyed by "{culture}/{name}" so each
    // language's file is cached independently (feature 031).
    private static readonly Dictionary<string, string> _templateCache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Escapes the HTML-significant characters (<c>&lt; &gt; &amp; " '</c>) and nothing else.
    ///
    /// Deliberately not <see cref="WebUtility.HtmlEncode(string?)"/>, which also escapes every
    /// non-ASCII character: that turned the German and Spanish bodies into walls of numeric entities
    /// (<c>Du erh&amp;#228;ltst…</c>) for no security benefit, since a mail client renders those
    /// identically. Neutralizing markup is the requirement; mangling accented letters is not.
    /// <see cref="UnicodeRanges.All"/> is the framework-supported way to say exactly that.
    /// </summary>
    private static readonly HtmlEncoder MarkupEncoder =
        HtmlEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));

    public EmailTemplateService(
        IWebHostEnvironment environment,
        ILogger<EmailTemplateService> logger,
        IOptions<EmailOptions> emailOptions,
        IEmailLocalizer localizer)
    {
        _environment = environment;
        _logger = logger;
        _emailOptions = emailOptions.Value;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<string> GeneratePasswordResetEmailAsync(string resetUrl, string resetToken, string userEmail, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = _localizer.Get("title.passwordReset", culture),
            ["RESET_URL"] = resetUrl,
            ["RESET_TOKEN"] = resetToken,
            ["USER_EMAIL"] = userEmail,
            ["FOOTER_REASON"] = _localizer.Get("footer.passwordReset", culture)
        };

        return await GenerateEmailAsync("password-reset", variables, culture);
    }

    /// <inheritdoc />
    public async Task<string> GenerateInvitationEmailAsync(string recipientName, string inviterName, string inviterEmail, string organizationName, string invitationUrl, string role, DateTime expirationDate)
    {
        var variables = new Dictionary<string, object>
        {
            {"EMAIL_TITLE", $"{inviterName} invited you to join {organizationName}"},
            {"RECIPIENT_NAME", recipientName},
            {"INVITER_NAME", inviterName},
            {"INVITER_EMAIL", inviterEmail},
            {"ORGANIZATION_NAME", organizationName},
            {"INVITATION_URL", invitationUrl},
            {"USER_ROLE", role},
            {"EXPIRATION_DATE", expirationDate.ToString("MMMM dd, yyyy")},
            {"EXPIRATION_TIME", expirationDate.ToString("HH:mm")},
            {"FOOTER_REASON", $"You're getting this because {inviterName} invited you to their team on JuggerHub."}
        };

        return await GenerateEmailAsync("invitation", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateSubscriptionWelcomeEmailAsync(string recipientName, string planName, List<string> features)
    {
        var variables = new Dictionary<string, object>
        {
            {"EMAIL_TITLE", $"Your JuggerHub {planName} plan is active"},
            {"RECIPIENT_NAME", recipientName},
            {"PLAN_NAME", planName},
            // Product-authored markup: the list separator is a <br/>, so this is one of the two
            // values that must opt out of the default encoding (feature 039, FR-007).
            {"PLAN_FEATURES", new RawHtml(string.Join("<br/>", features.Select(f => $"• {MarkupEncoder.Encode(f)}")))},
            {"FOOTER_REASON", $"You're getting this because you subscribed to JuggerHub {planName}."}
        };

        return await GenerateEmailAsync("subscription-welcome", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateEmailVerificationEmailAsync(string recipientName, string recipientEmail, string verificationUrl, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            {"EMAIL_TITLE", _localizer.Get("title.verification", culture)},
            {"USER_NAME", recipientName},
            {"USER_EMAIL", recipientEmail},
            {"VERIFICATION_URL", verificationUrl},
            {"FOOTER_REASON", _localizer.Get("footer.verification", culture)}
        };

        return await GenerateEmailAsync("email-verification", variables, culture);
    }

    /// <inheritdoc />
    public async Task<string> GenerateWelcomeEmailAsync(string recipientName, string recipientEmail, string companyName, DateTime createdDate, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            {"EMAIL_TITLE", _localizer.Get("title.welcome", culture)},
            {"USER_NAME", recipientName},
            {"USER_EMAIL", recipientEmail},
            {"COMPANY_NAME", companyName},
            {"CREATED_DATE", createdDate.ToString("MMMM dd, yyyy")},
            {"FOOTER_REASON", _localizer.Get("footer.welcome", culture)}
        };

        return await GenerateEmailAsync("welcome-email", variables, culture);
    }

    /// <inheritdoc />
    public async Task<string> GeneratePasswordChangeNotificationEmailAsync(string recipientName, string recipientEmail, DateTime changeDate, string ipAddress, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = _localizer.Get("title.passwordChanged", culture),
            ["RECIPIENT_NAME"] = recipientName,
            ["RECIPIENT_EMAIL"] = recipientEmail,
            ["CHANGE_DATE"] = changeDate.ToString("MMMM dd, yyyy"),
            ["CHANGE_TIME"] = changeDate.ToString("HH:mm:ss UTC"),
            ["IP_ADDRESS"] = ipAddress,
            ["FOOTER_REASON"] = _localizer.Get("footer.passwordChanged", culture)
        };

        return await GenerateEmailAsync("password-change-notification", variables, culture);
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
            // Product-authored markup: a CSS declaration list injected into a style attribute.
            ["STATUS_STYLE"] = new RawHtml(statusStyle),
            ["UNUSUAL_REASONS"] = unusualReasons
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
            ["CURRENT_YEAR"] = DateTime.Now.Year
        };

        return await GenerateEmailAsync("access-request", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateTeamRoleChangedEmailAsync(string teamName, string teamUrl, string? actorName, string roleLabel, string rolePhrase)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = $"Your role in {teamName} changed",
            ["TEAM_NAME"] = teamName,
            ["TEAM_URL"] = teamUrl,
            ["ACTOR_LINE"] = string.IsNullOrWhiteSpace(actorName) ? "Your role was updated." : $"{actorName} updated your role.",
            ["ROLE_LABEL"] = roleLabel,
            ["ROLE_PHRASE"] = rolePhrase,
            ["FOOTER_REASON"] = "You're getting this because your role on a JuggerHub team changed."
        };

        return await GenerateEmailAsync("team-role-changed", variables);
    }

    /// <inheritdoc />
    public async Task<string> GenerateTeamNewsEmailAsync(string teamName, string teamUrl, string? authorName, string excerpt)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = $"News from {teamName}",
            ["TEAM_NAME"] = teamName,
            ["TEAM_URL"] = teamUrl,
            ["AUTHOR_LINE"] = string.IsNullOrWhiteSpace(authorName) ? "Someone" : authorName!,
            ["NEWS_EXCERPT"] = excerpt,
            ["FOOTER_REASON"] = "You're getting this because you're a member of this team on JuggerHub."
        };

        return await GenerateEmailAsync("team-news", variables);
    }

    // --- Feature 039 -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<string> GenerateEventCancelledEmailAsync(string eventName, string eventUrl, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = _localizer.Get("title.eventCancelled", culture),
            ["EVENT_NAME"] = eventName,
            ["EVENT_URL"] = eventUrl,
            ["FOOTER_REASON"] = _localizer.Get("footer.eventCancelled", culture),
        };

        return await GenerateEmailAsync("event-cancelled", variables, culture);
    }

    /// <inheritdoc />
    public async Task<string> GeneratePartyRequestEmailAsync(
        string recipientName, string teamName, string eventName, string partyUrl, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = _localizer.Get("title.partyRequest", culture),
            ["RECIPIENT_NAME"] = recipientName,
            ["TEAM_NAME"] = teamName,
            ["EVENT_NAME"] = eventName,
            ["PARTY_URL"] = partyUrl,
            ["FOOTER_REASON"] = _localizer.Get("footer.partyRequest", culture),
        };

        return await GenerateEmailAsync("party-request", variables, culture);
    }

    /// <inheritdoc />
    public async Task<string> GeneratePartyNewsEmailAsync(
        string recipientName, string teamName, string eventName, string excerpt, string partyUrl, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = _localizer.Get("title.partyNews", culture),
            ["RECIPIENT_NAME"] = recipientName,
            ["TEAM_NAME"] = teamName,
            ["EVENT_NAME"] = eventName,
            ["NEWS_EXCERPT"] = excerpt,
            ["PARTY_URL"] = partyUrl,
            ["FOOTER_REASON"] = _localizer.Get("footer.partyNews", culture),
        };

        return await GenerateEmailAsync("party-news", variables, culture);
    }

    /// <inheritdoc />
    public async Task<string> GenerateMarketInviteEmailAsync(
        string recipientName, string teamName, string eventName, string inviterName, string eventUrl, string culture = SupportedLanguages.Default)
    {
        var variables = new Dictionary<string, object>
        {
            ["EMAIL_TITLE"] = _localizer.Get("title.marketInvite", culture),
            ["RECIPIENT_NAME"] = recipientName,
            ["TEAM_NAME"] = teamName,
            ["EVENT_NAME"] = eventName,
            ["INVITER_NAME"] = inviterName,
            ["EVENT_URL"] = eventUrl,
            ["FOOTER_REASON"] = _localizer.Get("footer.marketInvite", culture),
        };

        return await GenerateEmailAsync("market-invite", variables, culture);
    }

    /// <summary>
    /// Every template is wrapped in the shared header/footer, so the SPA links those chrome
    /// pieces need are supplied here rather than by each caller. The base URL is
    /// <see cref="EmailOptions.FrontendBaseUrl"/> — the same value the auth links are built
    /// from, so an email can never point at a different host than the link beside it.
    /// A caller may still pass its own value; <c>TryAdd</c> leaves it alone.
    /// </summary>
    private void AddSharedUrls(Dictionary<string, object> variables)
    {
        var baseUrl = _emailOptions.FrontendBaseUrl.TrimEnd('/');

        if (baseUrl.Length == 0)
        {
            // Not fatal here, but every link in this email is about to be href="" — and the
            // verification/reset links built elsewhere from the same setting are equally dead.
            // Say so, because a silently linkless email looks like a template bug for a while.
            _logger.LogWarning(
                "Email:FrontendBaseUrl is not configured — links in outgoing email will be empty.");
        }

        variables.TryAdd("DASHBOARD_URL", baseUrl);
        variables.TryAdd("SETTINGS_URL", $"{baseUrl}/settings/notifications");

        // Legal reachability from every email (feature 039). Built from the same base URL as every
        // other link, so a message can never point a reader at a different origin than the one
        // beside it. The pages themselves shipped with 036 and are anonymously readable.
        variables.TryAdd("PRIVACY_URL", $"{baseUrl}/privacy");
        variables.TryAdd("IMPRINT_URL", $"{baseUrl}/imprint");
    }

    private async Task<string> GenerateEmailAsync(string templateName, Dictionary<string, object> variables, string culture = SupportedLanguages.Default)
    {
        try
        {
            AddSharedUrls(variables);

            // Load templates for the requested culture; each falls back to English (FR-008).
            var baseTemplate = await LoadTemplateAsync("base-styles.html", culture);
            var headerTemplate = await LoadTemplateAsync("header.html", culture);
            var contentTemplate = await LoadTemplateAsync($"{templateName}.html", culture);
            var footerTemplate = await LoadTemplateAsync("footer.html", culture);

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

    /// <summary>
    /// Load a template for a culture (feature 031). Looks in <c>EmailTemplates/{culture}/</c> and
    /// falls back to <c>EmailTemplates/en/</c> when that language has no localized file yet, so a
    /// partially-translated set still renders (in English) rather than failing (FR-008).
    /// </summary>
    private async Task<string> LoadTemplateAsync(string templateName, string culture = SupportedLanguages.Default)
    {
        var resolvedCulture = SupportedLanguages.ResolveOrDefault(culture);
        var cacheKey = $"{resolvedCulture}/{templateName}";

        lock (_cacheLock)
        {
            if (_templateCache.TryGetValue(cacheKey, out var cachedTemplate))
            {
                return cachedTemplate;
            }
        }

        var root = Path.Combine(_environment.ContentRootPath, "EmailTemplates");
        var localizedPath = Path.Combine(root, resolvedCulture, templateName);
        var fallbackPath = Path.Combine(root, SupportedLanguages.Default, templateName);

        var templatePath = File.Exists(localizedPath) ? localizedPath
            : File.Exists(fallbackPath) ? fallbackPath
            : throw new FileNotFoundException($"Email template not found: {localizedPath}");

        var template = await File.ReadAllTextAsync(templatePath);

        lock (_cacheLock)
        {
            _templateCache[cacheKey] = template;
        }

        return template;
    }

    /// <summary>
    /// Substitutes <c>{{PLACEHOLDER}}</c> values into a template, <b>HTML-encoding every value</b>
    /// unless it is a <see cref="RawHtml"/> (feature 039, FR-006/FR-007).
    ///
    /// Encoding lives here rather than at each call site on purpose: this is the single point every
    /// templated value passes through, so a new template cannot forget it. The previous
    /// per-call-site arrangement is what allowed <c>team-news.html</c> to render a member-authored
    /// news body as live markup.
    ///
    /// Subjects never reach this method — they are composed by the calling service and stay
    /// unencoded (FR-010), because a mail client renders a subject as text, not markup.
    /// </summary>
    private string ReplaceVariables(string template, Dictionary<string, object> variables)
    {
        var result = template;

        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            var value = variable.Value switch
            {
                null => string.Empty,
                RawHtml raw => raw.Value,
                var v => MarkupEncoder.Encode(v.ToString() ?? string.Empty),
            };

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
} 