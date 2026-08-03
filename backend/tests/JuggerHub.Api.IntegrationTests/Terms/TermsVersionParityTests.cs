using System.Text.Json;
using JuggerHub.Common;

namespace JuggerHub.Api.IntegrationTests.Terms;

/// <summary>
/// The published Terms of Use text and the version the server records must never disagree
/// (feature 041, guard G3).
///
/// The document lives in the frontend catalogues; the version written into every
/// <c>TermsAcceptance</c> comes from <see cref="TermsOptions"/>. Nothing at runtime connects the
/// two, so a text edit that forgets the constant produces acceptance records naming a version
/// whose text nobody ever saw — which is precisely the failure the whole feature exists to
/// prevent, and it is completely silent.
///
/// The frontend's own catalogue guard compares <b>keys</b> across languages, and values are
/// supposed to differ between translations. <c>terms.version</c> is the single leaf that must be
/// identical everywhere, so nothing else checks it.
/// </summary>
public sealed class TermsVersionParityTests
{
    private static readonly string[] Languages = ["en", "de", "es"];

    [Fact]
    public void Every_language_publishes_the_same_terms_version()
    {
        var versions = Languages.ToDictionary(lang => lang, ReadPublishedVersion);

        Assert.Single(versions.Values.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void Published_terms_version_matches_the_version_the_server_records()
    {
        // The built-in default, not a bound configuration: a deployment that forgets the section
        // still has to agree with the shipped text.
        var configured = new TermsOptions().ResolvedVersion;

        foreach (var language in Languages)
        {
            Assert.Equal(configured, ReadPublishedVersion(language));
        }
    }

    /// <summary>
    /// A version has to be a real date so the page can localise it, and so "which text did they
    /// agree to" is answerable by ordering rather than by memory.
    /// </summary>
    [Fact]
    public void Published_terms_version_is_an_iso_date()
    {
        foreach (var language in Languages)
        {
            Assert.True(DateOnly.TryParseExact(
                ReadPublishedVersion(language),
                "yyyy-MM-dd",
                out _));
        }
    }

    private static string ReadPublishedVersion(string language)
    {
        var path = Path.Combine(CatalogRoot(), $"{language}.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement
            .GetProperty("terms")
            .GetProperty("version")
            .GetString()
            ?? throw new InvalidOperationException($"{language}.json has no terms.version.");
    }

    /// <summary>
    /// Walks up from the test assembly to the frontend's legal catalogue directory — the same
    /// approach <c>Email/TemplateParityTests</c> uses for the email templates.
    /// </summary>
    /// <remarks>
    /// Throws rather than skipping when the directory is absent. A guard that quietly stops
    /// running is worse than no guard: it reports green while the property it protects rots.
    /// </remarks>
    private static string CatalogRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "frontend", "apps", "web", "public", "i18n", "legal");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "de.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate frontend/apps/web/public/i18n/legal from the test output path.");
    }
}
