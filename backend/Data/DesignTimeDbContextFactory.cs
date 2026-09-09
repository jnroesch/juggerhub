using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace JuggerHub.Data;

/// <summary>
/// Builds an <see cref="AppDbContext"/> for the EF Core command-line tools
/// (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>) without starting the
/// application host.
/// </summary>
/// <remarks>
/// <para>
/// Added with feature 047. Chat message encryption refuses to start without a configured key, in
/// every environment and with no switch to disable it — which is correct at runtime and unhelpful
/// at design time, where scaffolding a migration has nothing to do with encryption keys. Without
/// this factory, adding any migration would require exporting an unrelated secret first.
/// </para>
/// <para>
/// Scoping this to the tools rather than relaxing the guard is deliberate. The alternative — a
/// "design time" escape hatch inside the guard — would be a code path that starts the application
/// without encryption, and something would eventually take it.
/// </para>
/// <para>
/// The connection string is read from the same configuration the application uses. It may be empty:
/// <c>migrations add</c> only needs the provider to shape the SQL, and no environment applies
/// migrations by hand — the application migrates itself at startup.
/// </para>
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
            .Options;

        return new AppDbContext(options);
    }
}
