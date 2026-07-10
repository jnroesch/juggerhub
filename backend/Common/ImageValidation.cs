namespace JuggerHub.Common;

/// <summary>
/// Server-side image validation shared by upload paths (badge/achievement icons, feature 012).
/// Never trusts a client-declared content type — the type is determined by sniffing magic bytes.
/// Mirrors the approach used for profile avatars (see <c>ProfileService.SniffImageContentType</c>);
/// the two could be unified later.
/// </summary>
public static class ImageValidation
{
    /// <summary>
    /// Identify a supported image by its magic bytes (PNG / JPEG / WebP). Returns the canonical
    /// content type, or <c>null</c> if unrecognized. Declared content types are ignored.
    /// </summary>
    public static string? SniffImageContentType(byte[] b)
    {
        if (b.Length >= 8 &&
            b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
        {
            return "image/png";
        }

        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // RIFF....WEBP
        if (b.Length >= 12 &&
            b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }
}
