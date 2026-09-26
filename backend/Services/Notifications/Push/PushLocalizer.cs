using JuggerHub.Common;

namespace JuggerHub.Services.Notifications.Push;

/// <summary>
/// The short strings a push notification is built from (feature 055).
///
/// <para>
/// <b>These live in C#, not in the frontend catalogues, and that inverts the usual rule.</b> GH #141
/// established that server-assembled prose has no catalogue key to be missing, so the guards cannot
/// see it — and the usual remedy is to send parameters and let the client build the sentence, which
/// is what the in-app notification list does. That cannot work here: the sentence is rendered by
/// the operating system, on a device where the app is not running. So it is built on the server,
/// which means it must be localized on the server, from the <i>recipient's</i> stored language.
/// </para>
///
/// <para>
/// Backed by in-code per-culture dictionaries with an English fallback, the same shape as
/// <c>EmailLocalizer</c> and <c>NotificationPreferenceService</c>'s category copy. Placeholders are
/// <b>positional</b> for the reason EmailLocalizer records: word order differs across en/de/es, so
/// a sentence cannot be assembled from concatenated fragments.
/// </para>
/// </summary>
public interface IPushLocalizer
{
    /// <summary>Localized value for <paramref name="key"/>, English-fallback, with positional arguments applied.</summary>
    string Get(string key, string culture, params object[] args);
}

/// <inheritdoc cref="IPushLocalizer" />
public sealed class PushLocalizer : IPushLocalizer
{
    // key -> culture -> text.
    //
    // Note what these deliberately do NOT carry: a news post's body, a training's description, or
    // any other member-written content. The owner's decision was that a notification NAMES ITS
    // SUBJECT — who and what — not that it reproduces it. Content on a lock screen, and in a push
    // service's hands, is a separate decision belonging to #309.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            // {0} = inviter's name
            ["teamInvite.body"] = new Dictionary<string, string>
            {
                ["en"] = "{0} invited you to join",
                ["de"] = "{0} hat dich eingeladen beizutreten",
                ["es"] = "{0} te ha invitado a unirte",
            },
            ["teamRoleChanged.body"] = new Dictionary<string, string>
            {
                ["en"] = "Your role in this team changed",
                ["de"] = "Deine Rolle in diesem Team hat sich geändert",
                ["es"] = "Tu rol en este equipo ha cambiado",
            },
            ["teamNews.body"] = new Dictionary<string, string>
            {
                ["en"] = "There's a new post in your team",
                ["de"] = "Es gibt einen neuen Beitrag in deinem Team",
                ["es"] = "Hay una publicación nueva en tu equipo",
            },
            // {0} = team name
            ["partyRequest.body"] = new Dictionary<string, string>
            {
                ["en"] = "{0} is putting a party together",
                ["de"] = "{0} stellt gerade eine Party zusammen",
                ["es"] = "{0} está formando un grupo",
            },
            ["partyNews.body"] = new Dictionary<string, string>
            {
                ["en"] = "There's news in your party",
                ["de"] = "Es gibt Neuigkeiten in deiner Party",
                ["es"] = "Hay novedades en tu grupo",
            },
            // {0} = team name
            ["marketInvite.body"] = new Dictionary<string, string>
            {
                ["en"] = "{0} invited you to join their party",
                ["de"] = "{0} hat dich in die Party eingeladen",
                ["es"] = "{0} te ha invitado a su grupo",
            },
            ["trainingScheduled.body"] = new Dictionary<string, string>
            {
                ["en"] = "A new training was scheduled",
                ["de"] = "Ein neues Training wurde angesetzt",
                ["es"] = "Se ha programado un entrenamiento nuevo",
            },
            ["trainingUpdated.body"] = new Dictionary<string, string>
            {
                ["en"] = "A training you're going to has changed",
                ["de"] = "Ein Training, zu dem du kommst, hat sich geändert",
                ["es"] = "Un entrenamiento al que vas ha cambiado",
            },
            ["eventCancelled.body"] = new Dictionary<string, string>
            {
                ["en"] = "This event was cancelled",
                ["de"] = "Diese Veranstaltung wurde abgesagt",
                ["es"] = "Este evento se ha cancelado",
            },
            // Title and body used when a payload is missing the names the sentence needs. Rare, and
            // it still tells the member something happened rather than dropping the notification.
            ["fallback.title"] = new Dictionary<string, string>
            {
                ["en"] = "JuggerHub",
                ["de"] = "JuggerHub",
                ["es"] = "JuggerHub",
            },
            ["fallback.body"] = new Dictionary<string, string>
            {
                ["en"] = "You have a new notification",
                ["de"] = "Du hast eine neue Meldung",
                ["es"] = "Tienes una notificación nueva",
            },

            // --- Chat (feature 056 / GH #309) ------------------------------------------------
            //
            // These DO carry member-written content, unlike everything above. That is an owner
            // decision (spec 056, Clarifications): a notification that does not say what was said
            // does not save anyone the trip into the app, and chat is the one place in the product
            // where that trip is the whole cost. It is why the privacy policy describes what a
            // notification can contain — the push service is chosen by the recipient's browser and
            // is under no contract with us.
            //
            // The message text itself is never a value here; it is an argument. Nothing in this
            // file is ever logged, and neither is what fills these placeholders.

            // {0} = sender's display name, {1} = the message (already truncated)
            ["chat.groupBody"] = new Dictionary<string, string>
            {
                ["en"] = "{0}: {1}",
                ["de"] = "{0}: {1}",
                ["es"] = "{0}: {1}",
            },
            // Used when the text cannot be read back, so the notification still goes out naming who
            // wrote — the same posture the app takes when it shows a placeholder for one message
            // rather than failing the conversation (feature 047).
            ["chat.sentMessage"] = new Dictionary<string, string>
            {
                ["en"] = "sent you a message",
                ["de"] = "hat dir eine Nachricht geschickt",
                ["es"] = "te ha enviado un mensaje",
            },
            // {0} = sender's display name. The group/team form of the above.
            ["chat.sentMessageIn"] = new Dictionary<string, string>
            {
                ["en"] = "{0} wrote in the chat",
                ["de"] = "{0} hat im Chat geschrieben",
                ["es"] = "{0} ha escrito en el chat",
            },
            // A real message from a real sender that is a photo or a file and no words
            // (feature 049) — not an error, and it must not render as an empty preview.
            ["chat.sentAttachment"] = new Dictionary<string, string>
            {
                ["en"] = "sent an attachment",
                ["de"] = "hat einen Anhang geschickt",
                ["es"] = "ha enviado un archivo adjunto",
            },
            // {0} = sender's display name.
            ["chat.sentAttachmentIn"] = new Dictionary<string, string>
            {
                ["en"] = "{0} sent an attachment",
                ["de"] = "{0} hat einen Anhang geschickt",
                ["es"] = "{0} ha enviado un archivo adjunto",
            },

            // The generic conversation names, for ChatNameFallbacks. The inbox ships these in
            // English to everyone; a lock screen is composed per recipient, so it need not.
            // chat.name.party is the one that always fires: a live party conversation stores no
            // name and there is nothing to derive one from (feature 046 drift).
            ["chat.name.group"] = new Dictionary<string, string>
            {
                ["en"] = "Group",
                ["de"] = "Gruppe",
                ["es"] = "Grupo",
            },
            ["chat.name.team"] = new Dictionary<string, string>
            {
                ["en"] = "Team chat",
                ["de"] = "Team-Chat",
                ["es"] = "Chat del equipo",
            },
            ["chat.name.party"] = new Dictionary<string, string>
            {
                ["en"] = "Party chat",
                ["de"] = "Party-Chat",
                ["es"] = "Chat del grupo",
            },
            ["chat.name.other"] = new Dictionary<string, string>
            {
                ["en"] = "Chat",
                ["de"] = "Chat",
                ["es"] = "Chat",
            },
        };

    /// <inheritdoc />
    public string Get(string key, string culture, params object[] args)
    {
        // TryGetValue with an English fallback at BOTH levels, never a bare indexer. A bare indexer
        // here is what once threw KeyNotFoundException and took the notification settings page down
        // for an entire language (see NotificationPreferenceService, feature 039). A missing key is
        // a gap in copy; it must never be an outage.
        if (!Strings.TryGetValue(key, out var byCulture))
        {
            return key;
        }

        if (!byCulture.TryGetValue(culture, out var text)
            && !byCulture.TryGetValue(SupportedLanguages.Default, out text))
        {
            return key;
        }

        return args.Length == 0 ? text : string.Format(text, args);
    }
}
