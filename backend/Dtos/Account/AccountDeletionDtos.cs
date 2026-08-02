using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Account;

/// <summary>
/// Why an account cannot be erased yet. The client renders the message; the server sends a
/// <b>key</b>, never prose, because this text ships in three languages (spec FR-008).
/// </summary>
public enum DeletionBlockerKind
{
    /// <summary>Sole admin of a team — the last-admin guard (feature 005) applies.</summary>
    SoleTeamAdmin = 0,

    /// <summary>Sole admin of an event.</summary>
    SoleEventAdmin = 1,

    /// <summary>Sole admin of an active party (its creator counts).</summary>
    SolePartyAdmin = 2,
}

/// <summary>What the member can do about a blocker. Also a key, for the same reason.</summary>
public enum DeletionBlockerRemedy
{
    /// <summary>Promote someone else, then come back.</summary>
    MakeAnotherAdmin = 0,

    /// <summary>Promote someone else, or disband/cancel the thing entirely.</summary>
    MakeAnotherAdminOrDisband = 1,
}

/// <summary>
/// One outstanding obligation. <see cref="SubjectName"/> names the <b>team, event, or party</b> —
/// never a person — so a refusal can say which without disclosing anyone.
/// </summary>
public sealed record DeletionBlockerDto(
    DeletionBlockerKind Kind,
    Guid SubjectId,
    string SubjectName,
    DeletionBlockerRemedy Remedy);

/// <summary>
/// The pre-confirmation answer to "what happens if I do this, and may I?" (spec US2).
/// Mutates nothing; everything here is re-checked at confirmation (FR-013).
/// </summary>
/// <remarks>
/// <see cref="Retained"/> and <see cref="Erased"/> are enum-ish keys rather than sentences. The
/// client owns the three-language catalogue, and the disclosure has to say things the server has no
/// business phrasing — in particular that messages and posts <em>stay</em> (FR-025) and that
/// identifying text a member typed themselves survives with them (FR-027).
/// </remarks>
public sealed record AccountDeletionPreviewDto(
    bool CanDelete,
    IReadOnlyList<DeletionBlockerDto> Blockers,
    IReadOnlyList<string> Retained,
    IReadOnlyList<string> Erased);

/// <summary>
/// The confirmation. Both fields are required and both are checked server-side (Principle I);
/// notably there is <b>no account identifier</b> — the subject is always the caller (FR-002).
/// </summary>
public sealed record DeleteAccountRequest(
    [Required] string Password,
    [Required] string Confirmation);
