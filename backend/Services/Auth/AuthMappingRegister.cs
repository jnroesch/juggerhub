using JuggerHub.Dtos.Auth;
using JuggerHub.Entities;
using Mapster;

namespace JuggerHub.Services.Auth;

/// <summary>
/// Mapster mapping for auth DTOs. Picked up automatically by the assembly scan in
/// <see cref="Common.MappingConfig"/> (constitution Principle II — entities → DTOs via Mapster).
/// </summary>
public sealed class AuthMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<User, AuthUserDto>()
            .Map(dest => dest.Email, src => src.Email ?? string.Empty);
    }
}
