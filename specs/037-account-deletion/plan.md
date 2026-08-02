# Implementation Plan: Self-Service Account Deletion

**Branch**: `037-account-deletion` | **Date**: 2026-08-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/037-account-deletion/spec.md`

## Summary

Give a signed-in member a control in `/account` that erases their own account, replacing the manual by-hand route that feature 036's privacy policy currently has to describe (GH [#105](https://github.com/jnroesch/juggerhub/issues/105), deletion half only — export is deferred).

The research phase reframed the technical approach. **The account row cannot be deleted** — roughly twenty foreign keys into `User` are `DeleteBehavior.Restrict` by explicit design, each protecting a record the spec independently says must survive. And **the "neutral placeholder" the spec asks for already exists and already works**: `"A former player"` is keyed on the profile projecting to null, not on ban status, so deleting the profile row lands on the existing path for free.

So the shape is: **delete the `PlayerProfile`, neutralise the `User` row.** Everything the member owns cascades away with the profile; everything other people depend on keeps pointing at an account id that no longer identifies anybody. One new enum value, no new tables, no new columns.

The two genuinely new pieces of engineering are the **precondition query** that gathers every blocking obligation in one pass (FR-011 — the existing last-admin guard is per-team), and the **audit of seven `!= Banned` predicates** that a new enum value would otherwise silently pass (research R3).

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 21 zoneless (frontend)

**Primary Dependencies**: EF Core 10 + Npgsql, Microsoft Identity, Transloco (i18n), Tailwind CSS

**Storage**: PostgreSQL 18. One code-only enum extension; no schema migration beyond it.

**Testing**: xUnit + Testcontainers (backend integration), Jest (frontend), Playwright (e2e)

**Target Platform**: Linux containers — local compose, AKS Dev/Prod

**Project Type**: Web application — .NET REST backend + Angular SPA frontend

**Performance Goals**: Erasure completes inside a single request/response (SC-001, SC-006 — no scheduled process). Bounded by the number of a member's rows, which is small; no pagination concerns.

**Constraints**: Atomic — all-or-nothing (FR-038). Immediate — no grace period, no scheduled job (FR-036). Irreversible (FR-029). Multi-step transaction must run through the EF execution strategy (Principle VII).

**Scale/Scope**: Backend — 1 enum value, 1 service, 2 endpoints, ~7 predicate audits. Frontend — 1 danger-zone section on the existing `/account` page, 1 confirmation dialog, 3 language catalogues. Docs — privacy policy rights section in 3 languages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Gate | Status | Notes |
|---|---|---|---|
| I | **Security-first, never trust the client** | ✅ PASS | Subject is always the auth principal; no endpoint accepts an account id (contract). Re-auth via `CheckPasswordSignInAsync` with lockout. `Suspended`/`Banned` refused server-side, not via the sign-in gate (FR-005). Failures generic — a wrong password is indistinguishable from any other (SC-008). No internal detail on 500. |
| II | **Thin controllers, service-centric** | ✅ PASS | Two actions on the existing `AccountController`, each mapping a service status to a response. Logic in a new `IAccountDeletionService`. DTOs via explicit `.Select` projections; no object mapper. |
| III | **Disciplined data access** | ✅ PASS | No new entity, so no `BaseEntity`/UUIDv7 question. Bulk removals via `ExecuteDeleteAsync`; `ModifiedDate`/`StatusChangedAt` set explicitly where the change tracker is bypassed. No list endpoint, so pagination does not apply. |
| IV | **Secure auth & session management** | ✅ PASS | Reuses `SignInManager` and the existing refresh-token service. Cookies cleared on 204. Tokens **deleted** rather than only revoked, because FR-016 requires the retained `CreatedByIp` to go. |
| V | **Environment parity** | ✅ PASS | No new service, container, or infrastructure. Behaviour identical local/Dev/Prod. |
| VI | **Conventions & tooling** | ✅ PASS | Frontend keeps `.html`/`.css`/`.ts` separate. No `.sh` added. |
| VII | **Resilient by default** | ✅ PASS | Single transaction inside `IExecutionStrategy.ExecuteAsync` with all mutation in the delegate. **Browser-hop mutation is never auto-retried** — the deletion `POST` must be excluded from any frontend retry, since a timed-out request may already have erased the account. Blob reclaim happens after commit (cannot be rolled back). Confirmation email follows the existing transactional-email path. |
| 7 | **UI/Design compliance** | ⏳ PENDING | UI review checklist to be instantiated at implementation. DESIGN.md already defines `danger-fg`/`danger-bg`/`danger-border` tokens and an `jh-alert tone="danger"` with `role="alert"` — the destructive pattern exists and will be reused, not invented. |
| 8 | **Resilience review** | ✅ PASS | Covered under VII. The one judgement to write down at the call site: a failed confirmation email must **not** roll back the erasure. |

**Post-Phase-1 re-check**: no gate changed status. The design added no network call, no new integration, no new persisted entity, and no unbounded query.

### One item for Complexity Tracking

See below — promoting and localising the placeholder constant is an app-wide touch that this feature does not strictly need but should not leave broken.

## Project Structure

### Documentation (this feature)

```text
specs/037-account-deletion/
├── plan.md                        # This file
├── spec.md                        # Feature specification (42 FRs, 12 SCs)
├── research.md                    # Phase 0 — 9 resolved unknowns
├── data-model.md                  # Phase 1 — disposition inventory
├── quickstart.md                  # Phase 1 — validation guide
├── contracts/
│   └── account-deletion.md        # Phase 1 — 2 endpoints
├── checklists/
│   ├── requirements.md            # Spec quality (passing)
│   └── ui-review.md               # To be instantiated at implementation
└── tasks.md                       # Phase 2 — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   └── AccountEnums.cs                     # MODIFY — AccountStatus.Deleted = 3
├── Services/
│   ├── Account/
│   │   ├── IAccountDeletionService.cs      # NEW — preview + delete
│   │   └── AccountDeletionService.cs       # NEW — the whole operation
│   ├── Chat/
│   │   ├── ChatConversationService.cs      # MODIFY — 3 predicates (R3); promote PlaceholderName
│   │   └── ChatMessageService.cs           # verify only — placeholder path already correct
│   ├── Admin/
│   │   └── AdminUserService.cs             # MODIFY — reinstate/unban must refuse Deleted
│   └── Email/                              # MODIFY — deletion confirmation email
├── Controllers/
│   └── AccountController.cs                # MODIFY — GET preview, POST deletion
├── EmailTemplates/                         # NEW — account-deleted template
└── Data/Migrations/                        # NEW — enum value only

frontend/apps/web/src/app/
├── features/account/
│   ├── account.component.ts|html|css       # MODIFY — danger zone section
│   └── delete-account-dialog.*             # NEW — disclosure + confirmation
├── core/                                   # MODIFY — deletion API client; exclude POST from retry
└── i18n/{en,de,es}.json                    # MODIFY — disclosure, blockers, placeholder

specs/036-privacy-policy-imprint/            # reference only
frontend/apps/web/public/i18n/legal/         # MODIFY — rights section, 3 languages (FR-043/044/045)
```

**Structure Decision**: Standard backend + frontend split, matching every prior feature. The deletion service goes in the existing `backend/Services/Account/` namespace (currently home to language preference) rather than into `Auth` — this is an account lifecycle operation, not an authentication one, even though it verifies a password.

## Implementation phases

**Phase A — make `Deleted` safe to exist.** Add the enum value; audit all seven `!= Banned` predicates; make admin reinstate/unban refuse `Deleted`. This lands first because every later phase depends on the new state not failing open. Nothing user-visible ships.

**Phase B — the erasure itself.** `IAccountDeletionService`: precondition query, transaction inside the execution strategy, disposition per [data-model.md](data-model.md), post-commit media reclaim, confirmation email. Backend integration tests including the archived-`Conversation.Name` trap.

**Phase C — the endpoints.** Preview and delete per [contracts/account-deletion.md](contracts/account-deletion.md).

**Phase D — the control.** Danger zone on `/account`, disclosure dialog, three catalogues. UI review checklist run here.

**Phase E — the policy.** Rights section in German (authoritative), English, Spanish. Last, because until D ships the policy would describe a control that does not exist — the exact failure 036 refused to commit.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Promoting `ChatConversationService.PlaceholderName` from an `internal const` to a shared, localised string | FR-008 and FR-023 need the placeholder on non-chat surfaces (news post authors, rosters) and in three languages. It is currently English-only and private to the Chat namespace. | *Leave it and duplicate the literal* — rejected, it would put the same user-facing string in several places with no localisation, and the next surface would add a fourth copy. *Leave it English-only* — rejected, it is user-facing prose on a page that ships in three languages. The refactor is small but has app-wide reach, so it is recorded here rather than absorbed silently. |
| Defining event/party sole-admin guards that do not exist today | FR-010 requires the outcome be defined rather than discovered. `Party.CreatedBy` and `Conversation.Requester` are `Restrict`, so without a guard the constraint surfaces as a mid-transaction violation — the partial failure FR-038 forbids. | *Rely on the database constraint* — rejected, it produces a 500 instead of an actionable 409 and gives the member nothing to act on. *Cascade the party* — rejected, it would delete other members' party on one member's departure. |

## Risks carried into implementation

1. **The seven-predicate audit is the highest-risk item in the feature.** A missed `!= Banned` means a deleted account stays contactable. Four are incidentally safe (their query filters sit on the deleted profile), three are not. Test each explicitly rather than reasoning about reachability.
2. **`Conversation.Name` is a frozen string.** Archival copies a display name into a column no cascade or filter can reach ([data-model.md §3](data-model.md)). It is the single most likely place for the member's name to survive erasure and pass every other check.
3. **The disclosure is a correctness surface, not copy.** The owner's Q2 answer means messages survive. A member who reads the dialog and expects otherwise has been misled by us, in a legally-described flow. FR-025 and FR-027 are requirements, not polish.
4. **Frontend retry must exclude this POST.** Principle VII forbids auto-retrying browser-hop mutations; here a retry after a timeout would hit an account that no longer exists and report a confusing failure for an operation that actually succeeded.
5. ~~**Feature 035 is unmerged.**~~ **CORRECTED during implementation: 035 is merged** (`b801df9`, PR #104). `ProfileAvatar` is a descriptor (`ObjectKey` + `SizeBytes`) and the bytes live in blob storage, so the cascade deletes the *pointer* and orphans the *image*. FR-015 has teeth from day one and T026 is a real `IMediaStore.DeleteAsync` call. Read `ObjectKey` before the cascade, delete the object after commit. See corrected research R8.
6. **Releasing the email is not enough to free the address.** Registration sets `UserName = email` and `NormalizedUserName` is uniquely indexed, so a residual username collides and `CreateAsync` fails — landing on registration's **neutral acceptance**, which tells the member they registered when nothing was created. Every uniqueness-constrained identifier must be released (FR-034). Test by completing a real re-registration and signing in, never by asserting the email column is null.
7. **Ban-bars / deletion-permits is one code path.** Both outcomes come from the same `FindByEmailAsync` check at [AuthService.cs:79](backend/Services/Auth/AuthService.cs#L79) — a retained banned row is found and refused; a released address is not found and proceeds. Do not add a second branch for deletion; do test both directions (SC-013), because a regression in either looks like a passing test of the other.
