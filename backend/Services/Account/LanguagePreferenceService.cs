using JuggerHub.Common;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Account;

/// <summary>
/// Persists the signed-in user's language preference with a single-row set-based update
/// (feature 031). <see cref="Entities.User"/> is the identity foundation and carries no audit
/// timestamps, so no <c>ModifiedDate</c> is stamped here.
/// </summary>
public sealed class LanguagePreferenceService : ILanguagePreferenceService
{
    private readonly AppDbContext _db;

    public LanguagePreferenceService(AppDbContext db) => _db = db;

    public async Task<bool> SetAsync(Guid userId, string language, CancellationToken ct = default)
    {
        // Defensive: normalize to the supported base tag even though the controller validates.
        var normalized = SupportedLanguages.ResolveOrDefault(language);

        var affected = await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PreferredLanguage, normalized), ct);

        return affected > 0;
    }
}
