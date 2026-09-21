namespace JuggerHub.Services.Chat;

/// <summary>
/// The generic labels a conversation falls back to when it has no name of its own.
/// </summary>
/// <remarks>
/// <para>
/// They are English literals because that is what the inbox has always shipped — server-assembled
/// prose with no catalogue key and no parity guard behind it (GH #141). Passing them in rather than
/// hard-coding them lets a caller that <em>does</em> know the reader's language supply its own,
/// which is what feature 056 needs: a notification is rendered by the operating system, so it is
/// composed on the server in the recipient's language or not at all.
/// </para>
/// <para>
/// <b><see cref="Party"/> is the one that always fires.</b> A live party conversation stores no name
/// and <see cref="ChatDisplayName.For"/> has no party-name input, so every party chat in the product
/// is called exactly this. Feature 046 recorded it as minor drift; naming a party chat after its
/// party is still an open gap and not this type's business.
/// </para>
/// </remarks>
/// <param name="Group">A manual group whose name is somehow missing. In practice unreachable — a group's name is required.</param>
/// <param name="Team">A team chat with no team name. In practice unreachable while the chat is live.</param>
/// <param name="Party">A live party chat. Always used, see the remarks.</param>
/// <param name="Other">Any kind not otherwise handled.</param>
internal readonly record struct ChatNameFallbacks(
    string Group = "Group",
    string Team = "Team chat",
    string Party = "Party chat",
    string Other = "Chat")
{
    /// <summary>What the inbox has always used. Kept as the default so callers can ignore this type.</summary>
    public static readonly ChatNameFallbacks English = new();
}

/// <summary>
/// What a conversation is called (feature 019, extracted in feature 056).
/// </summary>
/// <remarks>
/// <para>
/// <b>Call this, never copy it.</b> It moved out of <c>ChatConversationService</c> when chat push
/// arrived, because a notification and the inbox naming the same conversation differently is the
/// kind of thing nobody notices until a member asks why their phone said something their app does
/// not. Keeping it in one place makes them agree structurally rather than by convention — the same
/// reason feature 043 made <c>LocationLabelFor</c> shared rather than duplicated.
/// </para>
/// <para>
/// It is pure and static: everything that varies by request — the reader's language, their
/// placeholder, whether they are the requester of an inquiry — arrives as an argument.
/// </para>
/// </remarks>
internal static class ChatDisplayName
{
    /// <summary>
    /// A conversation's display name. Only a group stores one; the rest derive it — except an archived
    /// auto chat, which froze its name at archival because the link it derived from is gone (019 R3a).
    /// </summary>
    /// <remarks>
    /// Inquiry threads (feature 027) name themselves <b>per viewer</b>: the requester sees the team
    /// name / event title (what they're asking about); an admin sees the requester's name <em>and</em>
    /// the team/event it concerns — e.g. "Ada K. · Rheinfeuer" — because an admin of several teams/events
    /// needs the context to tell inquiries apart. A frozen <paramref name="stored"/> name (set at
    /// archival, when the link is severed) wins for every derived kind.
    /// <para>
    /// The <paramref name="placeholder"/> argument is the caller's localized
    /// <see cref="Common.MemberPlaceholder"/> (feature 037). It is passed in rather than read from a
    /// constant so the value can vary by request culture while this helper stays static and pure.
    /// <paramref name="fallbacks"/> exists for the same reason and defaults to the English set the
    /// inbox has always used.
    /// </para>
    /// </remarks>
    public static string For(
        Entities.ConversationKind kind,
        string? stored,
        string? teamName,
        string? otherName,
        string? eventName,
        string? requesterName,
        bool isRequester,
        string placeholder,
        ChatNameFallbacks? fallbacks = null)
    {
        var names = fallbacks ?? ChatNameFallbacks.English;

        return kind switch
        {
            Entities.ConversationKind.Group => stored ?? names.Group,
            Entities.ConversationKind.Direct => otherName ?? placeholder,
            Entities.ConversationKind.Team => stored ?? teamName ?? names.Team,
            Entities.ConversationKind.Party => stored ?? names.Party,
            Entities.ConversationKind.TeamInquiry => stored ?? (isRequester ? teamName : InquiryAdminLabel(requesterName, teamName, placeholder)) ?? placeholder,
            Entities.ConversationKind.EventInquiry => stored ?? (isRequester ? eventName : InquiryAdminLabel(requesterName, eventName, placeholder)) ?? placeholder,
            _ => stored ?? names.Other,
        };
    }

    /// <summary>
    /// The admin-side label for an inquiry row: the requester's name plus the team/event it concerns,
    /// so an admin who manages several can tell them apart (feature 027). Degrades gracefully when
    /// either part is missing.
    /// </summary>
    private static string InquiryAdminLabel(string? requesterName, string? context, string placeholder) =>
        (requesterName, context) switch
        {
            ({ } r, { } c) => $"{r} · {c}",
            ({ } r, null) => r,
            (null, { } c) => c,
            _ => placeholder,
        };
}
