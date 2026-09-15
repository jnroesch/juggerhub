namespace JuggerHub.Entities;

/// <summary>
/// Where a tournament's recorded results came from (feature 050). A pasted Tugeny export is
/// <see cref="Manual"/>: the browser parses it, so "pasted" would only be the client's word; the
/// event admin vouches for it exactly as for typing. Only a server-fetched import is
/// <see cref="TugenyImport"/>. Serialized by name.
/// </summary>
public enum ResultSource
{
    /// <summary>Nothing recorded yet — the row may exist only to hold a Tugeny link.</summary>
    None = 0,

    /// <summary>Entered (or pasted) by an event admin.</summary>
    Manual = 1,

    /// <summary>Fetched from Tugeny's public data interface by the server.</summary>
    TugenyImport = 2,
}

/// <summary>The outcome of an imported match (feature 050). Serialized by name.</summary>
public enum MatchWinner
{
    First = 0,
    Second = 1,

    /// <summary>Tugeny reports no winner — every such match in its data has equal sets.</summary>
    Draw = 2,
}
