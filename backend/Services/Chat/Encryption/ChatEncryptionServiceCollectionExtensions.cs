using JuggerHub.Common;

namespace JuggerHub.Services.Chat.Encryption;

/// <summary>
/// Registers chat message encryption (feature 047 / #223).
/// </summary>
public static class ChatEncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Parses and validates <c>Chat:Encryption:Keys</c> and registers <see cref="IChatMessageCipher"/>.
    /// </summary>
    /// <remarks>
    /// Validation happens <b>here</b>, at registration, rather than lazily on first use: the point
    /// of failing on a missing key is that the process does not start, and a check deferred to the
    /// first message would let a misconfigured deployment pass its readiness probe and then reject
    /// every send. This mirrors the Redis backplane guard in <c>Program.cs</c>, which exists for
    /// the same reason — an invisible degradation is worse than a loud refusal.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No usable key is configured (FR-007).</exception>
    public static IServiceCollection AddChatMessageEncryption(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new ChatEncryptionOptions();
        configuration.GetSection(ChatEncryptionOptions.SectionName).Bind(options);

        // Throws with a message naming the configuration key and never echoing key material.
        var keys = options.Parse();

        services.AddSingleton<IChatMessageCipher>(new AesGcmChatMessageCipher(keys));
        return services;
    }
}
