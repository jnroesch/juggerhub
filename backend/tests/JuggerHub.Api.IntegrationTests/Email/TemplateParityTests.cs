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
    /// <summary>
    /// The English templates that are deliberately not translated. Keep this short: anything named
    /// here is sent in English to everyone.
    /// </summary>
    private static readonly string[] NotTranslated =
    [
        // No words in them: the stylesheet and the logo.
        "base-styles.html",
        "header.html",
        // Never sent — nothing calls GenerateSubscriptionWelcomeEmailAsync.
        "subscription-welcome.html",
    ];

    /// <summary>
    /// Every English template except <see cref="NotTranslated"/>. The list is <b>opt-out</b> (GH #379):
    /// it used to be an opt-in list, and three English-only templates sat outside it with nothing
    /// failing, so a German or Spanish member got those emails in English. A new template now fails
    /// here until it has all three variants or is named above.
    /// </summary>
    public static TheoryData<string> FullyTranslatedTemplates
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in TemplateNames("en").Except(NotTranslated).Order(StringComparer.Ordinal))
            {
                data.Add(name);
            }

            return data;
        }
    }

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

    /// <summary>
    /// The shared footer carries the legal links, so it must be present per language too. It is in
    /// <see cref="FullyTranslatedTemplates"/> like any other template; this names it on purpose.
    /// </summary>
    [Fact]
    public void The_footer_is_checked()
    {
        Assert.Contains("footer.html", FullyTranslatedTemplates.Cast<object[]>().Select(row => (string)row[0]));
    }

    /// <summary>An allowlist entry for a file that is gone would hide the next file of that name.</summary>
    [Fact]
    public void Every_untranslated_template_still_exists()
    {
        foreach (var name in NotTranslated)
        {
            Assert.True(
                File.Exists(Path.Combine(TemplateRoot(), "en", name)),
                $"en/{name} is named as untranslated but no longer exists; drop it from the list.");
        }
    }

    /// <summary>
    /// A translation without an English original is never loaded: <c>EmailTemplateService</c> is
    /// called by the English name. This is what a rename that missed one language leaves behind.
    /// </summary>
    [Fact]
    public void Every_translation_has_an_english_original()
    {
        var english = TemplateNames("en").ToHashSet(StringComparer.Ordinal);

        foreach (var culture in new[] { "de", "es" })
        {
            var orphans = TemplateNames(culture).Where(name => !english.Contains(name)).Order(StringComparer.Ordinal).ToList();
            Assert.True(
                orphans.Count == 0,
                $"{culture}/ has template(s) with no English original: {string.Join(", ", orphans)}");
        }
    }

    private static IEnumerable<string> TemplateNames(string culture) =>
        Directory.GetFiles(Path.Combine(TemplateRoot(), culture), "*.html").Select(path => Path.GetFileName(path));

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
