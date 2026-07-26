namespace JuggerHub.Dtos.Account;

/// <summary>
/// Body of <c>PUT /account/language</c> (feature 031). Carries the language the signed-in user
/// has chosen. Validated against the supported allowlist server-side — never trust the client.
/// </summary>
public sealed record UpdateLanguageRequest(string Language);
