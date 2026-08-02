# Implementation Plan: Transactional Email Templates & Notification Preference Gating

**Branch**: `039-transactional-email-templates` | **Date**: 2026-08-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/039-transactional-email-templates/spec.md`

## Summary

Four transactional emails — event cancellation, party request/nudge, party news, and market
invite — are composed as inline HTML strings and never reach `EmailTemplateService`. They
carry no shared chrome, ignore the recipient's language, and consult no notification
preference. This plan brings them onto the mechanisms features 010/011/031 already
established, and closes the two gaps that migration alone would leave open.

The work is deliberately ordered so the *safety* change lands first. Moving the four onto the
template layer as-is would **remove** the `HtmlEncode` they perform today, because
`ReplaceVariables` does a raw `string.Replace`. So the template layer is made encode-by-default
first (which also closes the pre-existing raw `{{NEWS_EXCERPT}}` hole in team news), and only
then do the four emails move.

Similarly, giving these emails the shared footer puts a "Manage notifications" link on mail
whose toggles do nothing. Preference gating is therefore part of this feature, not a
follow-up. Event cancellation — alone among the four — has no in-app counterpart, so it
becomes a first-class notification type under a new user-facing "Events" category, which is
what makes offering it an Email toggle safe.

**No database migration is required.** Notification preferences are sparse (absence means
enabled), so a new category needs no backfill.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 20 + Nx (frontend)

**Primary Dependencies**: Entity Framework Core, Microsoft Identity, Transloco (frontend i18n),
MailKit (Mailpit local) / Resend (Dev + Prod). No new dependency is introduced.

**Storage**: PostgreSQL 18. **No schema change** — the two enums are stored as `int` and both
are append-extensible; `NotificationPreference` rows are sparse so a new category needs no
migration.

**Testing**: xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`) against
Testcontainers Postgres with a capturing `TestEmailSender`; Jest for frontend unit tests.

**Target Platform**: Linux containers on AKS (Dev/Prod), docker-compose locally.

**Project Type**: Web application — ASP.NET Core API + Angular SPA.

**Performance Goals**: No new per-recipient query. Preference filtering is one batched call per
fan-out; language is read as one extra projected column on a query that already runs.

**Constraints**: Email sends stay best-effort — a delivery or preference failure must never roll
back the originating action (cancel the event, post the news). Preference resolution is
fail-safe toward delivery.

**Scale/Scope**: 12 new email templates, 4 new template-service methods, 4 producer call sites
gated, 1 new notification type, 1 new preference category, 3 footer edits, plus frontend
renderer and type-union updates.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Gate | Assessment |
|---|---|
| **I. Security-First, Never Trust the Client** | **Strengthened.** FR-006 makes the template layer encode-by-default, closing an existing HTML-injection path where member-authored team-news text is substituted raw. No secrets or exception detail enter email bodies. Preference gating is server-side only. |
| **II. Thin Controllers, Service-Centric** | No controller changes. All work is in existing DI'd services behind interfaces (`IEmailTemplateService`, `IEmailLocalizer`, `INotificationPreferenceService`). |
| **III. Disciplined Data Access** | Culture is added to existing `.Select` projections with `AsNoTracking` rather than materializing `User` entities or issuing a per-recipient lookup (research D3). No unbounded list is returned — fan-out recipient sets are bounded by team/party/event membership, not user-supplied paging. |
| **IV. Secure Auth & Session** | Untouched. |
| **V. Environment Parity** | Templates ship in the image via the existing `.csproj` glob and behave identically in all three environments. Only the mail provider differs, which is existing configuration. |
| **VI. Consistent Conventions & Tooling** | Angular files stay split `.ts`/`.html`/`.css`. No new scripts; any added script would be `.ps1`. |
| **VII. Resilient by Default** | **No new outbound integration.** Email continues to flow through the existing `IEmailSender` with 028's resilience pipeline. Per-recipient send failures remain caught and logged without failing the originating action. No new retry loop is introduced. |
| **Transactional Email (constitution section)** | **This feature is the compliance fix.** The section requires base header/footer templates "reused across all emails"; these four are the outstanding exceptions. All templates remain HTML with inline CSS. |
| **Gate 7 — UI/Design compliance** | Engaged: the notification row gains a type and the preferences screen gains a category row. A UI review checklist is required. Both reuse existing components and tokens; no new visual pattern. |

**Result: PASS.** No violations; Complexity Tracking is therefore empty.

### Post-design re-check (after Phase 1)

Re-evaluated against `research.md`, `data-model.md`, and the two contracts. **Still PASS**, with
three findings the design surfaced:

1. **Principle I is strengthened, not merely preserved.** The design closes a pre-existing
   injection path (`team-news.html` substitutes member-authored text raw) that was not part of
   the original issue. Phase A must land before Phase B or the migration briefly *widens* that
   path — recorded as a hard ordering constraint, not a preference.
2. **Principle III is satisfied without a new query.** Culture rides on projections that already
   run; the batched preference filter replaces no per-row work because none existed. Net query
   count per fan-out increases by exactly one (the preference batch).
3. **No migration** means Gate 2's `ExecuteUpdateAsync`/`ModifiedDate` and `BaseEntity` clauses
   are not engaged at all. Confirmed by the sparse-preference model in `data-model.md`.

Gate 7 (UI/Design) remains the one open obligation, discharged during implementation via
`checklists/ui-review.md`.

## Project Structure

### Documentation (this feature)

```text
specs/039-transactional-email-templates/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output — 8 decisions, all resolved from existing code
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── email-templates.md          # Template variable contracts for the 4 new emails
│   └── notification-contracts.md   # New type/category and their client-facing shapes
└── checklists/
    ├── requirements.md  # Spec quality checklist (from /speckit-specify)
    └── ui-review.md     # Created during implementation (Gate 7)
```

### Source Code (repository root)

```text
backend/
├── EmailTemplates/
│   ├── en/  # + event-cancelled, party-request, party-news, market-invite; footer.html edited
│   ├── de/  # + the same four; footer.html edited
│   └── es/  # + the same four; footer.html edited
├── Services/
│   ├── EmailTemplateService/
│   │   ├── EmailTemplateService.cs   # encode-by-default + RawHtml + 4 Generate methods + legal URLs
│   │   └── IEmailTemplateService.cs  # 4 new method signatures
│   ├── Email/
│   │   ├── EmailLocalizer.cs         # format overload + subject/title/footer keys for the 4
│   │   ├── EventEmailService.cs      # cancellation → template path
│   │   ├── PartyEmailService.cs      # request + news → template path
│   │   └── MarketEmailService.cs     # invite → template path
│   ├── Events/EventService.cs        # in-app cancellation fan-out + email preference gate
│   ├── Parties/PartyService.cs       # email preference gate + culture projection
│   ├── Parties/PartyNewsService.cs   # email preference gate + culture projection + excerpt
│   ├── Parties/PartyRosterService.cs # nudge path — same gate
│   ├── Marketplace/MarketRequestService.cs # email preference gate + culture projection
│   └── Notifications/NotificationPreferenceService.cs # Events category copy (en/de/es)
├── Entities/NotificationEnums.cs     # + EventCancelled, + Events, + mapping case
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Auth/EmailLinkTests.cs        # extended: non-auth email, legal links, encoding
    ├── Email/                        # new: template parity + preference-gating tests
    ├── Events/EventTests.cs          # extended: cancellation notification
    └── Parties/, Marketplace/        # extended: gating assertions

frontend/apps/web/src/app/
├── core/models/notification.models.ts              # + EventCancelled type, payload, guard
├── core/models/notification-preferences.models.ts  # + 'Events' category id
├── features/alerts/notification-row/               # + link/title/supporting branches
└── assets/i18n/{en,de,es}.json                     # + alerts.row.eventCancelled* keys
```

**Structure Decision**: Existing web-application layout. This feature adds no new project,
module, or namespace — every change lands in a directory that already owns that concern. The
`Services/Email/` ↔ `Services/EmailTemplateService/` split is preserved: composition and
sending stay in the former, rendering in the latter.

## Implementation Phases

Ordered so that each phase leaves the build green and no phase can silently regress the one
before it.

### Phase A — Make the template layer safe (must precede Phase B)

Introduce `RawHtml` and encode-by-default in `ReplaceVariables`; mark the two boilerplate
variables that genuinely carry markup (research D1). Add the encoding regression test.

*Why first*: Phase B removes `HtmlEncode` calls from four services. Doing B before A would
open an injection hole in the interim, and a bisect landing between them would ship one.

### Phase B — Author templates and move the four emails onto them

Twelve template files, four `Generate…Async` methods, the `IEmailLocalizer` format overload
and its new keys, culture threaded through the four projections, and the party-news excerpt
truncation. The four services stop building HTML.

### Phase C — Gate the email channel on preferences

Filter each of the four fan-outs through `GetEnabledRecipientsAsync(..., Email)` before
sending. Party request has **two** call sites — initial fan-out and the nudge — and both must
be gated.

### Phase D — Event cancellation as a first-class notification

Append the enum members and the mapping case, fan out in-app alongside the email, add category
copy in three languages, and extend the frontend renderer and type unions.

### Phase E — Footer legal links

`PRIVACY_URL` / `IMPRINT_URL` in `AddSharedUrls`, linked from all three `footer.html` files.

### Phase F — Verification

Full test run, UI review checklist against DESIGN.md, and a manual Mailpit pass rendering each
of the four emails in each of the three languages.

## Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Encode-by-default silently changes *existing* emails that relied on markup passing through | Research D1 audits every current template variable; only `PLAN_FEATURES` and `STATUS_STYLE` need `RawHtml`, and both live in unused boilerplate. |
| A `de`/`es` template omits a placeholder its `en` sibling has — fallback is per-file, so the email ships with no call-to-action and no error | FR-026a placeholder-parity test across the three variants of each template. |
| `NotificationCategories.For` has a `_ => TeamNews` default arm, so a missing `EventCancelled` case compiles and mis-files cancellations under "Team news" | Explicit mapping case plus a test asserting the category of the new type. |
| Escaping subject lines would put encoded entities in recipients' inboxes | FR-010 states subjects are excluded; encoding stays inside `ReplaceVariables`, which subjects never pass through. |
| Gating suppresses mail during a preference-service outage | Existing behaviour is fail-safe toward delivery (`GetEnabledRecipientsAsync` returns all on error); the gating calls are wrapped as team news already does. |
| Party-request nudge path forgotten (second call site) | Called out explicitly in Phase C and covered by its own test. |

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
