using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Health;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- MVC / controllers -----------------------------------------------------
builder.Services.AddControllers();

// --- Data access (EF Core + Npgsql) ----------------------------------------
// Resolve the connection string lazily from IConfiguration (not a build-time
// local) so test hosts / overrides that layer config in after composition are
// honoured.
builder.Services.AddSingleton<AuditFieldsInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()
        .GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<AuditFieldsInterceptor>());
});

// --- Identity (foundation only; auth flows deferred to a later feature) -----
builder.Services
    .AddIdentity<User, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// --- Mapping (Mapster) -----------------------------------------------------
builder.Services.AddMappingConfig();

// --- Application services --------------------------------------------------
builder.Services.AddScoped<IHealthService, HealthService>();

// --- API versioning (URL segment: /api/v{n}) -------------------------------
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

var app = builder.Build();

// --- Auto-apply EF migrations on startup (fail-fast) -----------------------
// Every environment (incl. Production) is brought up to schema before serving;
// a failure logs a generic error and exits non-zero rather than serving against
// a broken/half-migrated schema. See specs/001-project-scaffold/research.md §5.
await ApplyMigrationsAsync(app);

// --- Middleware pipeline ----------------------------------------------------
// Exception handler is registered first so it wraps the whole pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup.Migrations");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database schema is up to date.");
    }
    catch (Exception ex)
    {
        // Generic message only — never leak connection strings or internals.
        logger.LogCritical(ex, "Database migration failed on startup; shutting down.");
        Environment.Exit(1);
    }
}

/// <summary>
/// Exposed so the integration test project's <c>WebApplicationFactory</c> can
/// bootstrap the real app (minimal-hosting partial-Program pattern).
/// </summary>
public partial class Program
{
}
