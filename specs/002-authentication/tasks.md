---
description: "Task list for Authentication & Account Access"
---

# Tasks: Authentication & Account Access

**Input**: Design documents from `/specs/002-authentication/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: INCLUDED — this is a security-critical feature; backend integration tests and frontend unit/e2e tests are first-class (see spec Success Criteria).

**Organization**: Grouped by user story. P1 stories (US1–US3) are the MVP of the cycle the user wants to run locally (register → verify → login → logout → reset). US4 (silent refresh) and US5 (live policy) layer on top.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1–US5 for story-phase tasks; Setup/Foundational/Polish carry no story label
- Exact file paths included. Paths are relative to repo root.

## Path conventions

- Backend: `backend/` (single layered API; namespace `JuggerHub`)
- Frontend: `frontend/apps/web/src/app/` (Nx Angular app `web`); e2e `frontend/apps/web-e2e/`
- Backend tests: `backend/tests/JuggerHub.Api.IntegrationTests/`

> **Cross-story shared files** (sequential, not `[P]` across stories): `backend/Services/Auth/AuthService.cs`, `backend/Controllers/AuthController.cs`, `frontend/apps/web/src/app/core/services/auth.service.ts`, `frontend/apps/web/src/app/app.routes.ts`. Each story appends its methods/actions/routes to these.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Packages, folders, and config scaffolding the rest builds on.

- [x] T001 Add `MailKit` package reference to `backend/JuggerHub.Api.csproj` and create folders `backend/Services/Auth/`, `backend/Services/Email/`, `backend/Dtos/Auth/`.
- [x] T002 [P] Add an `Email` section (`Provider`, `FromAddress`, `SmtpHost`, `SmtpPort`, `FrontendBaseUrl`, `Resend:ApiKey`) to `backend/appsettings.json` (empty defaults) and `backend/appsettings.Development.json` (local Mailpit + `http://localhost:3000` frontend).
- [x] T003 [P] Reconcile `.env.sample`: remove stale `Jwt__Secret` / `Jwt__AccessTokenMinutes` / `cursor-template` placeholders; document `Email__Provider`, `Email__FromAddress`, `Email__SmtpHost`, `Email__SmtpPort`, `Email__FrontendBaseUrl`, and (commented) `Email__Resend__ApiKey` with safe local defaults that match what the backend reads.
- [x] T004 [P] Update `docker-compose.yml` backend `environment`: add `Email__FromAddress=no-reply@juggerhub.local` and `Email__FrontendBaseUrl=http://localhost:3000` (keep existing `Email__SmtpHost/Port/Provider`).

**Checkpoint**: Packages restore; config keys resolve.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story can be completed until this phase is done. This builds the email-sending path, the refresh-token machinery, the verify-gate config, the DTOs, and the `AuthController`/`IAuthService` skeleton every story extends.

### Email sending

- [x] T005 [P] Create `EmailOptions` in `backend/Common/EmailOptions.cs` (`Provider`, `FromAddress`, `SmtpHost`, `SmtpPort`, `Resend:ApiKey`, `FrontendBaseUrl`); bind `builder.Configuration.GetSection("Email")` in `backend/Program.cs`.
- [x] T006 [P] Create `IEmailSender` in `backend/Services/Email/IEmailSender.cs` (`Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)`).
- [x] T007 Create `SmtpEmailSender` (MailKit → Mailpit, no auth/TLS locally) in `backend/Services/Email/SmtpEmailSender.cs`.
- [x] T008 [P] Create `ResendEmailSender` (typed `HttpClient` → Resend REST API) in `backend/Services/Email/ResendEmailSender.cs`.
- [x] T009 In `backend/Program.cs`: register the existing `EmailTemplateService` as `IEmailTemplateService`; register `IEmailSender` selected by `Email:Provider` (Smtp vs Resend, with `AddHttpClient` for Resend).
- [x] T010 Create `AuthEmailService` in `backend/Services/Email/AuthEmailService.cs` — composes `IEmailTemplateService` (render verification / password-reset / password-change-notification / welcome HTML) + builds the SPA link from `EmailOptions.FrontendBaseUrl` + hands off to `IEmailSender`; register in DI.

### Refresh-token machinery

- [x] T011 [P] Create `RefreshToken : BaseEntity` in `backend/Entities/RefreshToken.cs` (fields per [data-model.md](./data-model.md): `UserId`, `TokenHash`, `FamilyId`, `ReplacedByTokenId`, `ExpiresAt`, `IsPersistent`, `RevokedAt`, `CreatedByIp`, `RevokedReason`).
- [x] T012 Add `DbSet<RefreshToken>` and `OnModelCreating` config (unique index `TokenHash`; indexes `UserId`, `FamilyId`; FK→`User` cascade; max lengths) in `backend/Data/AppDbContext.cs`.
- [x] T013 Generate EF migration `AddRefreshTokens` into `backend/Data/Migrations/` (depends on T011, T012; auto-applied on startup).
- [x] T014 [P] Create `IRefreshTokenService` + `RefreshTokenService` in `backend/Services/Auth/` — issue (random 256-bit, store SHA-256 hash, new/continued family), validate+rotate (single-use, reuse→revoke family), revoke-one, revoke-all-for-user (`ExecuteUpdateAsync` setting `RevokedAt` + `ModifiedDate` explicitly).

### Cookies, DTOs, mapping, gate, skeleton

- [x] T015 [P] Extend `backend/Common/AuthCookieDefaults.cs`: add `RefreshTokenCookie = "jh_refresh"` and a builder for its options (`HttpOnly`, `Secure` env-driven, `SameSite=Strict`, `Path=/api/v1/auth`, persistent vs session by remember-me).
- [x] T016 [P] Create auth DTOs in `backend/Dtos/Auth/` (`RegisterRequest`, `LoginRequest`, `ForgotPasswordRequest`, `ResetPasswordRequest`, `ResendVerificationRequest`, `VerifyEmailRequest`, `MessageResponse`, `VerificationRequiredResponse`, `AuthUserDto`, `PasswordPolicyDto`) with data-annotation validation per [contracts/openapi.yaml](./contracts/openapi.yaml).
- [x] T017 [P] Add `User → AuthUserDto` mapping in `backend/Common/MappingConfig.cs`.
- [x] T018 [P] Create `AuthResults` in `backend/Services/Auth/AuthResults.cs` (outcome type with `Succeeded` / `RequiresEmailVerification` / `PendingTwoFactor` (reserved) / `Failed`) and `IAuthService` interface in `backend/Services/Auth/IAuthService.cs` (all flow signatures).
- [x] T019 In `backend/Program.cs`: set `options.SignIn.RequireConfirmedEmail = true`; register a dedicated short-lived (~1h) password-reset token provider and point `options.Tokens.PasswordResetTokenProvider` at it (verification keeps the default ~1-day lifespan).
- [x] T020 Create `AuthController` skeleton in `backend/Controllers/AuthController.cs` (`[ApiController]`, `[Route("api/v{version:apiVersion}/auth")]`, `[ApiVersion("1.0")]`, DI `IAuthService`) with all action stubs; register `IAuthService`, `IRefreshTokenService`, `AuthEmailService`, and `SignInManager<User>` in `backend/Program.cs`.

### Frontend + test foundation

- [x] T021 [P] Create frontend auth models in `frontend/apps/web/src/app/core/models/auth.models.ts` (request/response/policy interfaces matching the contract).
- [x] T022 [P] Create `TestEmailSender` in `backend/tests/JuggerHub.Api.IntegrationTests/TestEmailSender.cs` (captures `(to, subject, html)` in-memory) and override `IEmailSender` with it in the test `JuggerHubApiFactory`.

**Checkpoint**: Foundation ready — solution builds, migration applies, DI resolves, stories can begin.

---

## Phase 3: User Story 1 — Create an account & verify email (Priority: P1) 🎯 MVP

**Goal**: A visitor registers, receives a verification email, and confirms ownership; enumeration-neutral throughout.

**Independent Test**: Register a new email → verification email appears in Mailpit → link verifies the account; registering an existing email yields the identical neutral response; an expired/invalid link is refused neutrally with a resend path.

### Tests for User Story 1

- [x] T023 [P] [US1] Backend integration tests in `backend/tests/JuggerHub.Api.IntegrationTests/Auth/RegisterVerifyTests.cs`: register creates an unverified user + sends one email (via `TestEmailSender`); existing-email registration returns the same neutral body and creates no duplicate; verify-email confirms; expired/tampered token → neutral failure; resend-verification neutral.

### Implementation for User Story 1

- [x] T024 [US1] Implement `RegisterAsync` (create user enumeration-neutrally, generate confirmation token, send verification email; existing address → no duplicate, optional "account exists" email) in `backend/Services/Auth/AuthService.cs`.
- [x] T025 [US1] Implement `VerifyEmailAsync` (`ConfirmEmailAsync`) and `ResendVerificationAsync` (neutral) in `backend/Services/Auth/AuthService.cs`.
- [x] T026 [US1] Implement `register`, `verify-email`, `resend-verification` actions in `backend/Controllers/AuthController.cs` (generic responses; validation in controller).
- [x] T027 [P] [US1] Create register component in `frontend/apps/web/src/app/features/auth/register/` (`.ts/.html/.css`) — reactive form, calls `AuthService.register`, shows "check your email" state; styled from DESIGN.md, responsive.
- [x] T028 [P] [US1] Create verify-email component in `frontend/apps/web/src/app/features/auth/verify-email/` (`.ts/.html/.css`) — reads `userId`+`token` from query, calls `verifyEmail`, renders success / expired / resend states.
- [x] T029 [US1] Add `register` / `verifyEmail` / `resendVerification` methods to `frontend/apps/web/src/app/core/services/auth.service.ts`; add `/register` and `/verify-email` routes to `frontend/apps/web/src/app/app.routes.ts` (full-screen, outside shell).
- [x] T030 [P] [US1] Jest unit test for AuthService register/verify/resend in `frontend/apps/web/src/app/core/services/auth.service.spec.ts`.

**Checkpoint**: Registration + verification work end-to-end (verified via Mailpit + DB state).

---

## Phase 4: User Story 2 — Sign in & sign out (Priority: P1)

**Goal**: A verified member signs in (with remember-me) and out; the verify gate, generic failures, and lockout are enforced; a session (access + refresh cookies) is issued.

**Independent Test**: Verified account signs in and reaches a protected area; wrong password and unknown email give one generic error; a correct password on an unverified account returns the verify path; 5 failures lock the account; sign-out clears the session.

### Tests for User Story 2

- [x] T031 [P] [US2] Backend integration tests in `backend/tests/JuggerHub.Api.IntegrationTests/Auth/LoginLogoutTests.cs`: login success sets `jh_access`+`jh_refresh` and issues a refresh row; wrong password & unknown email → identical generic 401; correct password + unverified → 403 verify signal; lockout after the configured failures; logout revokes the refresh token and clears cookies; `/auth/me` reflects auth state.

### Implementation for User Story 2

- [x] T032 [US2] Implement `LoginAsync` (`CheckPasswordSignInAsync` w/ `lockoutOnFailure: true` + dummy-hash on missing user for timing + explicit `EmailConfirmed` gate → mint access JWT + issue refresh token; return `AuthOutcome`) in `backend/Services/Auth/AuthService.cs`.
- [x] T033 [US2] Implement `LogoutAsync` (revoke presented refresh token/family) and `GetMe` in `backend/Services/Auth/AuthService.cs`.
- [x] T034 [US2] Implement `login` / `logout` / `me` actions in `backend/Controllers/AuthController.cs` — set/clear `jh_access` (Path=/) + `jh_refresh` (Path=/api/v1/auth), persistence by remember-me; `me` is `[Authorize]`.
- [x] T035 [US2] Rework sign-in component `frontend/apps/web/src/app/features/auth/sign-in/` (move/rework existing placeholder; reactive form + remember-me checkbox + generic error + "verify your email"/resend branch + links to register/forgot).
- [x] T036 [US2] Make `frontend/.../core/services/auth.service.ts` real: `login`/`logout`/`me` with signals (`currentUser`, `isAuthenticated` computed), hydrate from `/auth/me` on init; wire a logout action into `frontend/.../layout/top-nav/` (or shell).
- [x] T037 [P] [US2] Jest unit tests for AuthService login/logout/me + state signals (`auth.service.spec.ts`).

**Checkpoint**: Register → verify → **login → logout** works; verify gate + lockout enforced.

---

## Phase 5: User Story 3 — Recover a forgotten password (Priority: P1)

**Goal**: Self-service reset via emailed link; on success the password changes, all sessions are invalidated, and a notification email is sent. Enumeration-neutral request.

**Independent Test**: Request reset for a known account → reset email in Mailpit → set new password → old password rejected, new works, prior session invalidated; unknown email yields the identical neutral response; expired/used reset link refused neutrally.

### Tests for User Story 3

- [x] T038 [P] [US3] Backend integration tests in `backend/tests/JuggerHub.Api.IntegrationTests/Auth/ForgotResetTests.cs`: forgot-password neutral for known & unknown; reset updates password, revokes all refresh tokens, sends change-notification (via `TestEmailSender`); old password rejected at next login; expired/tampered reset token → neutral failure; policy enforced on the new password.

### Implementation for User Story 3

- [x] T039 [US3] Implement `ForgotPasswordAsync` (neutral; generate reset token + send reset email) and `ResetPasswordAsync` (`ResetPasswordAsync` + revoke all refresh families + send change-notification) in `backend/Services/Auth/AuthService.cs`.
- [x] T040 [US3] Implement `forgot-password` / `reset-password` actions in `backend/Controllers/AuthController.cs`.
- [x] T041 [P] [US3] Create forgot-password component in `frontend/apps/web/src/app/features/auth/forgot-password/` (`.ts/.html/.css`) — email form + neutral confirmation; `AuthService.forgotPassword`.
- [x] T042 [P] [US3] Create reset-password component in `frontend/apps/web/src/app/features/auth/reset-password/` (`.ts/.html/.css`) — reads `userId`+`token` from query, policy-validated new password, success/expired states; `AuthService.resetPassword`.
- [x] T043 [US3] Add `forgotPassword`/`resetPassword` to `auth.service.ts`; add `/forgot-password` and `/reset-password` routes to `app.routes.ts`; link "Forgot password?" from sign-in.
- [x] T044 [P] [US3] Jest unit tests for forgot/reset in `auth.service.spec.ts`.

**Checkpoint**: The full P1 cycle (register → verify → login → logout → forgot/reset) runs locally — the user's stated goal.

---

## Phase 6: User Story 4 — Stay signed in (silent refresh & rotation) (Priority: P2)

**Goal**: Expired access credentials renew silently via rotating, single-use refresh tokens; replay of a rotated token invalidates the family.

**Independent Test**: After the access token expires, a protected action succeeds via silent refresh; replaying an already-rotated refresh token forces re-login (family revoked); idle-beyond-lifetime routes to sign-in.

### Tests for User Story 4

- [x] T045 [P] [US4] Backend integration tests in `backend/tests/JuggerHub.Api.IntegrationTests/Auth/RefreshRotationTests.cs`: `/auth/refresh` rotates (old revoked, new issued, new cookies); reuse of a rotated token revokes the whole family and returns 401 with cookies cleared; expired refresh → 401.

### Implementation for User Story 4

- [x] T046 [US4] Implement `RefreshAsync` (validate+rotate via `IRefreshTokenService`, reuse detection, issue new access+refresh cookies) in `backend/Services/Auth/AuthService.cs` and the `refresh` action in `backend/Controllers/AuthController.cs`.
- [x] T047 [US4] Upgrade `frontend/.../core/interceptors/auth.interceptor.ts` to single-flight refresh-and-retry on 401 (shared in-flight refresh; skip `/auth/login|register|refresh|forgot-password|reset-password|verify-email|resend-verification`; on failure `signOut` + route to `/sign-in`); add `refresh` to `auth.service.ts`.
- [x] T048 [P] [US4] Jest unit test for the interceptor (single-flight refresh, retry once, redirect on failure) in `frontend/apps/web/src/app/core/interceptors/auth.interceptor.spec.ts`.

**Checkpoint**: Sessions persist across access-token expiry; reuse detection works.

---

## Phase 7: User Story 5 — Live password-policy feedback (Priority: P3)

**Goal**: Register/reset screens render the policy and validate it live (submit gated), with the same policy enforced server-side.

**Independent Test**: Load register/reset → rules fetched from `/auth/password-policy` and rendered → indicators update live as the password is typed → submit enabled only when all pass → server still rejects a bypassed non-compliant password.

### Tests for User Story 5

- [x] T049 [P] [US5] Backend integration test in `backend/tests/JuggerHub.Api.IntegrationTests/Auth/PasswordPolicyTests.cs`: `GET /auth/password-policy` returns the `IdentityOptions.Password` values.

### Implementation for User Story 5

- [x] T050 [US5] Implement the `password-policy` action (`[AllowAnonymous]`, from `IOptions<IdentityOptions>`) in `backend/Controllers/AuthController.cs` + `GetPasswordPolicy` in `AuthService`.
- [x] T051 [P] [US5] Create reusable password-rules component in `frontend/apps/web/src/app/features/auth/password-policy/` (`.ts/.html/.css`) — fetches the policy, shows per-rule indicators, exposes an `allValid` output.
- [x] T052 [US5] Integrate the password-rules component into the register (T027) and reset-password (T042) forms; gate submit until all rules pass; add `getPasswordPolicy` to `auth.service.ts`.
- [x] T053 [P] [US5] Jest unit test for the password-rules live validation.

**Checkpoint**: All five stories functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [x] T054 [P] Playwright e2e `frontend/apps/web-e2e/src/auth.spec.ts` — full register → verify → login → logout → forgot/reset journey at **desktop and mobile** viewports, reading verification/reset links from the Mailpit API (`GET http://mailpit:8025/api/v1/messages`).
- [x] T055 Ensure `docker-compose.test.yml` runs the new backend auth tests and the Playwright auth journey (services reach Mailpit); adjust if needed.
- [x] T056 Security review pass (OWASP / never-trust-the-client): confirm no token/secret/stack-trace in any response or log; both cookies `HttpOnly` + `SameSite=Strict` + env-driven `Secure`; enumeration neutrality (bodies + rough timing); lockout honored; refresh stored only as hash. Run `/security-review` on the branch diff and resolve findings.
- [x] T057 [P] Update `README.md` auth section and confirm `.env.sample` ↔ backend config parity.
- [x] T058 Run the full [quickstart.md](./quickstart.md): `docker compose up` manual journey + all three test suites (backend integration, frontend Jest, Playwright desktop+mobile) green.
- [x] T059 Record outcomes (what changed, why, key decisions, verification results, follow-ups e.g. GitHub issue #11 HIBP).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)** → no deps.
- **Foundational (P2)** → depends on Setup; **BLOCKS all stories**. Within it: email-sending (T005–T010), refresh machinery (T011–T014), and cookies/DTOs/skeleton (T015–T020) are largely independent tracks; T013 needs T011+T012; T009/T020 wire DI last.
- **US1 / US2 / US3 (P1)** → depend on Foundational. US1 is the MVP entry. US2 and US3 are independently testable but in practice exercised together with US1 for the full cycle; they all append to the shared `AuthService`/`AuthController`/`auth.service.ts`/`app.routes.ts`, so run them **in priority order** rather than concurrently in a single-developer flow.
- **US4 (P2)** → depends on Foundational + US2 (a session must exist to refresh).
- **US5 (P3)** → depends on Foundational; integrates into US1/US3 forms (T052), so do after those.
- **Polish (P8)** → after the stories you intend to ship.

### Within each story

- Test task first (write it to fail), then service method(s) → controller action(s) → frontend component(s)/service/routes → unit test.

### Parallel opportunities

- Setup: T002/T003/T004 in parallel.
- Foundational: T005/T006/T008, T011, T014, T015/T016/T017/T018, T021/T022 are `[P]` (distinct files); T007→needs T006, T009/T010/T020 wire DI after their deps, T012→T011, T013→T012.
- Per story: the `[P]` component/test files (e.g. T027/T028/T030; T041/T042/T044) run in parallel; the shared-file tasks do not.

---

## Implementation strategy

### MVP = the local cycle the user asked for

1. Phase 1 (Setup) → Phase 2 (Foundational).
2. Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3).
3. **STOP & VALIDATE**: run [quickstart.md](./quickstart.md) — register, verify via Mailpit, login, logout, forgot/reset all work locally. This is the deliverable.

### Incremental hardening

4. Phase 6 (US4) — silent refresh so sessions don't drop at 15 min.
5. Phase 7 (US5) — live password-policy feedback.
6. Phase 8 — e2e desktop+mobile, security review, quickstart sign-off, memory.

---

## Notes

- `[P]` = different files, no incomplete-task dependency. Shared cross-story files (`AuthService.cs`, `AuthController.cs`, `auth.service.ts`, `app.routes.ts`) are edited sequentially.
- Never put token material/secrets/passwords in response bodies or logs (FR-021/FR-022/FR-024).
- Out of scope (do not implement): MFA/2FA, OAuth/SSO, HIBP breach-screening (GitHub issue **#11**).
- Commit after each task or logical group; keep commits small. Stop at any checkpoint to validate independently.
