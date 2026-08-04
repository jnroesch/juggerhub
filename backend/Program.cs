using System.Text;
using Asp.Versioning;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Resilience;
using JuggerHub.Services;
using JuggerHub.Services.Achievements;
using JuggerHub.Services.Auth;
using JuggerHub.Services.Badges;
using JuggerHub.Services.Email;
using JuggerHub.Services.Events;
using JuggerHub.Services.Health;
using JuggerHub.Services.Home;
using JuggerHub.Services.Media;
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
    // Connection resiliency (feature 028; constitution VII). A rolling deploy or a database
    // restart is the most predictable transient fault we have, and without this every request in
    // flight during those seconds surfaces as a 500.
    //
    // This has a hard consequence: a retrying execution strategy REFUSES user-initiated
    // transactions. Every Database.BeginTransactionAsync call site must run through
    // Database.CreateExecutionStrategy().ExecuteAsync(...) as one retriable unit, with all state
    // mutation inside the delegate. Ten sites were restructured for this — see
    // specs/028-network-resilience/research.md §5 before adding another.
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
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

// --- Terms of Use (feature 041): the version recorded on every acceptance at registration ----
// Kept in parity with the legal catalogues by TermsVersionParityTests, not by discipline.
builder.Services.Configure<TermsOptions>(builder.Configuration.GetSection(TermsOptions.SectionName));

// --- Platform admin gate (feature 013: PlatformAdmin Identity role, mirrored from config at startup) -----
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddScoped<PlatformAdminRoleSync>();
builder.Services.AddScoped<IAuthorizationHandler, PlatformAdminHandler>();
builder.Services.AddAuthorization(options =>
{
    // Secure-by-default (feature 026): every endpoint requires an authenticated user
    // (JWT-in-cookie scheme) UNLESS it carries an explicit [AllowAnonymous]. This closes
    // the "forgot to authorize" class (OWASP A01) — new endpoints are private by default.
    // The intentionally-anonymous allowlist is: Auth flows, Health, RecognitionIcons
    // (icon bytes only), invite previews (Invitations/EventInvitations/PartyInvitations/
    // Market preview reads), and the visibility-gated public-profile reads
    // (ProfilesController {handle}, {handle}/avatar, {handle}/activity).
    options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(PlatformAdminPolicy.Name, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new PlatformAdminRequirement());
    });
});
builder.Services.Configure<RecognitionOptions>(builder.Configuration.GetSection(RecognitionOptions.SectionName));

// --- Application services --------------------------------------------------
builder.Services.AddScoped<IHealthService, HealthService>();

// --- Email (transactional auth mail) ---------------------------------------
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<ResetPasswordTokenProviderOptions>(_ => { }); // ctor sets name + 1h lifespan
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>(); // existing service, now registered
builder.Services.AddScoped<AuthEmailService>();
builder.Services.AddScoped<JuggerHub.Services.Email.AccountEmailService>();

// Localization (feature 031): resolves which language backend-generated content renders in and
// localizes the short code-authored email strings (subjects/titles/footers).
builder.Services.AddSingleton<JuggerHub.Services.Email.IEmailLocalizer, JuggerHub.Services.Email.EmailLocalizer>();
builder.Services.AddScoped<JuggerHub.Services.Localization.IRecipientCultureResolver, JuggerHub.Services.Localization.RecipientCultureResolver>();

// Pick the sender by configured provider: Mailpit (SMTP) locally, Resend on Dev/Prod.
var emailProvider = builder.Configuration.GetValue<string>("Email:Provider") ?? "Smtp";
if (string.Equals(emailProvider, "Resend", StringComparison.OrdinalIgnoreCase))
{
    // Feature 028 / constitution VII: the ONE opt-in. Timeout, jittered retry and circuit breaker
    // all arrive from the shared policy; this client carries no resilience logic of its own, and
    // the next outbound integration inherits the same behaviour with the same single line.
    builder.Services
        .AddHttpClient<IEmailSender, ResendEmailSender>()
        .AddJuggerHubResilience(builder.Configuration, "Resend");
}
else
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}

// --- Auth flows + session (refresh token) ----------------------------------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// --- Automated data retention (GH #106) ------------------------------------
// The platform's first scheduled deletion of application data. It exists because the privacy
// policy states how long each category is kept, and an unenforced period in a published legal
// document is worse than making no promise at all. Sweeps are registered individually and the
// hosted service runs whatever it finds; expired refresh tokens are the first category.
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddScoped<JuggerHub.Services.Retention.IRetentionSweep, JuggerHub.Services.Retention.ExpiredRefreshTokenSweep>();
builder.Services.AddHostedService<JuggerHub.Services.Retention.RetentionBackgroundService>();

// --- Account settings (feature 031: language preference) -------------------
builder.Services.AddScoped<JuggerHub.Services.Account.ILanguagePreferenceService, JuggerHub.Services.Account.LanguagePreferenceService>();
// Feature 037 — self-service account erasure.
builder.Services.AddScoped<JuggerHub.Services.Account.IAccountDeletionService, JuggerHub.Services.Account.AccountDeletionService>();
// Lets RecipientCultureResolver read the caller's Accept-Language for pre-account email language.
builder.Services.AddHttpContextAccessor();

// --- Server-side image processing (feature 034 / #98) ----------------------
// Reusable, owner-agnostic pipeline used by avatar uploads (and galleries #99 later).
// Stateless → singleton. Options bound with safe defaults (no config required).
builder.Services.Configure<ImageProcessingOptions>(builder.Configuration.GetSection(ImageProcessingOptions.SectionName));
builder.Services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();

// --- Media object storage (feature 035 / #97) ------------------------------
// Media bytes live in blob storage, not in Postgres; only a descriptor row stays behind. Azurite
// serves the same API locally and in tests, so one implementation covers every environment.
builder.Services.Configure<MediaStorageOptions>(builder.Configuration.GetSection(MediaStorageOptions.SectionName));

// Resilience for the store, and the one subtlety worth reading before changing anything here.
//
// The Azure SDK ships its own retry policy, but it has NO circuit breaker — and Principle VII
// requires a stop-condition wherever retry is used, so it cannot satisfy the gate on its own.
// Leaving it enabled alongside our pipeline would also stack two resilience strategies, turning
// 2 retries into 2x2 attempts exactly when a struggling dependency can least afford it. So the
// SDK's retry is disabled and the transport is routed through a named HttpClient carrying the
// shared feature-028 policy — one integration, one resilience section, same as every other.
builder.Services
    .AddHttpClient(MediaStorageOptions.ResilienceName)
    .AddJuggerHubResilience(builder.Configuration, MediaStorageOptions.ResilienceName);

builder.Services.AddSingleton<BlobServiceClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MediaStorageOptions>>().Value;
    foreach (var problem in options.Normalize())
    {
        sp.GetRequiredService<ILoggerFactory>()
            .CreateLogger("MediaStorage")
            .LogWarning("Media storage configuration was invalid and has been corrected: {Problem}", problem);
    }

    if (string.IsNullOrWhiteSpace(options.ConnectionString))
    {
        // Fail loudly rather than fall back. A silent default here would either point at nothing
        // (every picture 404s while the app looks healthy) or, worse, at another environment.
        throw new InvalidOperationException(
            "MediaStorage:ConnectionString is not configured. Local development uses the Azurite "
            + "defaults in .env.sample; deployed environments receive it from GitHub Environments.");
    }

    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(MediaStorageOptions.ResilienceName);

    // Pin the service API version rather than letting the SDK negotiate its newest.
    //
    // This is an environment-parity control, not a style choice. The SDK defaults to the latest
    // version it knows about, which the local/test emulator does not yet implement — so an
    // unpinned client works in Dev and Prod and fails everywhere a developer can actually see it.
    // Pinning means local, CI, Dev and Prod all speak ONE protocol version (Principle V), and a
    // package bump that would change it fails loudly in tests instead of drifting silently.
    //
    // The alternative — running the emulator with --skipApiVersionCheck — was rejected: it makes
    // the emulator accept headers it does not understand, which is precisely how local behaviour
    // starts diverging from real storage without anyone noticing. Raise this constant deliberately,
    // once the emulator supports the newer version.
    var clientOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05)
    {
        Transport = new HttpClientTransport(httpClient),
    };
    clientOptions.Retry.MaxRetries = 0; // See the note above — ours, not the SDK's.

    return new BlobServiceClient(options.ConnectionString, clientOptions);
});

builder.Services.AddSingleton<IMediaStore, AzureBlobMediaStore>();

// Orphan reclamation (FR-030). Scoped — it reads the descriptor tables. Operator-triggered via the
// admin endpoint rather than scheduled; see MediaReconciliationService for why.
builder.Services.AddScoped<MediaReconciliationService>();

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
// Resolves link cards against the VIEWER's permissions at read time, never the sender's, and never
// over the network (specs/019-chat/research.md §5).
builder.Services.AddScoped<JuggerHub.Services.Chat.ChatLinkResolver>();
builder.Services.AddScoped<JuggerHub.Services.Chat.IChatConversationService, JuggerHub.Services.Chat.ChatConversationService>();
builder.Services.AddScoped<JuggerHub.Services.Chat.IChatMessageService, JuggerHub.Services.Chat.ChatMessageService>();
builder.Services.AddScoped<JuggerHub.Services.Chat.IChatSearchService, JuggerHub.Services.Chat.ChatSearchService>();
builder.Services.AddScoped<JuggerHub.Services.Chat.IChatBlockService, JuggerHub.Services.Chat.ChatBlockService>();
// The realtime seam is a singleton over IHubContext, mirroring feature 010's registration.
builder.Services.AddSingleton<JuggerHub.Services.Chat.Realtime.IChatRealtime, JuggerHub.Services.Chat.Realtime.SignalRChatRealtime>();

// Rate limiting — new shared infrastructure, required because chat's DM reach is open (FR-049a).
// The counters live in Redis: in-memory partitions are per-pod, so on a multi-replica deployment they
// would silently multiply every limit by the replica count (specs/019-chat/research.md §11).
builder.Services.AddJuggerHubRateLimiting(builder.Configuration.GetConnectionString("Redis"));

// --- Structured locations (feature 030, research R8) -----------------------
// City search + selection resolve against a bundled, seeded GeoNames cities500 reference table —
// a local SQL query, NOT an external geocoder. No HTTP client, no resilience pipeline, no API key,
// nothing leaves the box (Principle I). The reference table is loaded once per environment by
// CityReferenceSeeder below.
builder.Services.Configure<GeocodingOptions>(builder.Configuration.GetSection(GeocodingOptions.SectionName));
builder.Services.AddScoped<JuggerHub.Services.Geocoding.ICityService, JuggerHub.Services.Geocoding.CityService>();

// --- Search / browse (feature 007) -----------------------------------------
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.SectionName));
builder.Services.AddScoped<ITeamSearchService, TeamSearchService>();
builder.Services.AddScoped<IEventSearchService, EventSearchService>();
builder.Services.AddScoped<IPlayerSearchService, PlayerSearchService>();
builder.Services.AddScoped<ITrainingSearchService, TrainingSearchService>(); // public trainings (043)

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
await EnsureMediaContainerAsync(app);

// Load the bundled GeoNames cities500 reference dataset (feature 030, R8) in EVERY environment —
// it is the city-picker's search source. Idempotent: a no-op once the table is populated. Gated by
// config so integration tests can seed a small fixture instead of ~235k rows.
if (builder.Configuration.GetValue("Seeding:CityReferences", true))
{
    using var cityScope = app.Services.CreateScope();
    var cityDb = cityScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var cityLog = cityScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.CitySeed");
    await CityReferenceSeeder.SeedAsync(cityDb, AppContext.BaseDirectory, cityLog);
}

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

// NOTE (feature 031): the app runs in globalization-invariant mode
// (<InvariantGlobalization>true</InvariantGlobalization>), where constructing real CultureInfo
// objects throws. So request-language handling deliberately does NOT use RequestLocalization /
// CultureInfo — the caller's effective language rides in on the Accept-Language header (stamped by
// the frontend) and is read as a plain string by RecipientCultureResolver, mapped onto the
// supported allowlist. Keeping invariant mode avoids pulling ICU into the Alpine image.

// Interactive API reference (Scalar over the built-in OpenAPI document),
// Development-only so the schema/UI is never exposed in Prod.
if (app.Environment.IsDevelopment())
{
    // Anonymous by intent: the doc endpoints must stay reachable without a session,
    // otherwise the global FallbackPolicy (feature 026) would 401 them in dev.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
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

// Creates the media container if it is absent (feature 035 / #97), so a fresh environment — or a
// developer's first `docker compose up` — works without a manual provisioning step.
//
// Deliberately creates a PRIVATE container: no public access level is ever passed. Terraform sets
// allow_nested_items_to_be_public = false at the account level as the backstop, so a mistake here
// still cannot open a deployed container — but this is the first line of that defence.
//
// Unlike the migration above, a failure here is logged rather than fatal. The store being briefly
// unreachable at startup must not stop the app booting: pages still render, and pictures degrade to
// their placeholder until it returns (FR-029).
static async Task EnsureMediaContainerAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup.MediaStorage");
    try
    {
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        if (store is AzureBlobMediaStore blobStore)
        {
            await blobStore.EnsureContainerAsync();
        }

        logger.LogInformation("Media storage container is ready.");
    }
    catch (Exception ex)
    {
        // Generic message only — never leak the connection string or the account key.
        logger.LogError(ex, "Could not prepare the media storage container; media will be unavailable until it is reachable.");
    }
}

/// <summary>
/// Exposed so the integration test project's <c>WebApplicationFactory</c> can
/// bootstrap the real app (minimal-hosting partial-Program pattern).
/// </summary>
public partial class Program
{
}
