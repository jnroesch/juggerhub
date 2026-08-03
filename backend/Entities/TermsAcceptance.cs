namespace JuggerHub.Entities;

/// <summary>
/// Durable evidence that one account agreed to one version of the Terms of Use at one moment
/// (feature 041). Written in the same <c>SaveChanges</c> that creates the account, and never
/// updated or deleted by application code afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Modelled on <see cref="AdminActionRecord"/>, which solves the same problem: a record *about*
/// an account that must outlive every state that account can reach. <see cref="BaseEntity.CreatedDate"/>
/// is the acceptance moment — there is deliberately no separate <c>AcceptedAt</c> column, because a
/// second timestamp that could disagree with the first is a liability in an evidence row.
/// </para>
/// <para>
/// <b>⚠ This is NOT owned data. It must never be added to
/// <c>AccountDeletionService.EraseOwnedDataAsync</c>.</b> That method is a list of
/// <c>ExecuteDeleteAsync</c> calls over every table keyed by <c>UserId</c>, and this table is
/// keyed by <c>UserId</c> — so it reads like it belongs there. It does not. Erasure (feature 037)
/// does not delete the <see cref="User"/> row; it neutralises every identifying column on it. The
/// acceptance therefore survives pointing at a row that identifies nobody, which is exactly what
/// spec FR-024 requires: proof that an agreement was entered into, attached to no one. Deleting it
/// would destroy the consent evidence for precisely the accounts most likely to dispute something
/// later. <see cref="Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict"/> on the FK makes that
/// mistake fail loudly instead of succeeding quietly.
/// </para>
/// <para>
/// The relationship is <c>1:N</c>, not <c>1:1</c>, although exactly one row exists per account
/// today. Spec FR-021 requires that a future version change and re-acceptance flow need no
/// restructuring — a collection absorbs that, a single reference would need a migration.
/// </para>
/// </remarks>
public sealed class TermsAcceptance : BaseEntity
{
    /// <summary>The account that accepted (FK → AspNetUsers, Restrict).</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The document version agreed to. This is always the server's own
    /// <see cref="Common.TermsOptions.ResolvedVersion"/> — never the string the client submitted,
    /// which is validated and then discarded (research R1).
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// BCP-47 base tag of the translation the document was displayed in (<c>en</c>/<c>de</c>/
    /// <c>es</c>), validated server-side against <see cref="Common.SupportedLanguages"/>.
    /// Recorded because the German text is the authoritative one: knowing someone accepted while
    /// reading the Spanish translation is part of what the record has to be able to say.
    /// </summary>
    public string DisplayLanguage { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
