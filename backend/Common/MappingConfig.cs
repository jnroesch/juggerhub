using System.Reflection;
using Mapster;

namespace JuggerHub.Common;

/// <summary>
/// Central Mapster registration (constitution Principle II — controllers map
/// entities to DTOs with Mapster).
/// </summary>
/// <remarks>
/// Scans this assembly for <see cref="IRegister"/> implementations so future
/// features can declare mappings next to their feature code. No mappings exist
/// yet in the walking skeleton — the trivial health read model is constructed
/// directly — but the pipeline is wired so later DTOs inherit it.
/// </remarks>
public static class MappingConfig
{
    public static IServiceCollection AddMappingConfig(this IServiceCollection services)
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(Assembly.GetExecutingAssembly());
        services.AddSingleton(config);
        return services;
    }
}
