# Data Model: Authentication & Account Access

Phase 1 output. This feature adds **one** persisted entity (`RefreshToken`),
exercises existing Identity columns on `User`, and defines the auth request/response
DTOs. No product-domain entities are introduced.

---

## Entities

### RefreshToken (NEW — `: BaseEntity`)

One row per issued refresh token. Rotation creates a new row in the same family and
revokes the old; reuse of a revoked/expired row invalidates the whole family.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` (uuid) | From `BaseEntity` (UUIDv7). PK. |
| `CreatedDate` / `ModifiedDate` | `DateTime` (timestamptz, UTC) | From `BaseEntity`; audit interceptor. On bulk revoke via `ExecuteUpdateAsync`, set `ModifiedDate` explicitly. |
| `UserId` | `Guid` | FK → `AspNetUsers.Id`. Indexed. Required. |
| `TokenHash` | `string` (Base64 SHA-256, fixed length) | **SHA-256 of the raw token** — the raw value is never stored. **Unique index.** Lookups hash the presented cookie value and match here. |
| `FamilyId` | `Guid` | Shared across one login's rotation chain. Indexed. Reuse-detection revokes by family. |
| `ReplacedByTokenId` | `Guid?` | Back-link set when this token is rotated (points at its successor). Null while current. |
| `ExpiresAt` | `DateTime` (timestamptz, UTC) | Absolute expiry. Persistent (remember-me) ≈ 14 days; session ≈ 1 day. |
| `IsPersistent` | `bool` | Remember-me choice; mirrors cookie persistence. |
| `RevokedAt` | `DateTime?` (timestamptz, UTC) | Set on rotation, logout, password reset, or reuse-detection. Null = active. |
| `CreatedByIp` / `RevokedReason` | `string?` | Optional audit aid (e.g. `rotated`, `logout`, `password-reset`, `reuse-detected`). No PII beyond IP; never tokens. |

**Active token** = `RevokedAt IS NULL AND ExpiresAt > now`.

**Indexes**: unique on `TokenHash`; non-unique on `UserId` and `FamilyId` (revocation
sweeps); consider `(UserId, RevokedAt)` for "revoke all active for user".

**State transitions**:

```
            issue (login)                rotate (refresh, valid)
   (none) ───────────────▶ ACTIVE ──────────────────────────────▶ REVOKED(rotated)
                             │                                          │ sets ReplacedByTokenId → new ACTIVE
                             │ logout / password-reset                  │
                             ├─────────────────────────────▶ REVOKED(logout|password-reset)
                             │ presented after expiry/revocation
                             └─────────────────────────────▶ REUSE DETECTED ⇒ revoke ENTIRE family ⇒ 401
```

---

### User (EXISTING — `: IdentityUser<Guid>`, unchanged shape)

No new columns are required; the feature now *exercises* native Identity fields:

| Field (Identity) | Use in this feature |
|------------------|---------------------|
| `Email` / `NormalizedEmail` / `UserName` | Registration sets email (= username); unique email enforced. |
| `EmailConfirmed` | Set `true` on verification; **gates sign-in** (`RequireConfirmedEmail=true` + explicit check, research §1). |
| `PasswordHash` | Argon2id (existing hasher). Never logged/returned. |
| `SecurityStamp` | Rotated by reset/confirm (invalidates outstanding email/reset tokens). |
| `AccessFailedCount` / `LockoutEnd` / `LockoutEnabled` | Lockout 5 attempts / 15 min (constitution), enforced at sign-in. |
| `TwoFactorEnabled` | **Retained for MFA-readiness**; unused this feature (always false). |

**Relationship**: `User 1───* RefreshToken` (cascade-delete refresh tokens with the
user). Identity schema (Roles/Claims/Logins/Tokens) is unchanged.

---

## Request / Response DTOs (transient — not persisted)

Requests are validated in the thin controller (data annotations + model state);
responses are deliberately **generic** for the enumeration-neutral flows.

| DTO | Shape | Notes |
|-----|-------|-------|
| `RegisterRequest` | `{ email, password }` | Email format; password validated server-side by Identity policy. |
| `LoginRequest` | `{ email, password, rememberMe }` | `rememberMe` drives cookie/refresh persistence. |
| `ForgotPasswordRequest` | `{ email }` | Always neutral response. |
| `ResetPasswordRequest` | `{ userId, token, newPassword }` | `token` is Base64Url Identity reset token; policy enforced. |
| `ResendVerificationRequest` | `{ email }` | Always neutral response. |
| `VerifyEmailRequest` | `{ userId, token }` | Base64Url Identity confirmation token. |
| `MessageResponse` | `{ message }` | Generic neutral message (register/forgot/resend/verify outcomes). |
| `AuthUserDto` | `{ id, email, emailConfirmed }` | Returned by `/auth/me` and on successful login (no tokens in body — tokens are cookies only). Mapster `User → AuthUserDto`. |
| `PasswordPolicyDto` | `{ minLength, requireDigit, requireLowercase, requireUppercase, requireNonAlphanumeric, requiredUniqueChars }` | Sourced from `IdentityOptions.Password`; rendered live by the frontend. |
| `LoginResponse` | `AuthUserDto` **or** `{ status: "PendingTwoFactor" }` (future) | Today always `AuthUserDto`; the union shape reserves the MFA branch (research §7) without a later breaking change. |

**No token material is ever placed in a response body** — access and refresh tokens
live only in httpOnly cookies (FR-012/FR-022/FR-024).

---

## Configuration shapes (bound options — not persisted)

| Options | Keys | Notes |
|---------|------|-------|
| `JwtOptions` (existing) | `Issuer`, `Audience`, `SigningKey`, `AccessTokenLifetimeMinutes` | Unchanged. |
| `EmailOptions` (NEW) | `Provider` (`Smtp`\|`Resend`), `FromAddress`, `SmtpHost`, `SmtpPort`, `Resend:ApiKey`, `FrontendBaseUrl` | Selects sender + builds SPA links. Local defaults target Mailpit. |
| `RefreshTokenOptions` (NEW, optional) | `SessionLifetime`, `PersistentLifetime` | Absolute caps (≈1 day / ≈14 days). May be constants if not exposed. |

---

## Persistence context changes

`AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>` gains:
- `DbSet<RefreshToken> RefreshTokens`.
- `OnModelCreating`: unique index on `TokenHash`; indexes on `UserId`, `FamilyId`;
  `UserId` FK with cascade delete; sensible max length on `TokenHash`/`RevokedReason`.
- **New migration `AddRefreshTokens`** (auto-applied on startup like all migrations).

No change to UUIDv7/timestamptz conventions; `RefreshToken` follows them via
`BaseEntity`.
