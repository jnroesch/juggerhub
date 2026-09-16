namespace JuggerHub.Services.Chat;

/// <summary>
/// Builds the URL a chat surface renders a player's avatar from (issue #193). Chat DTOs carry a
/// ready-to-use URL rather than a <c>hasAvatar</c> flag + client-built path (the browse convention),
/// because an inbox row's <see cref="Dtos.Chat.ConversationAvatarDto"/> has no handle to build one
/// from — so the server does it once, consistently, for every surface.
/// </summary>
/// <remarks>
/// The URL points at the same handle-keyed, visibility-gated endpoint the rest of the app uses
/// (<c>GET /api/v1/profiles/{handle}/avatar</c>, feature 035), so the ban/erasure gate is re-applied
/// on the actual byte read — the URL is only a pointer, never the bytes.
/// </remarks>
internal static class ChatAvatarUrl
{
    /// <summary>
    /// The avatar URL for a player, or <c>null</c> when there is nothing to show: no handle (a banned
    /// or erased account, whose profile is filtered out globally, so it arrives here as null), or no
    /// uploaded avatar. Returning null keeps the placeholder in place and never emits a URL that would
    /// 404 into a broken image.
    /// </summary>
    public static string? ForPlayer(string? handle, bool hasAvatar) =>
        hasAvatar && !string.IsNullOrEmpty(handle)
            ? $"/api/v1/profiles/{Uri.EscapeDataString(handle)}/avatar"
            : null;

    /// <summary>
    /// The logo URL for a team (feature 051 / #305), or <c>null</c> when the team has none — in
    /// which case the client keeps its cluster placeholder, exactly as it did before team logos
    /// existed.
    /// </summary>
    /// <remarks>
    /// Same shape and same reasoning as <see cref="ForPlayer"/>: the URL points at the
    /// slug-keyed endpoint the rest of the app uses (<c>GET /api/v1/teams/{slug}/logo</c>), so the
    /// read is authorized on the actual byte fetch and this is only ever a pointer.
    /// </remarks>
    public static string? ForTeam(string? slug, bool hasLogo) =>
        hasLogo && !string.IsNullOrEmpty(slug)
            ? $"/api/v1/teams/{Uri.EscapeDataString(slug)}/logo"
            : null;
}
