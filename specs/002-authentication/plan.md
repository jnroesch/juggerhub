# Implementation Plan: Authentication & Account Access

**Branch**: `002-authentication` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-authentication/spec.md`

## Summary

Turn the scaffold's authentication *pipeline* into real, user-facing flows: self-service registration, email verification (required before sign-in), sign-in/sign-out, forgotten-password reset, an enforced+published password policy, and a rotating-refresh session model — backend, frontend, and local infrastructure — so a developer running `docker compose up` can register, verify via the Mailpit inbox, sign in, sign out, and complete a password reset, all enforced server-side.

The approach extends what 001 already built rather than re-scaffolding it. The backend adds a thin `AuthController` over a new DI'd `IAuthService` that drives ASP.NET Core Identity (`SignInManager`/`UserManager`) with the existing argon2id hasher; access stays a 15-minute JWT in the `jh_access` httpOnly cookie, joined by a new **rotating, single-use refresh token** (persisted as a `RefreshToken : BaseEntity`, stored only as a SHA-256 hash, with family-based reuse detection) carried in a path-scoped `jh_refresh` httpOnly cookie. Email verification and password reset use Identity's data-protection token providers (reset on a shortened 1-hour lifespan); a new `IEmailSender` abstraction (MailKit SMTP → Mailpit locally, Resend HTTP for Dev/Prod, selected by `Email:Provider`) finally *sends* the existing HTML templates through the now-DI-registered `EmailTemplateService`. Registration, forgot-password, and resend-verification are enumeration-neutral. Sign-in routes through `SignInManager.CheckPasswordSignInAsync` + an explicit `EmailConfirmed` gate so "verify your email" is revealed only to a caller who already supplied the correct password — and so a future MFA branch slots in after the password check without reshaping the flow. The frontend makes the stub `AuthService` real (signals + API calls), upgrades the interceptor to single-flight refresh-and-retry on 401, and adds register / sign-in (with remember-me) / forgot-password / reset-password / verify-email screens styled from `DESIGN.md` and validated desktop + mobile. MFA, OAuth/SSO, and HIBP breach-screening (GitHub issue #11) are explicitly out of scope.

## Technical Context

**Language/Version**: Backend — C# 13 on .NET 10 (ASP.NET Core, EF Core 10, ASP.NET Core Identity). Frontend — TypeScript on Angular (standalone components) in an Nx workspace.

**Primary Dependencies**:
- Backend (existing): `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Konscious.Security.Cryptography.Argon2`, `Mapster`, `Asp.Versioning.Mvc`.
- Backend (new): **`MailKit`** (SMTP client for Mailpit; `System.Net.Mail.SmtpClient` is discouraged for new code) and a typed `HttpClient` for the **Resend** REST API (no extra SDK dependency). No other new packages — refresh tokens, token providers, and lockout are native Identity/EF.
- Frontend: `@angular/*` (router, forms — reactive), RxJS, Tailwind; `jest` (unit), `@playwright/test` (e2e, desktop + mobile). No new runtime dependency.

**Storage**: PostgreSQL 18. One new table `RefreshTokens` (+ EF migration); Identity schema already exists. `User.EmailConfirmed`/`LockoutEnd`/`AccessFailedCount`/`TwoFactorEnabled` are existing Identity columns now actually exercised.

**Testing**: Backend — xUnit + `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (extend the existing integration project) with a test `IEmailSender` capturing outbound mail. Frontend — Jest unit (AuthService, interceptor) + Playwright e2e (full register→verify→login→logout→reset journey) reading verification/reset links from the **Mailpit API** (`:8025/api/v1/...`). All tests run in containers (no host runtimes).

**Target Platform**: Linux containers via Docker, orchestrated by `docker-compose`. Product UI targets desktop + mobile (responsive web).

**Project Type**: Web application — existing sibling `backend/` (.NET) and `frontend/` (Nx/Angular) trees.

**Performance Goals**: No throughput targets. Sign-in/verify/reset should feel instant under local load. Argon2id work factor stays at the existing OWASP-aligned defaults; refresh rotation adds one indexed lookup + one update per renewal.

**Constraints**: Security-first / OWASP / never-trust-the-client; all auth/authorization enforced server-side; JWT + refresh only in httpOnly cookies (never script-accessible storage); no stack traces/secrets/token material to the client or logs; enumeration-neutral register/forgot/resend; environment parity (local/Dev/Prod differ only by config+secrets); all config/secrets from `.env` (local) / env vars; `.ps1`-only scripts; Docker-only workflow (no `ng serve`, no host runtime); responsive UI validated at multiple viewports; **MFA-readiness without MFA** (flow + token issuance must accommodate a later second-factor step; `TwoFactorEnabled` retained).

**Scale/Scope**: One auth surface (~10 endpoints), one new entity, ~5 new frontend screens + real AuthService/interceptor, one email-sender abstraction with two providers. Sized so later features consume the session/identity without reshaping it.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Verdict |
|---|-----------|------------------------|---------|
| I | Security-First, Never Trust the Client | Every decision (verify-gate, lockout, rotation/reuse-detection, password policy) enforced server-side; client checks are UX only. Enumeration-neutral register/forgot/resend. Generic `ProblemDetails` via the existing middleware/`ProblemResponse`; no stack traces, secrets, passwords, or token material in responses or logs. Argon2id hashing (existing). OWASP Top-10 reviewed (A01 access control, A02 crypto/refresh-hash, A07 auth failures). | ✅ |
| II | Thin Controllers, Service-Centric | `AuthController` does HTTP shaping + model validation only, delegates to `IAuthService` (+ existing `IJwtTokenService`, `IEmailSender`, `IEmailTemplateService`); DI throughout behind interfaces; **no repository layer** (EF Core directly); responses are DTOs (Mapster where an entity is projected). | ✅ |
| III | Disciplined Data Access (EF Core + PostgreSQL) | `RefreshToken : BaseEntity` (UUIDv7 + audit interceptor); reads use `AsNoTracking`; bulk revocation uses `ExecuteUpdateAsync` **setting `ModifiedDate` explicitly** (interceptor bypassed); no unbounded list endpoints introduced. | ✅ |
| IV | Secure Authentication & Session Management | Microsoft Identity flows (register/login/forgot) now implemented; argon2id hasher (existing); JWT only in httpOnly cookies + path-scoped httpOnly refresh cookie (`SameSite=Strict`, `Secure` env-driven); password policy sourced from backend Identity options + a `/auth/password-policy` endpoint the frontend renders live; lockout 5/15min honored. | ✅ |
| V | Environment Parity & Containerized Deployments | Same `docker-compose` stack; Mailpit (local) vs Resend (Dev/Prod) selected purely by `Email:Provider` config; migrations auto-apply on startup everywhere; per-service Dockerfiles unchanged. CI/CD + Terraform remain deferred (allowed by scope). | ✅ |
| VI | Consistent Conventions & Tooling | Angular components keep separate `.html`/`.css`/`.ts`; any scripts added are `.ps1` only; Tailwind styled from `DESIGN.md` tokens. | ✅ |
| — | Secret & Configuration Management | Local secrets via `.env` (sample committed, real ignored); `.env.sample` reconciled to match what the backend actually reads; Resend API key/Email from-address are env/GitHub-Environment config; no Key Vault; no secrets committed. | ✅ |
| — | Transactional Email | Reuses the existing base templates + use-case templates (verification, password-reset, password-change-notification, welcome) via the now-registered `EmailTemplateService`; Mailpit locally, Resend deployed; HTML with inline CSS. | ✅ |

**Result**: PASS — no violations; Complexity Tracking left empty. The password policy is used **as-is** (user decision); breach-screening is deferred to GitHub issue #11, not a constitution deviation.

## Project Structure

### Documentation (this feature)

```text
specs/002-authentication/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — resolved technical decisions
├── data-model.md        # Phase 1 — User changes, RefreshToken, DTOs, state transitions
├── quickstart.md        # Phase 1 — runnable end-to-end validation guide
├── contracts/
│   ├── openapi.yaml      #   /api/v1/auth/* surface
│   └── README.md
└── checklists/
    └── requirements.md  # Spec quality checklist (from /speckit-specify)
```

### Source Code (repository root)

```text
backend/                                         # .NET 10 solution (namespace JuggerHub)
├── Controllers/
│   └── AuthController.cs                         # NEW — thin: register/verify-email/resend-verification/
│                                                 #   login/refresh/logout/forgot-password/reset-password/
│                                                 #   password-policy/me  (api/v1/auth/*)
├── Services/
│   ├── Auth/
│   │   ├── IAuthService.cs                        # NEW — orchestrates Identity flows + tokens + email
│   │   ├── AuthService.cs                         # NEW
│   │   ├── IRefreshTokenService.cs                # NEW — issue/validate/rotate/revoke + reuse detection
│   │   ├── RefreshTokenService.cs                 # NEW
│   │   └── AuthResults.cs                         # NEW — result types incl. a future-proof PendingTwoFactor
│   ├── Email/
│   │   ├── IEmailSender.cs                        # NEW — send(to, subject, html)
│   │   ├── SmtpEmailSender.cs                     # NEW — MailKit → Mailpit (local)
│   │   ├── ResendEmailSender.cs                   # NEW — typed HttpClient → Resend (Dev/Prod)
│   │   └── AuthEmailService.cs                    # NEW — composes EmailTemplateService + IEmailSender + links
│   ├── Security/                                  # EXISTING — Argon2PasswordHasher, JwtTokenService (extend)
│   └── EmailTemplateService/                      # EXISTING — now REGISTERED in DI
├── Entities/
│   ├── User.cs                                    # EXISTING — (no new columns required; doc TwoFactorEnabled use)
│   └── RefreshToken.cs                            # NEW : BaseEntity
├── Data/
│   ├── AppDbContext.cs                            # EXTEND — DbSet<RefreshToken> + config (indexes)
│   └── Migrations/                                # NEW migration: AddRefreshTokens
├── Dtos/
│   └── Auth/                                      # NEW — Register/Login/Forgot/Reset/ResendVerify/VerifyEmail
│                                                  #   requests; AuthUserDto, PasswordPolicyDto, MessageDto
├── Common/
│   ├── AuthCookieDefaults.cs                      # EXTEND — refresh cookie name/options (path-scoped)
│   ├── EmailOptions.cs                            # NEW — Provider/From/Smtp*/Resend*/FrontendBaseUrl
│   └── (ProblemResponse, JwtOptions, …)           # EXISTING
├── Program.cs                                     # EXTEND — RequireConfirmedEmail stays false (gate is manual, research §1); reset-token lifespan;
│                                                  #   register IAuthService/IRefreshTokenService/IEmailSender/
│                                                  #   EmailTemplateService/SignInManager; bind EmailOptions
├── appsettings*.json                             # EXTEND — Email section
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Auth/                                      # NEW — Register/Verify/Login/Refresh/Forgot/Reset/Enum tests
    └── TestEmailSender.cs                         # NEW — captures outbound email in tests

frontend/apps/web/src/app/
├── core/
│   ├── services/auth.service.ts                   # REWRITE — real API calls + signals (currentUser, isAuthenticated)
│   ├── interceptors/auth.interceptor.ts           # EXTEND — single-flight refresh-and-retry on 401
│   ├── guards/auth.guard.ts                       # EXISTING (unchanged; now backed by real state)
│   └── models/auth.models.ts                      # NEW — request/response/policy types
├── features/
│   ├── auth/
│   │   ├── register/        { *.ts/.html/.css }    # NEW
│   │   ├── sign-in/         { *.ts/.html/.css }    # REWORK existing placeholder (+ remember-me)
│   │   ├── forgot-password/ { *.ts/.html/.css }    # NEW
│   │   ├── reset-password/  { *.ts/.html/.css }    # NEW — reads userId+token from query
│   │   ├── verify-email/    { *.ts/.html/.css }    # NEW — reads userId+token from query; check-email/resend states
│   │   └── password-policy/ { password-rules.component.* }  # NEW — live rules indicator (shared by register/reset)
│   └── (dashboard, account) # EXISTING
├── app.routes.ts                                  # EXTEND — /register /forgot-password /reset-password /verify-email
└── (app.config.ts)                                # EXISTING — interceptor already provided

frontend/apps/web-e2e/src/
└── auth.spec.ts                                   # NEW — full journey, desktop + mobile, via Mailpit API

docker-compose.yml / .env.sample / appsettings    # EXTEND — Email__FromAddress, FrontendBaseUrl, Resend key
                                                   #   (deployed); reconcile stale .env.sample JWT placeholders
```

**Structure Decision**: Web application extending the existing `backend/` (single layered API, `.csproj` at root) and `frontend/` (Nx) trees. Backend folders stay organized by technical type; new auth logic is grouped under `Services/Auth/` and `Services/Email/` (mirroring the existing `Services/Health`, `Services/Security` grouping). Frontend auth screens are grouped under `features/auth/` and are full-screen routes **outside** the shell (like the existing `sign-in`), while the shell continues to host authenticated areas. No new project or library is introduced.

## Complexity Tracking

> No constitution violations. No entries required.
