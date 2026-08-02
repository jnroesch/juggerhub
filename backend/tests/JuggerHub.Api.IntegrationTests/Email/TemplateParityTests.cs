using System.Text.RegularExpressions;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// The language variants of a template must carry the same placeholders (feature 039, FR-026a).
///
/// <c>EmailTemplateService.LoadTemplateAsync</c> falls back per <b>file</b>, not per placeholder. A
/// German template that omits <c>{{PARTY_URL}}</c> therefore renders a perfectly valid German email
/// with no call-to-action — no exception, no log line, nothing to notice until a recipient reports
/// that the button is missing. Set equality across en/de/es is what catches that at build time.
///
/// Reads the templates from source rather than the build output so a missing file is a failure
/// here rather than a silent English fallback at runtime.
/// </summary>
public sealed class TemplateParityTests
{
    /// <summary>The templates feature 039 authored in all three languages.</summary>
    public static TheoryData<string> FullyTranslatedTemplates =>
    [
        "event-cancelled.html",
        "party-request.html",
        "party-news.html",
        "market-invite.html",
    ];

    [Theory]
    [MemberData(nameof(FullyTranslatedTemplates))]
    public void Language_variants_declare_the_same_placeholders(string templateName)
    {
        var root = TemplateRoot();

        var byCulture = new[] { "en", "de", "es" }
            .ToDictionary(culture => culture, culture =>
            {
                var path = Path.Combine(root, culture, templateName);
                Assert.True(File.Exists(path), $"Missing template: {culture}/{templateName}");
                return Placeholders(File.ReadAllText(path));
            });

        var english = byCulture["en"];
        Assert.NotEmpty(english);

        foreach (var (culture, placeholders) in byCulture.Where(kv => kv.Key != "en"))
        {
            var missing = english.Except(placeholders).OrderBy(p => p).ToList();
            var extra = placeholders.Except(english).OrderBy(p => p).ToList();

            Assert.True(
                missing.Count == 0,
                $"{culture}/{templateName} is missing placeholder(s) present in English: {string.Join(", ", missing)}");
            Assert.True(
                extra.Count == 0,
                $"{culture}/{templateName} declares placeholder(s) English does not: {string.Join(", ", extra)}");
        }
    }

    [Theory]
    [MemberData(nameof(FullyTranslatedTemplates))]
    public void Every_language_variant_exists(string templateName)
    {
        var root = TemplateRoot();

        foreach (var culture in new[] { "en", "de", "es" })
        {
            Assert.True(
                File.Exists(Path.Combine(root, culture, templateName)),
                $"{culture}/{templateName} does not exist — it would silently fall back to English.");
        }
    }

    /// <summary>The shared footer carries the legal links, so it must be present per language too.</summary>
    [Fact]
    public void Footer_variants_declare_the_same_placeholders()
    {
        var root = TemplateRoot();
        var english = Placeholders(File.ReadAllText(Path.Combine(root, "en", "footer.html")));

        foreach (var culture in new[] { "de", "es" })
        {
            var localized = Placeholders(File.ReadAllText(Path.Combine(root, culture, "footer.html")));
            Assert.True(
                english.SetEquals(localized),
                $"{culture}/footer.html placeholders differ from English: "
                + $"missing [{string.Join(", ", english.Except(localized))}] "
                + $"extra [{string.Join(", ", localized.Except(english))}]");
        }
    }

    private static HashSet<string> Placeholders(string template) =>
        Regex.Matches(template, @"\{\{([A-Z0-9_]+)\}\}")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Walks up from the test assembly to the repo's <c>backend/EmailTemplates</c>.</summary>
    private static string TemplateRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "EmailTemplates");
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "en")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the EmailTemplates directory from the test output path.");
    }
}
