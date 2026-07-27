namespace JuggerHub.Services.Account;

/// <summary>
/// Persists a signed-in user's chosen interface language (feature 031, FR-005). The value is
/// applied wherever the user is signed in (via <c>/auth/me</c>) and used to localize their
/// emails/notifications.
/// </summary>
public interface ILanguagePreferenceService
{
    /// <summary>
    /// Set the user's preferred language. <paramref name="language"/> MUST already be validated as
    /// a supported tag by the caller. Returns <c>false</c> if the user row was not found.
    /// </summary>
    Task<bool> SetAsync(Guid userId, string language, CancellationToken ct = default);
}
