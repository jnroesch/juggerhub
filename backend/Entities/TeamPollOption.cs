namespace JuggerHub.Entities;

/// <summary>
/// One of a <see cref="TeamPoll"/>'s two to ten fixed answers (feature 062).
/// </summary>
/// <remarks>
/// Options are replaced as a set — deleted and re-inserted, with new ids — only while the poll has no
/// answers and only when their text or order actually changed. Otherwise they are left alone and keep
/// their ids, because deleting an option cascades to every vote cast for it.
/// </remarks>
public sealed class TeamPollOption : BaseEntity
{
    public Guid PollId { get; set; }

    /// <summary>The answer's text, trimmed, 1–80 characters, unique within its poll ignoring case.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>0-based position in the order the admin gave; unique per poll.</summary>
    public int Position { get; set; }

    public TeamPoll Poll { get; set; } = null!;
}
