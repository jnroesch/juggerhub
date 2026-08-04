namespace JuggerHub.Entities;

/// <summary>
/// A single dated occurrence of a <see cref="Training"/> that members (and, on a public session, guests)
/// respond to (feature 018). Display fields are inherited from the parent <see cref="Training"/> unless
/// overridden here: the <em>effective</em> value of a field is its <c>…Override ?? Training.Field</c>.
/// A single-session edit sets the relevant overrides and <see cref="Detached"/> = true so the session no
/// longer follows subsequent whole-series edits; a per-session public toggle sets
/// <see cref="VisibilityOverride"/> independently of detach.
/// </summary>
/// <remarks>
/// <see cref="TrainingSessionStatus.Skipped"/> is a quiet soft-tombstone (hidden from every list, no
/// responder notice) so a later pattern regeneration never resurrects the date;
/// <see cref="TrainingSessionStatus.Cancelled"/> stays visible marked-off and notifies responders. Past
/// sessions are read-only regardless of status.
/// </remarks>
public sealed class TrainingSession : BaseEntity
{
    public Guid TrainingId { get; set; }

    /// <summary>Denormalized from <see cref="Training.TeamId"/> for the tab list and the cross-team dashboard agenda.</summary>
    public Guid TeamId { get; set; }

    /// <summary>The occurrence date.</summary>
    public DateOnly SessionDate { get; set; }

    public TimeOnly? StartTimeOverride { get; set; }

    public TimeOnly? EndTimeOverride { get; set; }

    public LocationKind? LocationKindOverride { get; set; }

    /// <summary>
    /// ⚠ <b>System-derived legacy label</b> (feature 042), never assigned from a request.
    /// See <see cref="Training.Location"/>.
    /// </summary>
    public string? LocationOverride { get; set; }

    public string? VirtualLinkOverride { get; set; }

    // ---- Structured address override (feature 042) --------------------------------------------
    //
    // ⚠ READ THIS BEFORE TOUCHING THE FOUR COLUMNS BELOW.
    //
    // Every OTHER override on this entity resolves as `X ?? Training.X`. The address MUST NOT.
    // It resolves as ONE INDIVISIBLE BLOCK, keyed on CityIdOverride:
    //
    //     hasOwnAddress = CityIdOverride != null
    //     venue  = hasOwnAddress ? VenueNameOverride  : Training.VenueName
    //     street = hasOwnAddress ? StreetOverride     : Training.Street
    //     postal = hasOwnAddress ? PostalCodeOverride : Training.PostalCode
    //     city   = hasOwnAddress ? CityOverride       : Training.City
    //
    // Writing `VenueNameOverride ?? Training.VenueName` is a DEFECT: a session relocated to a venue
    // with no name, under a series that has one, would render the SERIES' venue name against the
    // SESSION's street; and a street-only override would render under the series' city.
    // Keying on the city is reliable because an in-person address is invalid without one.
    //
    // A session whose EFFECTIVE kind is Virtual carries no address: all four are nulled.

    public string? VenueNameOverride { get; set; }

    public string? StreetOverride { get; set; }

    public string? PostalCodeOverride { get; set; }

    /// <summary>The block marker: non-null exactly when this session carries its own address.</summary>
    public Guid? CityIdOverride { get; set; }

    public City? CityOverride { get; set; }

    /// <summary>Per-session visibility; null ⇒ inherit <see cref="Training.Visibility"/>.</summary>
    public TrainingVisibility? VisibilityOverride { get; set; }

    /// <summary>Set once a single-session edit runs; excludes the row from whole-series in-place edits and pattern regeneration.</summary>
    public bool Detached { get; set; }

    public TrainingSessionStatus Status { get; set; } = TrainingSessionStatus.Scheduled;

    /// <summary>Set (UTC) when the session is cancelled.</summary>
    public DateTime? CancelledDate { get; set; }

    public Training Training { get; set; } = null!;

    public ICollection<TrainingResponse> Responses { get; set; } = [];
}
