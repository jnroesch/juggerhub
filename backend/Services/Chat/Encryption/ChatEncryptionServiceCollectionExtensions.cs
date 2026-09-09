using JuggerHub.Common;

namespace JuggerHub.Services.Chat.Encryption;

/// <summary>
/// Registers chat message encryption (feature 047 / #223).
/// </summary>
public static class ChatEncryptionServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IChatMessageCipher"/>, reading its keys from configuration.</summary>
    /// <remarks>
    /// Configuration is read when the singleton is <em>constructed</em>, not while services are
    /// being registered. That matters: <c>Program.cs</c> already notes that a test host layers its
    /// configuration in after composition, so a value read during registration would miss it — and
    /// the same is true of any host that adds a configuration source late. Construction is forced
    /// at startup by <see cref="ValidateChatMessageEncryption"/>, so the failure is still a refusal
    /// to start rather than a surprise on the first message.
    /// </remarks>
    public static IServiceCollection AddChatMessageEncryption(this IServiceCollection services)
    {
        services.AddSingleton<IChatMessageCipher>(sp =>
        {
            var options = new ChatEncryptionOptions();
            sp.GetRequiredService<IConfiguration>()
                .GetSection(ChatEncryptionOptions.SectionName)
                .Bind(options);

            // Throws with a message naming the configuration key and never echoing key material.
            return new AesGcmChatMessageCipher(options.Parse());
        });

        return services;
    }

    /// <summary>
    /// Forces the cipher to be constructed at startup, so a missing or malformed key stops the
    /// process instead of surfacing later (FR-007).
    /// </summary>
    /// <remarks>
    /// The point of failing on a missing key is that the process does not start. Left to first use,
    /// a misconfigured deployment would pass its readiness probe, serve every other page, and then
    /// reject every message send — an invisible degradation, which is exactly what this feature
    /// exists to remove. Same reasoning as the Redis backplane guard.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No usable key is configured.</exception>
    public static void ValidateChatMessageEncryption(this IServiceProvider services) =>
        services.GetRequiredService<IChatMessageCipher>();
}
