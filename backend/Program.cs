using System.Text;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Health;
using JuggerHub.Services.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

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

// --- Identity (foundation + auth pipeline; no auth endpoints/UI yet) --------
builder.Services
    .AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        // Password policy (constitution Principle IV). Enforced once auth flows
        // land; no endpoint exercises it in the walking skeleton.
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 3;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Replace Identity's default PBKDF2 hasher with argon2id (constitution IV).
builder.Services.AddSingleton<IPasswordHasher<User>, Argon2PasswordHasher>();

// --- Authentication: JWT carried in an httpOnly cookie ----------------------
// Bind JwtOptions from config so the validation parameters are resolved lazily
// (honours config layered in after composition, e.g. by the test host).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// AddIdentity (above) points the default authenticate/challenge schemes at the
// Identity cookie. Override them back to JwtBearer so a bare [Authorize] endpoint
// validates the JWT-in-cookie and challenges with 401 — never a cookie redirect.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtAccessor) =>
    {
        var jwt = jwtAccessor.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        bearer.Events = new JwtBearerEvents
        {
            // Read the token from the httpOnly cookie instead of the
            // Authorization header (constitution Principle IV).
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthCookieDefaults.AccessTokenCookie, out var token)
                    && !string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
            // Emit a generic ProblemDetails body on 401 (no internals leaked).
            OnChallenge = context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                {
                    return Task.CompletedTask;
                }

                return ProblemResponse.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    "Unauthorized",
                    "Authentication is required to access this resource.");
            },
        };
    });

builder.Services.AddAuthorization();

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

// --- OpenAPI document (rendered by Scalar in Development) -------------------
builder.Services.AddOpenApi("v1");

var app = builder.Build();

// --- Auto-apply EF migrations on startup (fail-fast) -----------------------
// Every environment (incl. Production) is brought up to schema before serving;
// a failure logs a generic error and exits non-zero rather than serving against
// a broken/half-migrated schema. See specs/001-project-scaffold/research.md §5.
await ApplyMigrationsAsync(app);

// --- Middleware pipeline ----------------------------------------------------
// Exception handler is registered first so it wraps the whole pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Interactive API reference (Scalar over the built-in OpenAPI document),
// Development-only so the schema/UI is never exposed in Prod.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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
