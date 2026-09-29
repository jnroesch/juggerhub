using System.Reflection;
using JuggerHub.Common;
using JuggerHub.Services.Email;

namespace JuggerHub.Api.IntegrationTests.Account;

/// <summary>
/// Pure unit checks for the language primitives (feature 031): base-language matching + allowlist
/// (FR-003), English fallback (FR-008/FR-018), and localized email subjects. No host/DB needed.
/// </summary>
public sealed class LocalizationUnitTests
{
    [Theory]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("es-MX", "es")]
    [InlineData("EN", "en")]
    [InlineData("fr", "en")]      // unsupported -> default
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void ResolveOrDefault_collapses_to_supported_base_or_english(string? input, string expected)
        => Assert.Equal(expected, SupportedLanguages.ResolveOrDefault(input));

    [Theory]
    [InlineData("de", true)]
    [InlineData("es", true)]
    [InlineData("en", true)]
    [InlineData("fr", false)]
    [InlineData(null, false)]
    public void IsSupported_matches_only_the_allowlist(string? input, bool expected)
        => Assert.Equal(expected, SupportedLanguages.IsSupported(input));

    [Fact]
    public void EmailLocalizer_returns_language_specific_subject()
    {
        var loc = new EmailLocalizer();
        Assert.Equal("Verify your email — JuggerHub", loc.Get("subject.verification", "en"));
        Assert.Equal("Bestätige deine E-Mail-Adresse — JuggerHub", loc.Get("subject.verification", "de"));
        Assert.Equal("Verifica tu correo electrónico — JuggerHub", loc.Get("subject.verification", "es"));
    }

    [Fact]
    public void EmailLocalizer_falls_back_to_english_for_unsupported_culture()
    {
        var loc = new EmailLocalizer();
        Assert.Equal(loc.Get("subject.welcome", "en"), loc.Get("subject.welcome", "fr"));
    }

    /// <summary>
    /// Every email string exists in every supported language (GH #379). A key missing one language
    /// falls back to English without a trace, which is how a German email gets an English subject.
    /// Reads the private table because the fallback makes the gap invisible through <c>Get</c>.
    /// </summary>
    [Fact]
    public void EmailLocalizer_has_every_key_in_every_supported_language()
    {
        var field = typeof(EmailLocalizer).GetField("Strings", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        var strings = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(field.GetValue(null));

        var gaps = strings
            .SelectMany(entry => SupportedLanguages.All
                .Where(language => !entry.Value.TryGetValue(language, out var text) || string.IsNullOrWhiteSpace(text))
                .Select(language => $"{entry.Key} [{language}]"))
            .ToList();

        Assert.True(gaps.Count == 0, $"Email strings missing a language: {string.Join(", ", gaps)}");
    }
}
