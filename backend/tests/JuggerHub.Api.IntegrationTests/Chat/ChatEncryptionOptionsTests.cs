using JuggerHub.Common;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Startup validation of the chat encryption key configuration (feature 047 / #223).
/// Covers contracts/chat-message-cipher.md cases S1–S6.
/// </summary>
/// <remarks>
/// Two things are asserted about every failure, and the second matters as much as the first:
/// that the message names the configuration key so an operator knows where to look, and that it
/// contains <b>no key material</b>. Configuration errors are exactly the moment logs get pasted
/// into an issue.
/// </remarks>
public sealed class ChatEncryptionOptionsTests
{
    private const string Key32 = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="; // 32 bytes
    private const string OtherKey32 = "/v37+vn49/b18/Lx8O/u7ezr6uno5+bl5OPi4eDf3t0=";

    private static ChatEncryptionOptions Options(string keys) => new() { Keys = keys };

    private static void AssertRejects(string keys, params string[] mustNotAppear)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Options(keys).Parse());

        Assert.Contains("Chat:Encryption:Keys", ex.Message, StringComparison.Ordinal);
        foreach (var secret in mustNotAppear)
        {
            Assert.DoesNotContain(secret, ex.Message, StringComparison.Ordinal);
        }
    }

    // --- S6: the happy paths ---------------------------------------------------

    [Fact]
    public void A_single_key_parses_and_is_the_write_key()
    {
        var keys = Options($"1:{Key32}").Parse();

        Assert.Single(keys);
        Assert.Equal(1, keys[0].Version);
        Assert.Equal(ChatEncryptionOptions.KeySizeBytes, keys[0].Key.Length);
    }

    [Fact]
    public void Order_is_preserved_so_the_first_entry_is_the_writer()
    {
        // The whole point of encoding "which key writes" as position: there is no second setting
        // that can disagree with this one.
        var keys = Options($"2:{OtherKey32};1:{Key32}").Parse();

        Assert.Equal(2, keys.Count);
        Assert.Equal(2, keys[0].Version);
        Assert.Equal(1, keys[1].Version);
    }

    [Fact]
    public void Whitespace_and_trailing_separators_are_tolerated()
    {
        // A secret pasted through GitHub Environments and a Kubernetes Secret picks these up.
        var keys = Options($" 2:{OtherKey32} ; 1:{Key32} ; ").Parse();

        Assert.Equal(2, keys.Count);
    }

    // --- S1: missing -----------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_value_refuses_to_start(string keys)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Options(keys).Parse());

        Assert.Contains("Chat:Encryption:Keys", ex.Message, StringComparison.Ordinal);
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_default_options_object_refuses_to_start()
    {
        // There is deliberately no "encryption disabled" default: an unconfigured application
        // must stop, not quietly store message text in the clear.
        Assert.Throws<InvalidOperationException>(() => new ChatEncryptionOptions().Parse());
    }

    // --- S2: wrong key size ----------------------------------------------------

    [Fact]
    public void A_key_that_is_not_thirty_two_bytes_is_rejected()
    {
        const string short16 = "AAECAwQFBgcICQoLDA0ODw==";

        AssertRejects($"1:{short16}", short16);
    }

    // --- S3: bad version -------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("256")]
    [InlineData("x")]
    [InlineData("-1")]
    public void A_version_outside_one_to_two_hundred_fifty_five_is_rejected(string version)
    {
        AssertRejects($"{version}:{Key32}", Key32);
    }

    // --- S4: duplicates --------------------------------------------------------

    [Fact]
    public void A_repeated_version_is_rejected()
    {
        // Two keys claiming one version makes it ambiguous which one wrote a row — the rows
        // would decrypt or not depending on dictionary iteration order.
        AssertRejects($"1:{Key32};1:{OtherKey32}", Key32, OtherKey32);
    }

    // --- S5: malformed ---------------------------------------------------------

    [Theory]
    [InlineData("not-a-pair")]
    [InlineData(":missingversion")]
    [InlineData("1:")]
    public void A_malformed_entry_is_rejected(string entry)
    {
        AssertRejects(entry);
    }

    [Fact]
    public void A_key_that_is_not_base64_is_rejected()
    {
        AssertRejects("1:this is not base64 !!", "this is not base64");
    }

    [Fact]
    public void The_position_of_the_bad_entry_is_reported_without_its_content()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Options($"1:{Key32};2:nonsense").Parse());

        Assert.Contains("entry 2", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Key32, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("nonsense", ex.Message, StringComparison.Ordinal);
    }
}
