# Implementation Plan: Terms of Use with Community Rules

**Branch**: `041-community-guidelines-terms` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/041-community-guidelines-terms/spec.md`

## Summary

Add a third legal document — a binding **Terms of Use** at `/terms`, with the community rules as
a section inside it — and make registration require an active, recorded agreement to it.

The enforcement *machinery* already exists (feature 013: `Suspended`/`Banned` + an append-only
`AdminActionRecord`). What is missing is the agreement it rests on and the evidence that each
member entered into it. This feature supplies both and builds **no new moderation capability**.

Three moving parts:

1. **The document** — a new `terms` node in the existing `public/i18n/legal/{en,de,es}.json`
   catalogues, rendered by the existing `LegalPageComponent` at a new unguarded off-shell route.
   German authoritative, exactly as feature 036 established.
2. **Acceptance at registration** — an unticked checkbox on the register form, and three new
   fields on `POST /auth/register` validated **server-side**. The checkbox is UX; the server is
   the boundary.
3. **The record** — a new `TermsAcceptance` entity written in the *same* `SaveChanges` that
   creates the account, so an acceptance record cannot exist without an account or vice versa.

The load-bearing design decision is that the client sends **the version string it actually
displayed**, and the server refuses anything that is not the current version. That is what makes
the record evidence of what the person saw rather than of what the server assumed. See
[research.md §1](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (frontend, Nx monorepo)

**Primary Dependencies**: EF Core 10 + Npgsql, ASP.NET Core Identity, Transloco (i18n),
Tailwind CSS. **No new package on either side.**

**Storage**: PostgreSQL 18 — one new table (`TermsAcceptances`), one new migration. No column
is added to `AspNetUsers`.

**Testing**: xUnit + Testcontainers (backend integration); Jest (frontend unit + catalogue
guards). Both already established.

**Target Platform**: Linux containers — AKS (Dev/Prod), docker-compose (local)

**Project Type**: Web application — `backend/` (.NET API) + `frontend/` (Angular/Nx)

**Performance Goals**: No new hot path. The register form gains one static-asset fetch of the
legal catalogue (~20 KB, cacheable, already served by nginx). Registration itself gains one
in-memory string comparison and one extra row in an existing `SaveChanges`.

**Constraints**:

- `/terms` makes **no backend call** and carries **no guard** — same rule as `/privacy` and
  `/imprint` (026 exception, 036 `contracts/routes.md` RC-1/RC-2). A 401-triggered refresh must
  never bounce a reader away from a document they are entitled to read.
- The global `useFallbackTranslation: true` + `fallbackLang: 'en'` means a missing German key
  renders **English inside the legally binding German document**, silently. The existing
  catalogue guard test is what prevents this and must cover the new keys.
- The acceptance record must survive feature 037 erasure. It is **not** owned data.

**Scale/Scope**: One new entity, one migration, one new route, one new backend option, three
catalogue documents, one shared-component refactor (2 → N cross-links). Roughly 30 files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see bottom.*

| Principle | Applies? | How this feature satisfies it |
|---|---|---|
| **I — Security-first, never trust the client** | **Yes, centrally** | The disabled submit button is explicitly *not* the boundary (FR-017 vs FR-018). `POST /auth/register` validates acceptance, the version string, and the language allowlist server-side and refuses otherwise. No new `[AllowAnonymous]` surface is added — `register` already carries it. Refusals return ProblemDetails via the existing handler; no exception detail escapes. |
| **II — Thin controllers, service-centric** | Yes | `AuthController.Register` gains only two `switch` arms mapping new `RegisterStatus` values to status codes. All validation and the write live in `AuthService.RegisterAsync`. No new controller. The one new read (FR-025) returns a DTO built with an explicit `.Select`. |
| **III — Disciplined data access** | Yes | `TermsAcceptance : BaseEntity` (UUIDv7 PK, interceptor-managed dates). `DeleteBehavior.Restrict` on the user FK, mirroring `AdminActionRecord`. Written via the tracked graph so the audit interceptor runs. The FR-025 read is `AsNoTracking` + projection. No list endpoint is added, so the pagination rule is not engaged. |
| **IV — Secure auth & sessions** | Yes | Registration flow only; no change to tokens, cookies, password policy, or session handling. The new fields ride the existing anonymous `register` endpoint. |
| **V — Environment parity** | Yes | Same behaviour in local/Dev/Prod. The terms version is configuration with a safe built-in default (`TermsOptions`), identical in shape across environments. No env-specific text, no env-specific version. |
| **VI — Conventions & tooling** | Yes | Angular `.html`/`.css`/`.ts` stay separate. No new scripts (so the `.ps1`-only rule is not engaged). Tailwind tokens only. |
| **VII — Resilient by default** | **Partially engaged** | One new *browser-hop* fetch: the register page reads the legal catalogue. It is a **GET of a static asset**, so it inherits feature 028's shared retry interceptor — no hand-rolled retry, no new `HttpClient` config. Registration itself is a mutation and is **never** auto-retried, which is unchanged. **No new outbound backend integration**, so no timeout/breaker tuning is required. The DB write joins an existing transaction. |
| **Gate 7 — UI/design compliance** | Yes | Ships UI (a document page + a form control), so a `checklists/ui-review.md` is instantiated from the template and verified against the diff before sign-off. DESIGN.md's **Long-form content** section already covers document pages and needs no amendment. |

**Result: PASS.** No violations, so the Complexity Tracking table is omitted.

One point deserves explicit recording rather than a checkbox: **the document reserves a right the
product cannot exercise through any interface.** FR-005 reserves content removal; no admin
endpoint deletes member content, so removal stays a manual database operation. This is
owner-decided and is bounded by FR-008, which forbids the text from *describing* tooling that
does not exist. It is a scope decision, not a constitutional violation — but implementation must
not "helpfully" close the gap by building moderation surfaces.

## Project Structure

### Documentation (this feature)

```text
specs/041-community-guidelines-terms/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — the six decisions that shape the build
├── data-model.md        # Phase 1 — TermsAcceptance + its lifecycle
├── quickstart.md        # Phase 1 — runnable validation scenarios
├── contracts/
│   ├── register-api.md  # POST /auth/register contract change
│   ├── routes.md        # /terms route + link-surface contract
│   └── catalog.md       # legal catalogue shape + release guards
├── checklists/
│   ├── requirements.md  # Spec quality (complete)
│   └── ui-review.md     # Gate 7 — verified during implementation
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── TermsAcceptance.cs              # NEW — the evidence row
│   └── User.cs                         # + TermsAcceptances navigation
├── Dtos/
│   ├── Auth/AuthRequests.cs            # + 3 fields on RegisterRequest
│   └── Account/TermsAcceptanceDto.cs   # NEW — FR-025 read shape
├── Services/
│   ├── Auth/AuthService.cs             # validate + write, inside CreateAsync
│   ├── Auth/AuthResults.cs             # + 2 RegisterStatus values
│   └── Terms/TermsOptions.cs           # NEW — the authoritative version
├── Controllers/AuthController.cs       # + 2 switch arms
├── Data/
│   ├── AppDbContext.cs                 # + DbSet + entity configuration
│   └── Migrations/                     # NEW — create TermsAcceptances
└── tests/JuggerHub.Api.IntegrationTests/
    └── Terms/                          # NEW — acceptance, refusal, survival, parity

frontend/apps/web/
├── public/i18n/
│   ├── legal/{en,de,es}.json           # + the whole `terms` document
│   └── {en,de,es}.json                 # + legal.terms label, + register strings
└── src/app/
    ├── app.routes.ts                   # + /terms (unguarded, off-shell)
    ├── core/
    │   ├── models/auth.models.ts       # + 3 fields on the register payload
    │   └── i18n/legal-catalog.spec.ts  # + version-parity assertion
    ├── features/
    │   ├── legal/
    │   │   ├── legal-content.service.ts    # + terms in LegalContent
    │   │   ├── legal-page.component.*      # siblingLink → siblings[] (N cross-links)
    │   │   ├── privacy/ · imprint/         # updated for the new cross-link shape
    │   │   └── terms/                      # NEW — TermsComponent + section order
    │   └── auth/register/register.component.*  # + acceptance control
    └── shared/ui/legal-links/          # + the /terms link (all 11 placements inherit it)
```

**Structure Decision**: The existing web-application split is used unchanged. This feature adds a
third document to the feature-036 legal infrastructure rather than introducing any new pattern —
the same catalogue files, the same page component, the same route treatment, the same guard
tests. The only structural addition is a `Services/Terms/` folder holding the version option,
kept out of `Services/Auth/` because the version is document metadata that a future re-acceptance
flow will also need.

## Key design decisions

Full reasoning in [research.md](./research.md). The four that most constrain implementation:

1. **The client sends the version it displayed; the server refuses any other.** This is what
   makes the record evidence rather than assumption. Consequence: the register page must load the
   legal catalogue, and a failed load **blocks submission** — which the spec's edge case requires
   anyway ("must not be pushed into agreeing to a document they were unable to read").

2. **The acceptance row is written inside `UserManager.CreateAsync`'s `SaveChanges`,** via a
   navigation property on `User` — exactly how `PlayerProfile` is already created atomically in
   `RegisterAsync`. This is what makes FR-022 (no orphan records) structural rather than a
   cleanup path.

3. **`TermsAcceptance` is not owned data.** It uses `DeleteBehavior.Restrict` like
   `AdminActionRecord`, and it must **NOT** be added to `AccountDeletionService.EraseOwnedDataAsync`.
   That method's list is the single most likely place for this to be broken later, so the test
   suite asserts survival across an erasure directly.

4. **`LegalPageComponent`'s cross-link becomes a list.** It currently takes `siblingLink` +
   `siblingLabelKey` — a shape that assumes exactly two documents. A third breaks the assumption,
   so it becomes `siblings: {link, labelKey}[]`. Small refactor, but it touches the privacy and
   imprint pages, so it is sequenced before the terms page is added.

## Risks & gotchas

| Risk | Why it bites | Mitigation |
|---|---|---|
| **Silent English inside the German terms** | `useFallbackTranslation: true` renders the `en` value for any key missing from `de`, with no error — inside the one document that legally binds. | The existing `legal-catalog.spec.ts` walks the whole file, so new keys are covered automatically. Verified as a task, not assumed. **Never** fix a failure by changing the global fallback (breaks 031 app-wide). |
| **Version drift between the text and the server** | Someone edits the catalogue text and forgets the backend constant. Records then name a version whose text nobody saw — the exact failure this feature exists to prevent. | Backend integration test walks up to `frontend/apps/web/public/i18n/legal/` and asserts every catalogue's `terms.version` equals `TermsOptions.CurrentVersion`. Reuses the repo-walk pattern already proven in `TemplateParityTests`. Throws (not skips) if the files are not found. |
| **The version differs *between* languages** | The identical-key-set guard checks **keys**, not values — and values are *supposed* to differ (they are translations). The version is the one leaf that must be identical everywhere. | A dedicated value-equality assertion for `terms.version` across en/de/es. |
| **A later change adds `TermsAcceptances` to the erasure list** | `EraseOwnedDataAsync` reads as "delete everything with this UserId", and this table has a UserId. Deleting it destroys the consent evidence. | `Restrict` FK makes a naive delete fail loudly rather than silently succeed, plus an explicit test that the record survives erasure, plus an XML-doc warning on the entity. |
| **Registration breaks for an open tab across a deploy** | A stale cached catalogue sends an old version and is refused. | Deliberate: a distinct `409` with a "reload and re-read" message, not a generic failure. Version changes are rare by design (FR-014 is publish-only). |
| **`RegisterRequest` is a positional record** | Its validation attributes must sit on **constructor parameters**, not properties — MVC throws otherwise. The file already carries this warning. | Follow the existing comment in `AuthRequests.cs`; new fields get parameter-level attributes. |
| **The neutral-registration response hides failures** | `RegisterAsync` returns `Accepted()` for several real failures to stay enumeration-neutral. Terms refusals must **not** be folded into that — they are not enumeration-sensitive and a silent "accepted" would strand the user. | Terms refusals return distinct statuses (`400`/`409`) before any account lookup happens, so they never interact with enumeration neutrality. |
| **Reserving an unbuildable right** | FR-005 reserves content removal the product cannot perform. | Bounded by FR-008 (no describing tooling that does not exist) and recorded in Out of Scope. Follow-up tracked separately; implementation must not build moderation surfaces. |

## Sequencing

The refactor comes before the addition, and the text comes before the gate:

1. **Catalogue + guards** — add the `terms` document to all three files; extend the guard tests.
   Nothing renders it yet, but the release blockers are live from this point.
2. **Cross-link refactor** — `LegalPageComponent` 2 → N siblings; privacy and imprint updated.
3. **`/terms` page** — route, component, section order, `jh-legal-links` entry. The document is
   now readable, which FR-016 needs before any acceptance control can link to it.
4. **Backend** — entity, migration, options, validation, write, and the FR-025 read.
5. **Register UI** — the acceptance control, wired to the now-existing document and endpoint.
6. **Verification** — quickstart scenarios + the UI review checklist.

Steps 1–3 ship a readable document with no behaviour change; steps 4–6 close the gate. That
ordering means the document is never linked before it exists, and the gate never precedes the
text.

## Post-design Constitution re-check

Re-evaluated after Phase 1 artifacts were written. **Still PASS.**

- The data model added no repository layer, no object mapper, and no unbounded read; the single
  new query is keyed by user id and projected.
- The contract change added no new endpoint and no new anonymous surface.
- The one new browser-hop request is a static-asset `GET` inheriting shared resilience — it
  introduced no per-call-site retry policy and no new outbound backend integration.
- No secret, token, or personal content enters a log: refusal logging records the *outcome*, not
  the submitted values.
