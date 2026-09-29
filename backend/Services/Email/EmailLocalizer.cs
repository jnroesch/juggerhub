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
    // key -> culture -> text. The de/es copy is a draft pending the native-speaker review (#84).
    // English is always present as the fallback.
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

            // --- Feature 058: join requests ------------------------------------------------------
            // The admins' email names the player — that is its point. The player's answer names the
            // team and never the admin who gave it (spec FR-011).

            // {0} = the player's display name, {1} = team name
            ["subject.joinRequest"] = new Dictionary<string, string>
            {
                ["en"] = "{0} wants to join {1} — JuggerHub",
                ["de"] = "{0} möchte {1} beitreten — JuggerHub",
                ["es"] = "{0} quiere unirse a {1} — JuggerHub",
            },
            // {0} = team name
            ["subject.joinRequestAccepted"] = new Dictionary<string, string>
            {
                ["en"] = "You're in: {0} — JuggerHub",
                ["de"] = "Du bist dabei: {0} — JuggerHub",
                ["es"] = "Ya formas parte de {0} — JuggerHub",
            },
            // {0} = team name
            ["subject.joinRequestDeclined"] = new Dictionary<string, string>
            {
                ["en"] = "Your request to join {0} — JuggerHub",
                ["de"] = "Deine Anfrage an {0} — JuggerHub",
                ["es"] = "Tu solicitud para unirte a {0} — JuggerHub",
            },

            ["title.joinRequest"] = new Dictionary<string, string>
            {
                ["en"] = "Someone wants to join your team",
                ["de"] = "Jemand möchte deinem Team beitreten",
                ["es"] = "Alguien quiere unirse a tu equipo",
            },
            ["title.joinRequestAccepted"] = new Dictionary<string, string>
            {
                ["en"] = "Your request to join was accepted",
                ["de"] = "Deine Beitrittsanfrage wurde angenommen",
                ["es"] = "Tu solicitud para unirte ha sido aceptada",
            },
            ["title.joinRequestDeclined"] = new Dictionary<string, string>
            {
                ["en"] = "Your request to join was declined",
                ["de"] = "Deine Beitrittsanfrage wurde abgelehnt",
                ["es"] = "Tu solicitud para unirte ha sido rechazada",
            },

            ["footer.joinRequest"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you're an admin of this team on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du Admin dieses Teams auf JuggerHub bist.",
                ["es"] = "Recibes este mensaje porque eres admin de este equipo en JuggerHub.",
            },
            ["footer.joinRequestAnswer"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you asked to join this team on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du auf JuggerHub angefragt hast, diesem Team beizutreten.",
                ["es"] = "Recibes este mensaje porque pediste unirte a este equipo en JuggerHub.",
            },

            // --- Feature 064: departures -------------------------------------------------------------
            // The removed player is told the team and nothing else; the admins are told the player.
            // Neither ever names the admin who removed someone (spec FR-011, FR-015).

            // {0} = team name
            ["subject.removedFromTeam"] = new Dictionary<string, string>
            {
                ["en"] = "You're no longer a member of {0} — JuggerHub",
                ["de"] = "Du bist kein Mitglied von {0} mehr — JuggerHub",
                ["es"] = "Ya no eres miembro de {0} — JuggerHub",
            },
            // {0} = the player's display name, {1} = team name
            ["subject.memberLeft"] = new Dictionary<string, string>
            {
                ["en"] = "{0} left {1} — JuggerHub",
                ["de"] = "{0} hat {1} verlassen — JuggerHub",
                ["es"] = "{0} ha dejado {1} — JuggerHub",
            },
            // {0} = the player's display name, {1} = team name
            ["subject.memberRemoved"] = new Dictionary<string, string>
            {
                ["en"] = "{0} was removed from {1} — JuggerHub",
                ["de"] = "{0} wurde aus {1} entfernt — JuggerHub",
                ["es"] = "Se ha retirado a {0} de {1} — JuggerHub",
            },

            ["title.removedFromTeam"] = new Dictionary<string, string>
            {
                ["en"] = "You're no longer a member of a team",
                ["de"] = "Du bist kein Mitglied eines Teams mehr",
                ["es"] = "Ya no eres miembro de un equipo",
            },
            ["title.memberLeft"] = new Dictionary<string, string>
            {
                ["en"] = "Someone left your team",
                ["de"] = "Jemand hat dein Team verlassen",
                ["es"] = "Alguien ha dejado tu equipo",
            },
            ["title.memberRemoved"] = new Dictionary<string, string>
            {
                ["en"] = "Someone was removed from your team",
                ["de"] = "Jemand wurde aus deinem Team entfernt",
                ["es"] = "Se ha retirado a alguien de tu equipo",
            },

            ["footer.removedFromTeam"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you were a member of this team on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du auf JuggerHub Mitglied dieses Teams warst.",
                ["es"] = "Recibes este mensaje porque eras miembro de este equipo en JuggerHub.",
            },

            // --- Feature 062: team polls -------------------------------------------------------------
            // The subject names the team and never the question: a mail app shows the subject on a
            // locked screen, and the owner kept poll questions off lock screens (spec FR-028's reason).
            // The question is in the body.

            // {0} = team name
            ["subject.teamPoll"] = new Dictionary<string, string>
            {
                ["en"] = "{0} started a poll — JuggerHub",
                ["de"] = "{0} hat eine Umfrage gestartet — JuggerHub",
                ["es"] = "{0} ha abierto una encuesta — JuggerHub",
            },
            ["title.teamPoll"] = new Dictionary<string, string>
            {
                ["en"] = "Your team is asking",
                ["de"] = "Dein Team fragt",
                ["es"] = "Tu equipo pregunta",
            },
            ["footer.teamPoll"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you're a member of this team on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du Mitglied dieses Teams auf JuggerHub bist.",
                ["es"] = "Recibes este mensaje porque eres miembro de este equipo en JuggerHub.",
            },

            // --- GH #379: the three invites, the role change and team news ----------------------
            // These five were English for every recipient until #379. The English values are the
            // strings the senders used to build inline, unchanged.

            // {0} = team name
            ["subject.teamInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You're invited to join {0} — JuggerHub",
                ["de"] = "Du bist zu {0} eingeladen — JuggerHub",
                ["es"] = "Te han invitado a unirte a {0} — JuggerHub",
            },
            // {0} = event name
            ["subject.eventAdminInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You're invited to co-administer {0} — JuggerHub",
                ["de"] = "Du bist eingeladen, {0} mitzuverwalten — JuggerHub",
                ["es"] = "Te han invitado a coadministrar {0} — JuggerHub",
            },
            // {0} = team name, {1} = event name
            ["subject.partyAdminInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You're invited to co-run {0}'s party at {1} — JuggerHub",
                ["de"] = "Du bist eingeladen, die Party von {0} bei {1} mitzuführen — JuggerHub",
                ["es"] = "Te han invitado a codirigir la party de {0} en {1} — JuggerHub",
            },
            // {0} = team name
            ["subject.teamRoleChanged"] = new Dictionary<string, string>
            {
                ["en"] = "Your role in {0} changed — JuggerHub",
                ["de"] = "Deine Rolle in {0} hat sich geändert — JuggerHub",
                ["es"] = "Tu rol en {0} ha cambiado — JuggerHub",
            },
            // {0} = team name
            ["subject.teamNews"] = new Dictionary<string, string>
            {
                ["en"] = "News from {0} — JuggerHub",
                ["de"] = "Neuigkeiten von {0} — JuggerHub",
                ["es"] = "Noticias de {0} — JuggerHub",
            },

            // {0} = inviter's name, {1} = team name
            ["title.teamInvite"] = new Dictionary<string, string>
            {
                ["en"] = "{0} invited you to join {1}",
                ["de"] = "{0} hat dich zu {1} eingeladen",
                ["es"] = "{0} te ha invitado a unirte a {1}",
            },
            // {0} = inviter's name, {1} = event name
            ["title.eventAdminInvite"] = new Dictionary<string, string>
            {
                ["en"] = "{0} invited you to help run {1}",
                ["de"] = "{0} hat dich eingeladen, {1} mitzuverwalten",
                ["es"] = "{0} te ha invitado a ayudar a organizar {1}",
            },
            // {0} = inviter's name
            ["title.partyAdminInvite"] = new Dictionary<string, string>
            {
                ["en"] = "{0} invited you to co-run a party",
                ["de"] = "{0} hat dich eingeladen, eine Party mitzuführen",
                ["es"] = "{0} te ha invitado a codirigir una party",
            },
            // {0} = team name
            ["title.teamRoleChanged"] = new Dictionary<string, string>
            {
                ["en"] = "Your role in {0} changed",
                ["de"] = "Deine Rolle in {0} hat sich geändert",
                ["es"] = "Tu rol en {0} ha cambiado",
            },
            // {0} = team name
            ["title.teamNews"] = new Dictionary<string, string>
            {
                ["en"] = "News from {0}",
                ["de"] = "Neuigkeiten von {0}",
                ["es"] = "Noticias de {0}",
            },

            // {0} = inviter's name
            ["footer.teamInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because {0} invited you to their team on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil {0} dich auf JuggerHub in ein Team eingeladen hat.",
                ["es"] = "Recibes este mensaje porque {0} te ha invitado a su equipo en JuggerHub.",
            },
            // {0} = inviter's name
            ["footer.eventAdminInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because {0} invited you to help run an event on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil {0} dich eingeladen hat, ein Event auf JuggerHub mitzuverwalten.",
                ["es"] = "Recibes este mensaje porque {0} te ha invitado a ayudar a organizar un evento en JuggerHub.",
            },
            // {0} = inviter's name
            ["footer.partyAdminInvite"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because {0} invited you to co-run a party on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil {0} dich eingeladen hat, eine Party auf JuggerHub mitzuführen.",
                ["es"] = "Recibes este mensaje porque {0} te ha invitado a codirigir una party en JuggerHub.",
            },
            ["footer.teamRoleChanged"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because your role on a JuggerHub team changed.",
                ["de"] = "Du erhältst diese E-Mail, weil sich deine Rolle in einem Team auf JuggerHub geändert hat.",
                ["es"] = "Recibes este mensaje porque tu rol en un equipo de JuggerHub ha cambiado.",
            },
            ["footer.teamNews"] = new Dictionary<string, string>
            {
                ["en"] = "You're getting this because you're a member of this team on JuggerHub.",
                ["de"] = "Du erhältst diese E-Mail, weil du Mitglied dieses Teams auf JuggerHub bist.",
                ["es"] = "Recibes este mensaje porque eres miembro de este equipo en JuggerHub.",
            },

            // The role-change email's two role words. The phrase completes "You're now … of {team}"
            // in each template, so it carries the article where the language needs one ("an admin")
            // and none where it doesn't ("Du bist jetzt Admin"). The label is the badge.
            ["role.phrase.admin"] = new Dictionary<string, string>
            {
                ["en"] = "an admin",
                ["de"] = "Admin",
                ["es"] = "admin",
            },
            ["role.phrase.member"] = new Dictionary<string, string>
            {
                ["en"] = "a member",
                ["de"] = "Mitglied",
                ["es"] = "miembro",
            },
            ["role.label.admin"] = new Dictionary<string, string>
            {
                ["en"] = "Admin",
                ["de"] = "Admin",
                ["es"] = "Admin",
            },
            ["role.label.member"] = new Dictionary<string, string>
            {
                ["en"] = "Member",
                ["de"] = "Mitglied",
                ["es"] = "Miembro",
            },

            // A date pattern, not prose. The app runs globalization-invariant, so a month-name
            // pattern always yields English month names: only English uses one. The others are
            // numeric, which reads natively in their language.
            ["format.date"] = new Dictionary<string, string>
            {
                ["en"] = "MMMM dd, yyyy",
                ["de"] = "dd.MM.yyyy",
                ["es"] = "dd/MM/yyyy",
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
