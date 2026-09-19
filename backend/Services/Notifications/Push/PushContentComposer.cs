using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Services.Notifications.Push;

/// <summary>
/// Turns a producer's notification payload into the four fields that reach a device (feature 055).
/// </summary>
public interface IPushContentComposer
{
    /// <summary>
    /// Composes the notification for one recipient. <paramref name="payloadJson"/> is the same
    /// camelCase JSON the in-app row stores, and <paramref name="culture"/> is the RECIPIENT's
    /// language — never the actor's, and never the request's.
    /// </summary>
    PushContent Compose(NotificationType type, string payloadJson, string culture, string tag);
}

/// <inheritdoc cref="IPushContentComposer" />
public sealed class PushContentComposer : IPushContentComposer
{
    private readonly IPushLocalizer _localizer;

    public PushContentComposer(IPushLocalizer localizer) => _localizer = localizer;

    /// <inheritdoc />
    public PushContent Compose(NotificationType type, string payloadJson, string culture, string tag)
    {
        JsonElement payload;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            payload = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Fallback(culture, tag);
        }

        var title = TitleFor(type, payload, culture);
        var body = BodyFor(type, payload, culture);
        var url = UrlFor(type, payload);

        return new PushContent(title, body, url, tag);
    }

    /// <summary>
    /// The heading names WHAT the notification is about — the team, the event, the training. Falls
    /// back to the product name rather than to an empty string, which some platforms render as a
    /// blank line.
    /// </summary>
    private string TitleFor(NotificationType type, JsonElement payload, string culture) => type switch
    {
        NotificationType.TeamInvite
            or NotificationType.TeamRoleChanged
            or NotificationType.TeamNews => Text(payload, "teamName"),

        NotificationType.PartyRequest
            or NotificationType.PartyNews
            or NotificationType.MarketInvite
            or NotificationType.EventCancelled => Text(payload, "eventName"),

        NotificationType.TrainingScheduled
            or NotificationType.TrainingUpdated => Text(payload, "trainingName"),

        _ => string.Empty,
    } is { Length: > 0 } named ? named : _localizer.Get("fallback.title", culture);

    private string BodyFor(NotificationType type, JsonElement payload, string culture) => type switch
    {
        NotificationType.TeamInvite => Sentence("teamInvite.body", culture, Text(payload, "inviterName")),
        NotificationType.TeamRoleChanged => _localizer.Get("teamRoleChanged.body", culture),
        NotificationType.TeamNews => _localizer.Get("teamNews.body", culture),
        NotificationType.PartyRequest => Sentence("partyRequest.body", culture, Text(payload, "teamName")),
        NotificationType.PartyNews => _localizer.Get("partyNews.body", culture),
        NotificationType.MarketInvite => Sentence("marketInvite.body", culture, Text(payload, "teamName")),
        NotificationType.TrainingScheduled => _localizer.Get("trainingScheduled.body", culture),
        NotificationType.TrainingUpdated => _localizer.Get("trainingUpdated.body", culture),
        NotificationType.EventCancelled => _localizer.Get("eventCancelled.body", culture),
        _ => _localizer.Get("fallback.body", culture),
    };

    /// <summary>
    /// Where the notification opens. <b>Always an app-relative path beginning with <c>/</c></b> —
    /// never absolute, so nothing here can send a member off-site, and never assembled from free
    /// text. Mirrors the in-app row's own link logic so the two agree about where a notification
    /// leads.
    /// </summary>
    private static string UrlFor(NotificationType type, JsonElement payload) => type switch
    {
        NotificationType.TeamInvite
            or NotificationType.TeamRoleChanged
            or NotificationType.TeamNews => Slug(payload, "teamSlug") is { } slug ? $"/t/{slug}" : "/",

        NotificationType.PartyRequest => Id(payload, "partyId") is { } party ? $"/parties/{party}" : "/",
        NotificationType.PartyNews => Id(payload, "partyId") is { } party ? $"/parties/{party}/news" : "/",

        NotificationType.MarketInvite
            or NotificationType.EventCancelled => Id(payload, "eventId") is { } ev ? $"/events/{ev}" : "/",

        NotificationType.TrainingScheduled or NotificationType.TrainingUpdated =>
            Id(payload, "sessionId") is { } session
                ? $"/trainings/sessions/{session}"
                : Slug(payload, "teamSlug") is { } teamSlug ? $"/t/{teamSlug}/trainings" : "/",

        _ => "/",
    };

    private string Sentence(string key, string culture, string argument) =>
        argument.Length > 0
            ? _localizer.Get(key, culture, argument)
            : _localizer.Get("fallback.body", culture);

    private PushContent Fallback(string culture, string tag) => new(
        _localizer.Get("fallback.title", culture),
        _localizer.Get("fallback.body", culture),
        "/",
        tag);

    private static string Text(JsonElement payload, string property) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>A GUID, or null. Parsed rather than interpolated, so nothing unexpected reaches a path.</summary>
    private static Guid? Id(JsonElement payload, string property) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && Guid.TryParse(value.GetString(), out var id)
            ? id
            : null;

    /// <summary>
    /// A team slug, or null. Restricted to the characters a slug can contain, so a path segment can
    /// never be smuggled in even if a payload were somehow malformed.
    /// </summary>
    private static string? Slug(JsonElement payload, string property)
    {
        var raw = Text(payload, property);
        if (raw.Length is 0 or > 64)
        {
            return null;
        }

        foreach (var c in raw)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-')
            {
                return null;
            }
        }

        return raw;
    }
}
