# JuggerHub Constitution

This constitution is the single source of truth for architecture, security, and
engineering conventions in this repository. It consolidates the guidance that
previously lived in `instructions/`. Per [CLAUDE.md](../../CLAUDE.md), Spec-Kit
specs and this constitution outrank GitHub Issues, Graphify, claude-mem, and general
model knowledge — but never override the current user instruction, the source
code/tests, or an explicit spec.

**Environments.** Three environments exist — **local**, **Dev**, and **Prod** —
and must be kept as close to one another as possible. Differences are limited to
configuration and secrets, never to architecture or behavior.

---

## Core Principles

### I. Security-First, Never Trust the Client (NON-NEGOTIABLE)

- All code is written with the **OWASP Top 10** in mind.
- The **"never trust the client"** paradigm is absolute: every authorization and
  validation decision is enforced server-side. Client-side checks exist only for
  UX and are never the security boundary.
- Exceptions and stack traces are **never** forwarded to the frontend. The backend
  returns well-formed or generic error messages via a global exception-handling
  middleware. Secrets never reach the client.

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        await HandleExceptionAsync(context, ex);
    }
}

private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
{
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

    var response = new
    {
        type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        title = "An error occurred",
        status = 500,
        detail = "An unexpected error occurred. Please try again later."
    };

    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
}
```

### II. Thin Controllers, Service-Centric Backend

- **Thin controllers**: controllers perform only rudimentary HTTP validation and
  response shaping, then forward to the service layer.
- **Dependency injection** is used throughout; every service exposes an interface.
- **No repository layer** — services access the database directly via Entity
  Framework Core.
- **DTOs** are used for all client-facing responses to strip unnecessary data.
  Services return **entities**; the controller maps entities to the response DTO
  with **Mapster**.
- **Lean middleware**: register only middleware needed for core mechanics
  (routing, model binding, error handling) and security (authentication,
  authorization, CORS, rate limiting). Cross-cutting concerns belong in services
  or filters.

### III. Disciplined Data Access (EF Core + PostgreSQL)

- **`BaseEntity`**: all entities derive from a shared base exposing `Id`,
  `CreatedDate`, and `ModifiedDate`. The primary key is a `Guid` generated via
  `Guid.CreateVersion7()` (UUIDv7). UUIDv7 is timestamp-prefixed, so inserts
  append to the right edge of the Postgres B-tree like a sequential key —
  avoiding the page splits, fragmentation, and WAL amplification that random v4
  GUIDs cause — while remaining unguessable enough to expose safely in URLs.

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
```

- **Audit fields are automatic**: `CreatedDate`/`ModifiedDate` are populated by an
  EF Core `SaveChangesInterceptor`; services never set them manually for tracked
  saves.

```csharp
public sealed class AuditFieldsInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in eventData.Context!.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedDate = now;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.ModifiedDate = now;
        }
        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

- **Projections**: use `.Select(...)` / `.ProjectToType<T>()` when reading so only
  required columns are pulled, avoiding N+1 problems and reducing transfer.
- **Query tracking**: use `AsNoTracking()` for read-only queries; only track when
  entities will actually be modified and saved.
- **Batch operations**: prefer `AddRange`/`UpdateRange`/`RemoveRange` and
  `ExecuteUpdateAsync`/`ExecuteDeleteAsync` over per-row `SaveChangesAsync` loops.
  Because `ExecuteUpdateAsync`/`ExecuteDeleteAsync` bypass the change tracker, the
  `AuditFieldsInterceptor` does **not** run — set `ModifiedDate` explicitly:

```csharp
await db.Users
    .Where(u => u.IsActive)
    .ExecuteUpdateAsync(s => s
        .SetProperty(u => u.Status, Status.Archived)
        .SetProperty(u => u.ModifiedDate, DateTime.UtcNow), ct);
```

- **Pagination is mandatory**: any endpoint or service method returning a list
  must paginate via `Skip`/`Take`. Never return unbounded collections. Use a
  shared `PaginationRequest` (bound from the query string, with a hard maximum
  page size) and a `PagedResult<T>` envelope so the contract is uniform.

```csharp
public sealed record PaginationRequest
{
    private const int MaxTake = 100;
    private const int DefaultTake = 20;

    public int Skip { get; init; } = 0;
    public int Take { get; init; } = DefaultTake;

    public int NormalizedSkip => Skip < 0 ? 0 : Skip;
    public int NormalizedTake => Take is <= 0 or > MaxTake ? DefaultTake : Take;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Skip, int Take);
```

```csharp
[HttpGet]
public async Task<PagedResult<UserDto>> GetUsers([FromQuery] PaginationRequest pagination, CancellationToken ct) { ... }
```

  Skip/Take is the default for normal CRUD lists. For very large or
  rapidly-changing tables, prefer keyset pagination (`WHERE Id > lastId`).

### IV. Secure Authentication & Session Management

- **Microsoft Identity** handles authentication; user data is stored in the
  PostgreSQL database. The following flows are required: **register with
  email & password**, **login**, and **forgot password**.
- Passwords are hashed with **argon2** using a salt mechanism for resilience
  against rainbow-table and brute-force attacks.
- **JWT** is used for auth handling. Tokens are **never** stored in
  `localStorage` — only in secure, `httpOnly` cookies:

```ts
// Set httpOnly cookie
res.cookie("auth_token", token, {
    httpOnly: true, // Cannot be accessed by JavaScript
    secure: false, // IMPORTANT: sameSite='none' requires secure=true (HTTPS)
    // Browsers may reject this cookie with sameSite='none' + secure=false
    sameSite: "strict", // 'none' = vulnerable to CSRF (but requires secure=true)
    // 'lax' = protects against cross-site POST (but allows top-level GET)
    // 'strict' = maximum protection (blocks all cross-site requests)
    maxAge: 3600000, // 1 hour
    path: "/", // Ensure cookie is sent with all requests
});
```

- **Frontend session handling** (never the security boundary): use the token's
  expiration to log the user out when idle, and navigate to the login screen on
  logout. An `AuthGuard`/interceptor refreshes the session timer on successful
  responses and, on a `401`, attempts a refresh and otherwise redirects to login:

```ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();

  // Add Authorization header if token exists and include credentials for cookies
  let authReq = req.clone({
    setHeaders: token ? { 'Authorization': `Bearer ${token}` } : {},
    withCredentials: true // Always send cookies
  });

  // Handle the request and catch 401 errors
  return next(authReq).pipe(
    catchError((error) => {
      // If we get a 401 Unauthorized response, try to refresh the token
      if (error.status === 401) {
        const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/register') || req.url.includes('/auth/google');

        // Do not attempt refresh for auth endpoints (login/register/google)
        if (isAuthEndpoint) {
          return throwError(() => error);
        }

        // Only try refresh if this wasn't already a refresh request
        if (!req.url.includes('/refresh')) {
          console.log('Token expired, attempting refresh...');

          return authService.refreshToken().pipe(
            switchMap(() => {
              // Retry the original request with the new token
              const newToken = authService.getToken();
              const retryReq = req.clone({
                setHeaders: newToken ? { 'Authorization': `Bearer ${newToken}` } : {},
                withCredentials: true
              });
              return next(retryReq);
            }),
            catchError((refreshError) => {
              // Refresh failed, clear auth state and redirect to login
              console.log('Token refresh failed, redirecting to login...');
              authService.logout();
              // Return a friendly error for UI consumption
              const friendly = new HttpErrorResponse({
                status: 401,
                statusText: 'Unauthorized',
                url: req.url,
                error: { message: 'Your session has expired. Please sign in again.' }
              });
              return throwError(() => friendly);
            })
          );
        } else {
          // This was already a refresh request that failed, clear auth state and redirect
          console.log('Refresh request failed, redirecting to login...');
          authService.logout();
          const friendly = new HttpErrorResponse({
            status: 401,
            statusText: 'Unauthorized',
            url: req.url,
            error: { message: 'Your session has expired. Please sign in again.' }
          });
          return throwError(() => friendly);
        }
      }

      // Re-throw the error so components can still handle it if needed
      return throwError(() => error);
    })
  );
};
```

- **Password policy** is enforced from a single backend source. Expose the policy
  so the frontend can render and check the rules live during registration, only
  enabling the register call once every rule is satisfied:

```csharp
[HttpGet("password-policy")]
[AllowAnonymous]
public ActionResult<object> GetPasswordPolicy()
{
    var p = _identityOptions.Value.Password;
    return Ok(new
    {
        minLength = p.RequiredLength,
        requireDigit = p.RequireDigit,
        requireLowercase = p.RequireLowercase,
        requireUppercase = p.RequireUppercase,
        requireNonAlphanumeric = p.RequireNonAlphanumeric,
        requiredUniqueChars = p.RequiredUniqueChars
    });
}
```

```csharp
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // Password settings - More secure requirements
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 3;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
```

### V. Environment Parity & Reproducible, Containerized Deployments

- Every required service (e.g. backend, frontend) ships its **own Dockerfile**. A
  centralized **docker-compose** file brings the full stack up locally.
- **CI/CD** uses **GitHub Actions** with **Terraform**. Actions test and build the
  Docker images, push them to the **GitHub Container Registry (GHCR)**, and deploy
  to **Azure App Services**.
- **Terraform state** lives in a single Azure storage account (one container,
  one state file per environment). The resource group holding that storage
  account is managed **outside** Terraform. **Azure Key Vault is not used.**
- Backend↔frontend communication is primarily a **REST API**.

### VI. Consistent Conventions & Tooling

- **Frontend**: Angular with **Nx** and **Tailwind CSS**. Each module keeps
  **separate `.html`, `.css`, and `.ts` files** — never combine them into one file.
- **Scripts**: use **PowerShell (`.ps1`) only**. Do not add `.sh` scripts anywhere.

---

## Technology Stack

| Layer | Choice |
|-------|--------|
| Backend runtime | .NET 10 (monorepo), Entity Framework Core |
| Database | PostgreSQL 18 |
| Mapping | Mapster (entity → DTO) |
| Auth | Microsoft Identity, argon2 (salted), JWT in httpOnly cookies |
| Frontend | Angular + Nx + Tailwind CSS |
| Containerization | Docker (per-service) + docker-compose (local) |
| CI/CD | GitHub Actions, Terraform, GHCR → Azure App Services |
| Email | Mailpit (local), Resend (Dev/Prod) |
| Package managers | NuGet (backend), npm (frontend) |

---

## Secret & Configuration Management

- **Local**: a single `.env` file is referenced by docker-compose. Commit a
  sample (`.env.sample`); add the real `.env` to `.gitignore`. Local development
  therefore does **not** rely on `appsettings.json` for secrets — all environment
  variables come from `.env`.
- **Deployed**: secrets and configuration are stored in **GitHub Environments**
  and injected during the deployment pipeline; Terraform supplies deployed
  configuration. **No Azure Key Vault.**

---

## Transactional Email

- Send transactional emails to users. Use **Mailpit** on localhost and **Resend**
  on Dev and Prod.
- Define **base templates** for the email header, footer, and body, reused across
  all emails. Use-case-specific templates extend these base files with their
  specific content.
- All templates are **HTML files with inline CSS**.
- Reference implementation: [`backend/EmailTemplates`](../../backend/EmailTemplates)
  and [`backend/Services/EmailTemplateService`](../../backend/Services/EmailTemplateService).

---

## Dependency Management

- **Pin the major version**; minor and patch versions may update automatically.
- Package managers: **NuGet** (backend), **npm** (frontend).
- Use **Dependabot** with a custom config: **major-version** and **security**
  updates are raised as **individual PRs**; all other updates are **combined into
  a single PR per dependency**.

---

## Development Workflow & Quality Gates

These gates apply to all changes and integrate with the Spec-Kit workflow in
[CLAUDE.md](../../CLAUDE.md):

1. **Architecture**: controllers stay thin; logic lives in DI'd services behind
   interfaces; no repository layer; responses go out as DTOs mapped via Mapster.
2. **Data access**: list endpoints paginate; reads use projections + `AsNoTracking`;
   `ExecuteUpdateAsync` paths set `ModifiedDate`; new entities derive from
   `BaseEntity`.
3. **Security review**: changes are checked against the OWASP Top 10 and the
   never-trust-the-client rule; no raw exceptions or secrets leak to the client.
4. **Auth**: tokens stay in httpOnly cookies (never `localStorage`); password
   policy is sourced from the backend.
5. **Conventions**: frontend keeps `.html`/`.css`/`.ts` separate; only `.ps1`
   scripts are added.
6. **Environment parity**: changes work identically across local/Dev/Prod;
   secrets flow through `.env` (local) and GitHub Environments (deployed).

---

## Governance

- This constitution supersedes other engineering practices. Where practices
  conflict, the constitution wins — subordinate only to the source-of-truth order
  defined in [CLAUDE.md](../../CLAUDE.md) (current user instruction, then code and
  tests, then specs/constitution).
- **Amendments** must be documented in a PR, versioned below, and accompanied by
  any required migration notes. PRs and reviews must verify compliance; added
  complexity must be justified against these principles.
- Versioning follows semantic versioning: **MAJOR** for principle
  removals/redefinitions, **MINOR** for new principles/sections or materially
  expanded guidance, **PATCH** for clarifications and wording.

**Version**: 1.0.0 | **Ratified**: 2026-06-29 | **Last Amended**: 2026-06-29
