# Phase 0 Research: First-Login Onboarding Flow

All items below were resolved before Phase 1. No open `NEEDS CLARIFICATION` remain. Two product decisions (team-step handling, re-prompt semantics) were resolved with the requester up front and are recorded in the spec's Assumptions.

## 1. Where is the "first login" trigger, and how is it detected?

**Decision**: Trigger at the **sign-in success** path on the frontend. After `AuthService.login()` resolves, inspect `user.onboardingCompleted`: if `false`, navigate to `/onboarding`; otherwise navigate to the dashboard (`/`). Detection is a server-provided boolean on `AuthUserDto`, derived from `PlayerProfile.OnboardingCompletedAt != null`.

**Rationale**: Email verification (feature 002) ends on the verify screen and sends the user to **sign in**; the first authenticated entry into the app is therefore the sign-in action. Keying off login (rather than session restore) matches FR-002 precisely and keeps the trigger in one obvious place. The `sign-in.component` today navigates to `/account` — a placeholder — so redecorating that redirect is the natural, minimal change.

**Alternatives considered**:
- *Redirect inside `authGuard`/session hydrate on every load.* Rejected: it would re-route users who intentionally left mid-flow on every app open (naggy), and the guard runs for many routes — spreading the trigger. We instead use a dedicated `onboardingGuard` only on the `/onboarding` route to bounce already-onboarded users away, not to force anyone in.
- *A backend redirect (302).* Rejected: this is an SPA; routing is a client concern, and the API stays a pure JSON contract.

## 2. Where does the completion state live?

**Decision**: A nullable **`OnboardingCompletedAt` (`DateTime?`)** on the existing `PlayerProfile` entity (1:1 with `User`). `null` = not yet onboarded; a UTC timestamp = onboarded (and *when*).

**Rationale**: Profile data already lives on `PlayerProfile` (not on the Identity `User`), keeping Identity clean per 003. A timestamp is strictly more informative than a boolean (useful later for analytics/cohorts) at no extra cost, and "is completed" is just `!= null`. No index is needed: the value is only ever read for the current authenticated user via the existing `UserId` relationship.

**Alternatives considered**:
- *Boolean `HasOnboarded`.* Rejected: loses the "when," and a timestamp is the conventional audit-friendly choice consistent with `CreatedDate`/`ModifiedDate`.
- *Derive "onboarded" from "profile has a non-default display name".* Rejected: conflates skipping (a valid terminal state that saves nothing) with not-yet-onboarded, and a user could legitimately keep their handle as their display name.
- *Separate `OnboardingState` table.* Rejected: over-modeled for one nullable field on an entity that is already 1:1 with the user.

## 3. How does the flag reach the client without breaking the existing auth mapping?

**Decision**: Add `bool OnboardingCompleted` to `AuthUserDto`. Because `AuthService` resolves users via `UserManager` (which does **not** eager-load the `Profile` navigation), do **not** rely on Mapster to project the nav. Instead, in each of the three paths that emit an `AuthUserDto` — `LoginAsync`, `RefreshAsync`, `GetUserAsync` (backing `/auth/me`) — build the DTO as `user.Adapt<AuthUserDto>() with { OnboardingCompleted = await _profiles.HasCompletedOnboardingAsync(user.Id, ct) }`. `HasCompletedOnboardingAsync` is a single projected `AsNoTracking` query on `PlayerProfiles`.

**Rationale**: `AuthUserDto` is a positional `record`, so a `with` expression cleanly sets the extra field after the base Mapster adapt. Threading through **all three** paths is essential: if `/me` or `/refresh` returned a stale `false`, an already-onboarded user's session restore would report "not onboarded." `AuthService` already depends on `IProfileService`, so no new dependency is introduced. The query is a lightweight boolean projection — no entity tracking, no extra columns pulled.

**Alternatives considered**:
- *`Include(u => u.Profile)` on the UserManager queries.* Rejected: changes existing, security-sensitive lookups and pulls the whole profile row for one boolean.
- *Map the nav via Mapster.* Rejected: the nav isn't loaded on the UserManager-fetched entity, so it would silently map to `false`.

## 4. What does "mark complete" look like, and how is it made idempotent + owner-safe?

**Decision**: `POST /api/v1/profiles/me/onboarding/complete` (JWT-cookie scheme, owner-only) → `IProfileService.CompleteOnboardingAsync(userId)`. The service loads the caller's profile (tracked); if `OnboardingCompletedAt` is `null` it sets it to `DateTime.UtcNow` and saves (the `AuditFieldsInterceptor` updates `ModifiedDate`); if already set, it returns success without changing the timestamp. Returns an enum/bool distinguishing **completed** from **profile-not-found** → controller maps to `204 No Content` / `404`. `401` when unauthenticated.

**Rationale**: Idempotency (first completion stands) satisfies the edge case and lets the client call it freely on any terminal exit without ordering concerns. A single tracked read+set+save keeps audit fields automatic (no `ExecuteUpdate`, so no manual `ModifiedDate`). Owner-scoping via the JWT `sub` (never a client id) matches every other `/me` route in `ProfilesController`.

**Alternatives considered**:
- *Fold "complete" into the existing `PUT /profiles/me`.* Rejected: completion must fire even when the user **skips everything** (no profile fields to update), and "I'll do this later" from the Welcome screen writes nothing — so completion needs to be independent of a field update.
- *`ExecuteUpdateAsync` set.* Rejected here: bypasses the audit interceptor (would need a manual `ModifiedDate`) and can't as cleanly express "only set if currently null" for the idempotent no-op; a single-row tracked update is simpler and correct.

## 5. Persisting the collected fields — reuse vs. new

**Decision**: Reuse feature 003's owner endpoints. Display name, city (hometown), bio (description), and pompfen selections persist via `PUT /profiles/me` (`UpdateProfileRequest`); the picture via `PUT /profiles/me/avatar`. The client submits the profile update once (with whatever the user provided), uploads the avatar if one was chosen, then calls complete.

**Rationale**: `UpdateProfileRequest(DisplayName, Hometown, Description, Pompfen[])` already models exactly the onboarding fields, with server-side validation and owner scoping already in place. Reuse means zero new write paths, zero new validation surface, and automatic parity with the normal profile editor (SC's "reflected immediately" is free because it's the same store). The flow batches the text/selection fields into one `PUT` at the end (values held in the component's step state), keeping writes minimal.

**Alternatives considered**:
- *Per-step writes (save on each Continue).* Rejected for this round: more requests, partial-write states, and no requirement for cross-device resume. Holding values in memory and writing once on finish is simpler and matches "one surface, back/forward preserves values." (A future enhancement could autosave per step.)

## 6. Team step with no Teams model

**Decision**: Render the team step as a **visual placeholder** matching the wireframe (search field + sample teams), clearly labeled as not-yet-functional; **persist nothing**. Feature 003 shipped teams as a UI-only stub, so there is no store to write to.

**Rationale**: Keeps the drawn flow intact and sets the expectation that teams are coming, without fabricating data or a schema. FR-021 makes the non-persistence explicit and testable.

**Alternatives considered**: Omit the step (loses the intended shape); add a free-text team field (introduces a real field the future Teams feature would have to reconcile/migrate). Both rejected in favor of the honest placeholder.

## 7. Making onboarding genuinely one-time

**Decision**: Two guards on the `/onboarding` route: the existing `authGuard` (must be signed in, else `/sign-in`) plus a new `onboardingGuard` that redirects **already-onboarded** users to the dashboard. The trigger *into* onboarding stays solely at sign-in. Both finish and dismiss call `completeOnboarding()` before leaving.

**Rationale**: `authGuard` already hydrates the session; `onboardingGuard` reuses that state (`ensureSession()` → check `onboardingCompleted`) to prevent a completed user from re-entering via a bookmarked/typed URL (edge case), while never *forcing* a user back in on ordinary navigation.

**Alternatives considered**: Guard-driven forced entry from anywhere (naggy, rejected — see §1); component-only check (works, but a guard is more testable and prevents a flash of the flow before redirect).
