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
}
