# Data Model: Privacy Policy & Imprint (036)

## Persisted entities: none

This feature adds **no entity, no table, no column, no migration, and no query**. It reads nothing
from the database and writes nothing to it. `backend/` is untouched.

That is not an oversight to be corrected later — it is the design. The feature's subject matter is
the data the platform *already* stores; describing it does not require storing more. See
[plan.md](./plan.md) Constitution Check, principles II and III (N/A).

The only "model" this feature has is the shape of its content, documented below and contracted in
[contracts/content-catalog.md](./contracts/content-catalog.md).

---

## Content model

Legal text is a lazily-loaded Transloco scope, `legal`, served from
`frontend/apps/web/public/i18n/legal/{en,de,es}.json` (research R2).

### Structure

```text
legal
├── meta
│   ├── lastUpdated            # ISO date string, rendered per active locale
│   ├── authoritativeNotice    # shown on the en/es versions only (FR-019)
│   └── ...
├── privacy
│   ├── title
│   ├── intro
│   ├── toc.*                  # section labels, one per `sections` child
│   └── sections
│       ├── controller         # FR-007 — who is responsible
│       ├── whatWeHold         # FR-004 — all data categories, by CATEGORY not feature
│       ├── whyAndOnWhatBasis  # FR-005 — purposes + lawful bases, other than analytics
│       ├── analytics          # FR-006 — incl. the verbatim-path disclosure
│       ├── legalBasis         # FR-010/FR-014 — legitimate interest + balancing test
│       ├── storage            # FR-011 — cookies and device storage
│       ├── processors         # FR-008 — hosting and email delivery
│       ├── retention          # FR-005 — how long, framed durably
│       ├── rights             # FR-009 — a route that is actually honoured
│       └── objection          # FR-013 — the DNT/GPC opt-out, in plain language
└── imprint
    ├── title
    ├── operator               # ⚠ blocked on spec Q1 — see plan.md Open dependency
    ├── contact
    └── responsibility
```

Each `sections.*` node is `{ heading, body[] }`, where `body` is an **array of paragraph strings**.
Paragraphs are separate array entries rather than one string containing markup, so no HTML is
interpolated and no `[innerHTML]` binding is introduced anywhere (plan Constitution Check,
Principle I).

**Ten sections, organised by category rather than by feature** (spec Clarifications, 2026-08-01).
An earlier revision had sixteen — one per product area — which meant every shipped feature dated a
legally binding document in three languages. `whatWeHold` now absorbs what were separate `account`,
`profile`, `location`, `chat`, `participation`, `eventContacts`, `media` and `language` sections,
worded so a new way to take part is covered without an edit here. The table further down still
enumerates the entities, because *the audit* must be exhaustive even though the prose is not.

The section list is duplicated as `PRIVACY_SECTIONS` in `privacy.component.ts` — deliberately, so
rendering never depends on JSON key order — and a test asserts the two agree, since a section
present in the catalog but missing from the order would be a disclosure that silently never
renders.

### Invariants

| # | Invariant | Enforced by |
|---|---|---|
| DM-1 | `en`, `de` and `es` have **identical key sets** | catalog completeness test (research R8) |
| DM-2 | No value contains the placeholder sentinel | placeholder-guard test — **fails until spec Q1 is answered**, by design |
| DM-3 | The `de` version is authoritative; `en` and `es` render `meta.authoritativeNotice` | component test + UI review |
| DM-4 | No value contains HTML markup | catalog test asserting no `<` in any leaf value |
| DM-5 | `meta.lastUpdated` is a valid ISO date | catalog test |

DM-1 exists because the global Transloco config sets `useFallbackTranslation: true` with an English
fallback. For interface labels that is correct and required by 031; for a German-authoritative legal
document it means a missing German paragraph renders in English **with no visible signal**. The test
turns a silent, legally-relevant defect into a build failure.

---

## Data the policy *describes* (not data this feature stores)

Compiled from `backend/Entities/` (52 files) and cross-checked against the feature specs. This is
the checklist SC-002 audits against, and the reason the inventory belongs in a design artifact
rather than only in prose.

| Category | Where it lives | Feature | Non-obvious detail the policy must state |
|---|---|---|---|
| Account & credentials | `User` (ASP.NET Identity) | 002 | Password is stored only as an argon2 hash |
| Account status | `User.Status`, `User.StatusChangedAt` | 013 | A ban is a soft-delete; the row is retained |
| Session records | `RefreshToken` | 002 | **`CreatedByIp` retains an originating IP address per session.** The token itself is stored only as a SHA-256 hash |
| Email address | `User.Email` | 002, 028 | Leaves the platform to the email provider |
| Profile | `PlayerProfile`, `ProfilePompfe` | 003, 026 | Private by default; public is opt-in and direct-link-only |
| Home city | `City`, `CityReference` | 030 | Resolved from seeded data — **no external geocoder is called** |
| Chat & DMs | `Conversation`, `ChatMessage`, `ConversationParticipant`, `UserBlock` | 019, 022, 027 | **Conversations are snapshotted, not deleted**, when a team is deleted or an event cancelled |
| Participation | `TeamMembership`, `EventSignup`, `TrainingResponse`, `PartyMember`, `MarketRequest`, … | 005–018 | Visible to other members |
| Recognition | `BadgeAward`, `AchievementAward` | 012, 014 | |
| Notifications | `Notification`, `NotificationPreference` | 010, 011 | |
| Media | `ProfileAvatar` → object storage | 034, 035 | Bytes live in Azure Blob Storage; delivery is proxy-only |
| Language preference | `User.PreferredLanguage` | 031 | Anonymous choice is stored in the browser instead |
| Admin actions | `AdminActionRecord` | 013 | Moderation decisions are recorded against the account |
| Analytics | Umami store (separate DB, same instance) | 033 | **Page paths verbatim** — profile and team paths name their subject. Query strings are not recorded (033 FR-008a) |

### Retention

**No automated retention, expiry, or purge job exists anywhere in the platform.** Verified: no
scheduled deletion, no TTL, no sweep other than 035's admin-triggered orphan reclamation (which
targets unreferenced blobs, not member data). The policy states this honestly — data is kept until
the account is deleted on request.

There is also **no self-service export or account deletion** in the product (verified across
`backend/Controllers`, `backend/Services`, and the frontend). The policy therefore documents a
manual contact route and must not describe a control that does not exist (FR-009). Building one is
out of scope here and warrants its own issue.
