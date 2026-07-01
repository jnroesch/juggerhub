namespace JuggerHub.Entities;

/// <summary>
/// The canonical Jugger weapon catalog a player can list as a favorite, plus the
/// Läufer (Runner) — a position rather than a pompfe, but chosen in the same
/// selector (see specs/003-profile/research.md §5). Stored as its int value; the
/// fixed catalog makes an enum simpler and safer than a lookup table. Display
/// labels (DE/EN) live in the frontend catalog, not here.
/// </summary>
public enum Pompfe
{
    Stab = 0,
    Langpompfe = 1,
    Schild = 2,
    QTip = 3,
    Kette = 4,
    DoppelKurz = 5,
    Laeufer = 6,
}
