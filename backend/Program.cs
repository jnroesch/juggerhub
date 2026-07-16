using System.Text;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services;
using JuggerHub.Services.Achievements;
using JuggerHub.Services.Auth;
using JuggerHub.Services.Badges;
using JuggerHub.Services.Email;
using JuggerHub.Services.Events;
using JuggerHub.Services.Health;
using JuggerHub.Services.Home;
using JuggerHub.Services.Notifications;
using JuggerHub.Services.Notifications.Realtime;
using JuggerHub.Services.Profile;
using JuggerHub.Services.Search;
using JuggerHub.Services.Security;
using JuggerHub.Services.Teams;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Security.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- MVC / controllers -----------------------------------------------------
// Serialize/accept enums (e.g. Pompfe) as their names, so the API contract and
// the Angular client speak "Stab"/"Schild" rather than opaque integers.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

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

        // Verify-before-login is enforced MANUALLY in AuthService.LoginAsync, AFTER a
        // correct password, so "unverified" is never revealed to someone who doesn't
        // know the password (enumeration protection — research §1). Leaving
        // RequireConfirmedEmail = false keeps Identity's pre-sign-in check from
        // short-circuiting on unverified accounts before the password is even checked.
        options.SignIn.RequireConfirmedEmail = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // Password-reset links expire faster than email-confirmation links.
        options.Tokens.PasswordResetTokenProvider = "ResetPasswordProvider";
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<ResetPasswordTokenProvider<User>>("ResetPasswordProvider");

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

// --- Platform admin gate (feature 013: PlatformAdmin Identity role, mirrored from config at startup) -----
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddScoped<PlatformAdminRoleSync>();
builder.Services.AddScoped<IAuthorizationHandler, PlatformAdminHandler>();
builder.Services.AddAuthorization(options =>
    options.AddPolicy(PlatformAdminPolicy.Name, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new PlatformAdminRequirement());
    }));
builder.Services.Configure<RecognitionOptions>(builder.Configuration.GetSection(RecognitionOptions.SectionName));

// --- Mapping (Mapster) -----------------------------------------------------
builder.Services.AddMappingConfig();

// --- Application services --------------------------------------------------
builder.Services.AddScoped<IHealthService, HealthService>();

// --- Email (transactional auth mail) ---------------------------------------
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<ResetPasswordTokenProviderOptions>(_ => { }); // ctor sets name + 1h lifespan
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>(); // existing service, now registered
builder.Services.AddScoped<AuthEmailService>();

// Pick the sender by configured provider: Mailpit (SMTP) locally, Resend on Dev/Prod.
var emailProvider = builder.Configuration.GetValue<string>("Email:Provider") ?? "Smtp";
if (string.Equals(emailProvider, "Resend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}

// --- Auth flows + session (refresh token) ----------------------------------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// --- Player profile + activity (feature 003) -------------------------------
builder.Services.Configure<ProfileOptions>(builder.Configuration.GetSection(ProfileOptions.SectionName));
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IEventActivityService, EventActivityService>();

// --- Teams + memberships + invitations (feature 005) -----------------------
builder.Services.Configure<TeamOptions>(builder.Configuration.GetSection(TeamOptions.SectionName));
builder.Services.AddScoped<TeamMembershipGuard>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<ITeamInvitationService, TeamInvitationService>();
builder.Services.AddScoped<ITeamActivityService, TeamActivityService>();
builder.Services.AddScoped<ITeamJoinRequestService, TeamJoinRequestService>(); // feature 009
builder.Services.AddScoped<ITeamNewsService, TeamNewsService>();
builder.Services.AddScoped<TeamEmailService>();

// --- Events (feature 006) --------------------------------------------------
builder.Services.Configure<EventOptions>(builder.Configuration.GetSection(EventOptions.SectionName));
builder.Services.AddScoped<EventAdminGuard>();
builder.Services.AddScoped<EventCapacity>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IEventSignupService, EventSignupService>();
builder.Services.AddScoped<IEventNewsService, EventNewsService>();
builder.Services.AddScoped<IEventContactService, EventContactService>();
builder.Services.AddScoped<IEventAdminService, EventAdminService>();
builder.Services.AddScoped<IEventInvitationService, EventInvitationService>();
builder.Services.AddScoped<EventEmailService>();

// --- Parties (feature 016) -------------------------------------------------
// Shared guard/capacity/email registered here; the four story services are registered as their
// implementations land per user story (see specs/016-event-parties/tasks.md).
builder.Services.AddScoped<JuggerHub.Services.Parties.PartyGuard>();
builder.Services.AddScoped<JuggerHub.Services.Parties.PartyCapacity>();
builder.Services.AddScoped<PartyEmailService>();
builder.Services.AddScoped<JuggerHub.Services.Parties.IPartyService, JuggerHub.Services.Parties.PartyService>();
builder.Services.AddScoped<JuggerHub.Services.Parties.IPartyRosterService, JuggerHub.Services.Parties.PartyRosterService>();
builder.Services.AddScoped<JuggerHub.Services.Parties.IPartyNewsService, JuggerHub.Services.Parties.PartyNewsService>();
builder.Services.AddScoped<JuggerHub.Services.Parties.IPartyInvitationService, JuggerHub.Services.Parties.PartyInvitationService>();

// --- Event marketplace (feature 017) ---------------------------------------
builder.Services.AddScoped<JuggerHub.Services.Marketplace.MarketEligibility>();
builder.Services.AddScoped<MarketEmailService>();
builder.Services.AddScoped<JuggerHub.Services.Marketplace.IMarketListingService, JuggerHub.Services.Marketplace.MarketListingService>();
builder.Services.AddScoped<JuggerHub.Services.Marketplace.IMarketRecruitingService, JuggerHub.Services.Marketplace.MarketRecruitingService>();
builder.Services.AddScoped<JuggerHub.Services.Marketplace.IMarketRequestService, JuggerHub.Services.Marketplace.MarketRequestService>();

// --- Trainings (feature 018) -----------------------------------------------
builder.Services.AddScoped<JuggerHub.Services.Trainings.TrainingGuard>();
builder.Services.AddScoped<JuggerHub.Services.Trainings.ITrainingSeriesService, JuggerHub.Services.Trainings.TrainingSeriesService>();
builder.Services.AddScoped<JuggerHub.Services.Trainings.ITrainingSessionService, JuggerHub.Services.Trainings.TrainingSessionService>();
builder.Services.AddScoped<JuggerHub.Services.Trainings.ITrainingResponseService, JuggerHub.Services.Trainings.TrainingResponseService>();

// --- Chat (feature 019) ----------------------------------------------------
// ChatGuard is the single home of the membership predicate; team/party chat membership is DERIVED
// from the roster on every request rather than mirrored into rows, so removal revokes access by
// construction (see specs/019-chat/research.md §4).
builder.Services.AddScoped<JuggerHub.Services.Chat.ChatGuard>();
builder.Services.AddScoped<JuggerHub.Services.Chat.IChatConversationService, JuggerHub.Services.Chat.ChatConversationService>();
builder.Services.AddScoped<JuggerHub.Services.Chat.IChatMessageService, JuggerHub.Services.Chat.ChatMessageService>();
// The realtime seam is a singleton over IHubContext, mirroring feature 010's registration.
builder.Services.AddSingleton<JuggerHub.Services.Chat.Realtime.IChatRealtime, JuggerHub.Services.Chat.Realtime.SignalRChatRealtime>();

// Rate limiting — new shared infrastructure, required because chat's DM reach is open (FR-049a).
// The counters live in Redis: in-memory partitions are per-pod, so on a multi-replica deployment they
// would silently multiply every limit by the replica count (specs/019-chat/research.md §11).
builder.Services.AddJuggerHubRateLimiting(builder.Configuration.GetConnectionString("Redis"));

// --- Search / browse (feature 007) -----------------------------------------
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.SectionName));
builder.Services.AddScoped<ITeamSearchService, TeamSearchService>();
builder.Services.AddScoped<IEventSearchService, EventSearchService>();
builder.Services.AddScoped<IPlayerSearchService, PlayerSearchService>();

// --- Home dashboard (feature 008) ------------------------------------------
builder.Services.Configure<HomeOptions>(builder.Configuration.GetSection(HomeOptions.SectionName));
builder.Services.AddScoped<IHomeService, HomeService>();

// --- Real-time (features 010 + 019) ----------------------------------------
// The deployment runs MORE THAN ONE REPLICA, so SignalR needs a backplane: without one, each pod
// only reaches the connections it holds, and Clients.Group(...) silently stops at the pod boundary.
// Two players on different pods would see a dead conversation — messages persist but never arrive.
// Feature 010 originally reasoned "single App Service instance ⇒ no backplane"; that premise is gone.
// See specs/019-chat/research.md §10.
//
// The backplane attaches to SignalR itself, not to a hub, so this one call fixes NotificationHub
// (010) and ChatHub (019) together.
//
// Redis is configured in EVERY environment, local included, so local development exercises the same
// fan-out path as production (constitution V: environments differ in configuration, never
// architecture). Outside Development a missing connection string is fatal rather than a silent
// in-process fallback — that fallback would look healthy and quietly drop half the messages.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
var signalR = builder.Services.AddSignalR();

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    signalR.AddStackExchangeRedis(redisConnection, options =>
    {
        // Namespaced so a shared Redis cannot collide with another app's channels.
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("juggerhub");
    });
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "ConnectionStrings:Redis is required outside Development: SignalR needs a backplane to fan out "
        + "across replicas, and the rate limiter needs shared counters. Without it, real-time delivery "
        + "silently fails for users on other pods and every rate limit is multiplied by the replica count.");
}

builder.Services.AddSingleton<INotificationRealtime, SignalRNotificationRealtime>();
builder.Services.AddScoped<INotificationService, NotificationService>();
// Per-user delivery preferences (feature 011) — consulted by the engine + producers before delivery.
builder.Services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();

// --- Platform admin area (feature 013) --------------------------------------
builder.Services.AddScoped<JuggerHub.Services.Admin.IAdminOverviewService, JuggerHub.Services.Admin.AdminOverviewService>();
builder.Services.AddScoped<JuggerHub.Services.Admin.IAdminUserService, JuggerHub.Services.Admin.AdminUserService>();
builder.Services.AddScoped<JuggerHub.Services.Admin.IAdminTeamService, JuggerHub.Services.Admin.AdminTeamService>();

// --- Badges & Achievements (feature 012) — two separate families -----------
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<JuggerHub.Services.Recognition.IRecognitionDisplayService, JuggerHub.Services.Recognition.RecognitionDisplayService>();

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

// Mirror the PlatformAdmin role to the configured admin identities (feature 013).
// Config is the source of truth: additions grant, removals revoke, unknown emails are
// picked up at a later startup. Never throws — authorization fails closed regardless.
using (var adminSyncScope = app.Services.CreateScope())
{
    await adminSyncScope.ServiceProvider.GetRequiredService<PlatformAdminRoleSync>().SyncAsync();
}

// Development-only sample data for demonstrable "recent activity" (never in Prod).
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DevDataSeeder.SeedAsync(seedDb);
}

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

// Rate limiting (feature 019) must come AFTER authentication: the chat policies partition on the
// authenticated user id, which does not exist yet earlier in the pipeline.
app.UseRateLimiter();

app.MapControllers();

// Real-time notifications hub (feature 010). Same-origin JWT cookie authenticates the handshake.
app.MapHub<NotificationHub>("/hubs/notifications");

// Real-time chat hub (feature 019). Same auth, same push-only per-user-group design; fan-out crosses
// replicas via the Redis backplane registered above.
app.MapHub<JuggerHub.Services.Chat.Realtime.ChatHub>("/hubs/chat");

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
