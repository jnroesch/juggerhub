using System.Globalization;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Localization;

/// <summary>
/// Decides which language backend-generated content (emails, notification mirrors) is rendered in
/// (feature 031, research D9). The rule depends on <em>who the content is for</em>:
/// <list type="bullet">
///   <item>Recipient-addressed content uses the <b>recipient's</b> stored preference — never the
///     actor's language — falling back to the ambient request culture, then English.</item>
///   <item>Pre-account content addressed to the caller themselves (verify/reset/resend) uses the
///     <b>request</b> culture, which RequestLocalization set from the caller's <c>Accept-Language</c>
///     (the frontend stamps the effective, post-override language there — FR-012a).</item>
/// </list>
/// </summary>
public interface IRecipientCultureResolver
{
    /// <summary>Culture for content addressed to a known recipient user (FR-012).</summary>
    string Resolve(User user);

    /// <summary>Culture for content addressed to the current caller themselves (FR-012a).</summary>
    string ResolveFromRequest();

    /// <summary>
    /// Culture for content addressed to a raw email: the matching user's preference if one exists,
    /// else the request culture, else English.
    /// </summary>
    Task<string> ResolveByEmailAsync(string email, CancellationToken ct = default);
}

public sealed class RecipientCultureResolver : IRecipientCultureResolver
{
    private readonly AppDbContext _db;

    public RecipientCultureResolver(AppDbContext db) => _db = db;

    public string Resolve(User user) =>
        SupportedLanguages.ResolveOrDefault(user.PreferredLanguage ?? CultureInfo.CurrentUICulture.Name);

    public string ResolveFromRequest() =>
        SupportedLanguages.ResolveOrDefault(CultureInfo.CurrentUICulture.Name);

    public async Task<string> ResolveByEmailAsync(string email, CancellationToken ct = default)
    {
        var pref = await _db.Users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(ct);

        return SupportedLanguages.ResolveOrDefault(pref ?? CultureInfo.CurrentUICulture.Name);
    }
}
