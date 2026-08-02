using System.Globalization;
using JuggerHub.Common;

namespace JuggerHub.Services.Email;

/// <summary>
/// Localizes the short, code-authored strings that surround transactional emails — subjects, and
/// the title/footer copy passed as template variables (feature 031). Long body prose is localized
/// via per-culture template files instead (see <see cref="EmailTemplateService"/>).
///
/// Backed by in-code per-culture dictionaries with an English fallback (FR-008). This is a
/// deliberate, low-risk alternative to <c>.resx</c>/<c>IStringLocalizer</c> for these few keys: it
/// needs no resource-generation tooling and is trivially unit-testable; the call sites can move to
/// <c>IStringLocalizer</c> later without changing signatures.
/// </summary>
public interface IEmailLocalizer
{
    /// <summary>Localized value for <paramref name="key"/> in <paramref name="culture"/>, English-fallback.</summary>
    string Get(string key, string culture);

    /// <summary>
    /// Localized value with positional arguments substituted (feature 039). Placeholders are
    /// positional (<c>{0}</c>, <c>{1}</c>) rather than concatenated fragments because word order
    /// differs across en/de/es — a subject naming a team and an event does not put them in the
    /// same order in every language.
    /// </summary>
    string Get(string key, string culture, params object[] args);
}

public sealed class EmailLocalizer : IEmailLocalizer
{
    // key -> culture -> text. Only the auth transactional set is fully translated for launch;
    // other emails keep English copy (and English bodies via en/ template fallback) pending the
    // native-review pass (#77). English is always present as the fallback.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["subject.verification"] = new Dictionary<string, string>
            {
                ["en"] = "Verify your email — JuggerHub",
                ["de"] = "Bestätige deine E-Mail-Adresse — JuggerHub",
                ["es"] = "Verifica tu correo electrónico — JuggerHub",
            },
            ["subject.passwordReset"] = new Dictionary<string, string>
            {
                ["en"] = "Reset your password — JuggerHub",
                ["de"] = "Setze dein Passwort zurück — JuggerHub",
                ["es"] = "Restablece tu contraseña — JuggerHub",
            },
            ["subject.passwordChanged"] = new Dictionary<string, string>
            {
                ["en"] = "Your password was changed — JuggerHub",
                ["de"] = "Dein Passwort wurde geändert — JuggerHub",
                ["es"] = "Tu contraseña ha cambiado — JuggerHub",
            },
            ["subject.welcome"] = new Dictionary<string, string>
            {
                ["en"] = "Welcome to JuggerHub",
                ["de"] = "Willkommen bei JuggerHub",
                ["es"] = "Te damos la bienvenida a JuggerHub",
            },
            // Feature 037. Deliberately plain: this is the last thing we send, and it must not read
            // as marketing or invite a reply that would go nowhere.
            ["subject.accountDeleted"] = new Dictionary<string, string>
            {
                ["en"] = "Your account has been deleted — JuggerHub",
                ["de"] = "Dein Konto wurde gelöscht — JuggerHub",
                ["es"] = "Tu cuenta ha sido eliminada — JuggerHub",
            },
            ["title.verification"] = new Dictionary<string, string>
            {
                ["en"] = "Confirm your email to finish signing up",
                ["de"] = "Bestätige deine E-Mail, um die Anmeldung abzuschließen",
                ["es"] = "Confirma tu correo para completar el registro",
            },
            ["title.passwordReset"] = new Dictionary<string, string>
            {
                ["en"] = "Reset your JuggerHub password",
                ["de"] = "Setze dein JuggerHub-Passwort zurück",
                ["es"] = "Restablece tu contraseña de JuggerHub",
            },
            ["title.passwordChanged"] = new Dictionary<string, string>
            {
                ["en"] = "Your JuggerHub password was changed",
                ["de"] = "Dein JuggerHub-Passwort wurde geändert",
                ["es"] = "Tu contraseña de JuggerHub ha cambiado",
            },
            ["title.accountDeleted"] = new Dictionary<string, string>
            {
                ["en"] = "Your JuggerHub account has been deleted",
                ["de"] = "Dein JuggerHub-Konto wurde gelöscht",
                ["es"] = "Tu cuenta de JuggerHub ha sido eliminada",
            },
            ["title.welcome"] = new Dictionary<string, string>
            {
                ["en"] = "Welcome to JuggerHub",
                ["de"] = "Willkommen bei JuggerHub",
                ["es"] = "Te damos la bienvenida a JuggerHub",
            },
            ["footer.verification"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because someone signed up for JuggerHub with this email address.",
                ["de"] = "Du erhältst diese E-Mail, weil sich jemand mit dieser Adresse bei JuggerHub angemeldet hat.",
                ["es"] = "Recibes este mensaje porque alguien se registró en JuggerHub con esta dirección de correo.",
            },
            ["footer.passwordReset"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because a password reset was requested for your account.",
                ["de"] = "Du erhältst diese E-Mail, weil für dein Konto ein Zurücksetzen des Passworts angefordert wurde.",
                ["es"] = "Recibes este mensaje porque se solicitó restablecer la contraseña de tu cuenta.",
            },
            ["footer.passwordChanged"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because your JuggerHub password was changed.",
                ["de"] = "Du erhältst diese E-Mail, weil dein JuggerHub-Passwort geändert wurde.",
                ["es"] = "Recibes este mensaje porque se cambió tu contraseña de JuggerHub.",
            },
            // "was deleted", not "you can manage notifications" — the shared footer's settings link
            // points at an account that no longer exists, so the reason line must not imply it works.
            ["footer.accountDeleted"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because your JuggerHub account was deleted.",
                ["de"] = "Du erhältst diese E-Mail, weil dein JuggerHub-Konto gelöscht wurde.",
                ["es"] = "Recibes este mensaje porque se eliminó tu cuenta de JuggerHub.",
            },
            ["footer.welcome"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you created a JuggerHub account.",
                ["de"] = "Du erhältst diese E-Mail, weil du ein JuggerHub-Konto erstellt hast.",
                ["es"] = "Recibes este mensaje porque creaste una cuenta en JuggerHub.",
            },

            // --- Feature 039: the four emails that used to be hand-rolled HTML ----------------
            // Subjects take positional args because word order differs by language — never build
            // these by concatenating fragments.

            // {0} = event name
            ["subject.eventCancelled"] = new Dictionary<string, string>
            {
                ["en"] = "{0} has been cancelled — JuggerHub",
                ["de"] = "{0} wurde abgesagt — JuggerHub",
                ["es"] = "{0} se ha cancelado — JuggerHub",
            },
            // {0} = event name, {1} = team name
            ["subject.partyRequest"] = new Dictionary<string, string>
            {
                ["en"] = "Fancy {0}? {1} is putting a party together — JuggerHub",
                ["de"] = "Lust auf {0}? {1} stellt eine Party auf — JuggerHub",
                ["es"] = "¿Te apuntas a {0}? {1} está formando una party — JuggerHub",
            },
            // {0} = team name, {1} = event name
            ["subject.partyNews"] = new Dictionary<string, string>
            {
                ["en"] = "{0} @ {1} — party update — JuggerHub",
                ["de"] = "{0} @ {1} — Party-Update — JuggerHub",
                ["es"] = "{0} @ {1} — novedades de la party — JuggerHub",
            },
            // {0} = team name, {1} = event name
            ["subject.marketInvite"] = new Dictionary<string, string>
            {
                ["en"] = "{0} wants you at {1} — JuggerHub",
                ["de"] = "{0} will dich bei {1} dabeihaben — JuggerHub",
                ["es"] = "{0} te quiere en {1} — JuggerHub",
            },

            ["title.eventCancelled"] = new Dictionary<string, string>
            {
                ["en"] = "An event you signed up for was cancelled",
                ["de"] = "Ein Event, für das du angemeldet warst, wurde abgesagt",
                ["es"] = "Se ha cancelado un evento al que te habías apuntado",
            },
            ["title.partyRequest"] = new Dictionary<string, string>
            {
                ["en"] = "Your team is putting a party together",
                ["de"] = "Dein Team stellt eine Party auf",
                ["es"] = "Tu equipo está formando una party",
            },
            ["title.partyNews"] = new Dictionary<string, string>
            {
                ["en"] = "A new update for your party",
                ["de"] = "Ein neues Update für deine Party",
                ["es"] = "Una novedad para tu party",
            },
            ["title.marketInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You've been invited to play",
                ["de"] = "Du wurdest zum Mitspielen eingeladen",
                ["es"] = "Te han invitado a jugar",
            },

            ["footer.eventCancelled"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you signed up for this event on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du dich auf JuggerHub für dieses Event angemeldet hast.",
                ["es"] = "Recibes este mensaje porque te apuntaste a este evento en JuggerHub.",
            },
            ["footer.partyRequest"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you're a member of this team on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du Mitglied dieses Teams auf JuggerHub bist.",
                ["es"] = "Recibes este mensaje porque eres miembro de este equipo en JuggerHub.",
            },
            ["footer.partyNews"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you're in this party on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du in dieser Party auf JuggerHub bist.",
                ["es"] = "Recibes este mensaje porque formas parte de esta party en JuggerHub.",
            },
            ["footer.marketInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because a party invited you via the JuggerHub marketplace.",
                ["de"] = "Du erhältst diese E-Mail, weil dich eine Party über den JuggerHub-Marktplatz eingeladen hat.",
                ["es"] = "Recibes este mensaje porque una party te ha invitado desde el mercado de JuggerHub.",
            },
        };

    public string Get(string key, string culture)
    {
        if (!Strings.TryGetValue(key, out var byCulture))
        {
            return key;
        }

        var normalized = SupportedLanguages.ResolveOrDefault(culture);
        return byCulture.TryGetValue(normalized, out var text)
            ? text
            : byCulture[SupportedLanguages.Default];
    }

    /// <inheritdoc />
    public string Get(string key, string culture, params object[] args)
    {
        var template = Get(key, culture);

        if (args.Length == 0)
        {
            return template;
        }

        try
        {
            // Invariant culture: the app runs globalization-invariant and every argument here is
            // already a string, so there is no culture-sensitive formatting to get wrong.
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            // A malformed placeholder in one language's copy must not break that language's mail.
            // Fall back to the unformatted template rather than throwing mid-send.
            return template;
        }
    }
}
