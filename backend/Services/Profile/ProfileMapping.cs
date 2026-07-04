using JuggerHub.Dtos.Profile;
using JuggerHub.Entities;
using Mapster;

namespace JuggerHub.Services.Profile;

/// <summary>
/// Mapster config for profile DTOs. Picked up automatically by the assembly scan in
/// <see cref="Common.MappingConfig"/>. Profile/public DTOs are otherwise built with
/// explicit projected queries in <see cref="ProfileService"/> so sensitive columns
/// are never loaded (constitution Principle I — data-side never-trust-the-client).
/// </summary>
public sealed class ProfileMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // An event participation flattens to a recent-activity item (pulls the joined event).
        config.NewConfig<EventParticipation, ActivityItemDto>()
            .Map(dest => dest.EventName, src => src.Event.Name)
            .Map(dest => dest.Date, src => DateOnly.FromDateTime(src.Event.StartsAt))
            .Map(dest => dest.Location, src => src.Event.Location)
            .Map(dest => dest.TeamLabel, src => src.TeamLabel);
    }
}
