# Data Model: Project Scaffold (Walking Skeleton)

Phase 1 output. This scaffold establishes the **identity foundation** and the
**shared data primitives** only — no product domains (Teams/Tournaments/Forum) and
no Club/Organization entities are created here. The hybrid tenancy model is
recorded as a convention (see end) rather than implemented.

---

## Entities

### BaseEntity (abstract base — all persisted records derive from this)

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` (PostgreSQL `uuid`) | Primary key. Defaults to `Guid.CreateVersion7()` (UUIDv7, timestamp-prefixed) generated app-side before insert. Immutable. |
| `CreatedDate` | `DateTime` (UTC, `timestamptz`) | Set automatically on insert by `AuditFieldsInterceptor`. Never set by hand for tracked saves. |
| `ModifiedDate` | `DateTime` (UTC, `timestamptz`) | Set automatically on insert and update by `AuditFieldsInterceptor`. For `ExecuteUpdateAsync` paths (interceptor bypassed) it MUST be set explicitly. |

**Notes**: This is the constitution's `BaseEntity`. Every future entity inherits
it. Stored as native `uuid`; UTC timestamps.

---

### User (identity foundation — Microsoft Identity)

`User : IdentityUser<Guid>`. Provides the Identity-managed account fields
(`UserName`, `Email`, `NormalizedEmail`, `PasswordHash`, `SecurityStamp`,
`LockoutEnd`, `AccessFailedCount`, …). For the scaffold we add **no** custom
profile fields — profile, display name, roles, and membership semantics arrive
with later features.

| Concern | Decision |
|---------|----------|
| Key type | `Guid` (UUIDv7), aligned with `BaseEntity` convention via `IdentityUser<Guid>`. |
| Password hashing | Custom argon2id `IPasswordHasher<User>` (see research §1). No password is set in this slice. |
| Roles | `IdentityRole<Guid>` table created by Identity, unused for now. |
| Relationship to `BaseEntity` | `IdentityUser<Guid>` already supplies `Id`; audit timestamps are not part of `IdentityUser`. Scaffold keeps `User` as the Identity type; future domain entities use `BaseEntity`. (If audit timestamps on `User` become needed, add them then.) |

**Validation / policy** (configured on Identity, enforced when auth flows land):
- Unique email required; password policy — min length 8, requires digit /
  lowercase / uppercase / non-alphanumeric, ≥3 unique chars; lockout 5 attempts /
  15 min (per constitution). No endpoint enforces these yet.

---

## Persistence context

**`AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>`**
- Registers the `AuditFieldsInterceptor`.
- Configures Npgsql provider; UUIDs as `uuid`, timestamps as `timestamptz` (UTC).
- Migrations live in `backend/Data/Migrations/`, auto-applied on startup
  (research §5).
- The **initial migration** creates the ASP.NET Core Identity schema (AspNetUsers,
  AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins,
  AspNetUserTokens, AspNetRoleClaims) keyed by `uuid`.

---

## Shared data shapes (DTOs / primitives — not persisted)

These are reusable contracts every future feature consumes. The record base and
the error envelope are exercised by the slice; the paging primitives
(`PaginationRequest`/`PagedResult<T>`) are provided now but **not yet exercised by
an endpoint** (no list ships in this slice).

### PaginationRequest (constitution primitive)

| Field | Type | Rules |
|-------|------|-------|
| `Skip` | `int` | Default `0`. Normalized: negative → `0`. |
| `Take` | `int` | Default `20`. Normalized: `<=0` or `> 100` (max) → default `20`. |

Bound from the query string. List-returning endpoints MUST accept this and page.

### PagedResult&lt;T&gt; (constitution primitive)

| Field | Type | Meaning |
|-------|------|---------|
| `Items` | `IReadOnlyList<T>` | The page of results. |
| `TotalCount` | `int` | Total matching rows (pre-paging). |
| `Skip` | `int` | Echoed applied skip. |
| `Take` | `int` | Echoed applied take. |

### HealthDto (slice read model — transient)

| Field | Type | Meaning |
|-------|------|---------|
| `status` | `string` | `"healthy"` \| `"degraded"` \| `"unhealthy"`. |
| `database` | `string` | `"reachable"` \| `"unreachable"`. |
| `version` | `string` | The API assembly's informational version (e.g. from `AssemblyInformationalVersionAttribute`), for diagnostics. |
| `timestamp` | `DateTime` (UTC) | When the check ran. |

### ProblemDetails (error envelope — transient)

RFC7231-shaped `{ type, title, status, detail }` with a **generic** `detail`. No
stack traces, internal messages, or secrets. Emitted by the global exception
middleware for unhandled errors and by the framework for `401`/`404` etc.

### WhoAmIDto (protected sample read model — transient)

| Field | Type | Meaning |
|-------|------|---------|
| `userId` | `Guid` | From the authenticated principal. |
| `authenticated` | `bool` | Always `true` when reached (endpoint is `[Authorize]`). |

Returned by the protected sample endpoint. Unauthenticated callers never see this
body — they receive `401` with a `ProblemDetails`.

---

## Relationships (current)

```
BaseEntity (abstract)
   └── (future domain entities derive from this)

IdentityDbContext
   └── User : IdentityUser<Guid>   ── Identity schema (Roles, Claims, Tokens, …)
```

No foreign keys between product domains exist yet (none are modeled).

---

## Recorded convention — hybrid tenancy (NOT implemented here)

For future features, scoping follows the agreed **hybrid** model:

- **Global** (no owning club): platform `User`s, the forum, and tournaments.
- **Optionally club-scoped**: teams may belong to a `Club`/`Organization`.
- **Implementation guidance for later specs**: club-scoped entities will carry a
  **nullable** `ClubId` (`null` = global/unaffiliated); queries that must be
  club-scoped filter on it explicitly. No global query filter is added now because
  no scoped entity exists yet. This bullet is the single source of the decision
  until a feature introduces `Club`.
