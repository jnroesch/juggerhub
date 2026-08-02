# Phase 1 Data Model: Transactional Email Templates & Notification Preference Gating

**Feature**: 039-transactional-email-templates | **Date**: 2026-08-01

## Summary

**No database migration is required by this feature.** No table is added, dropped, or altered.
Two enums gain an appended member; because notification preferences are sparse and both enums
persist as `int`, existing rows keep their meaning and new cells default to enabled.

---

## Modified enumerations

### `NotificationType` (`backend/Entities/NotificationEnums.cs`)

| Member | Value | Status |
|---|---|---|
| `TeamInvite` … `TrainingUpdated` | 0–7 | Unchanged |
| **`EventCancelled`** | **8** | **New** — an event the recipient signed up for was cancelled by its organiser. Link-only (no inline actions). |

Appended, so no stored `Notification.Type` value changes meaning. The enum is documented as
extensible: "new members append without touching existing rows or their payloads".

### `NotificationCategory` (`backend/Entities/NotificationEnums.cs`)

| Member | Value | Status |
|---|---|---|
| `InvitesAndRoster` | 0 | Unchanged |
| `TeamNews` | 1 | Unchanged |
| `Trainings` | 2 | Unchanged |
| **`Events`** | **3** | **New** — event lifecycle notices. Currently carries `EventCancelled` only. |

### `NotificationCategories.For` mapping

One case added: `EventCancelled => Events`.

> **This case is mandatory, not optional.** The switch ends in `_ => NotificationCategory.TeamNews`.
> Omitting the case therefore compiles cleanly and files every cancellation under the user's
> "Team news" preference — a recipient who muted team news would silently stop receiving
> cancellation notices for events they joined. A test asserts the mapping directly.

---

## Unchanged entities (consumed, not modified)

### `NotificationPreference`

Shape unchanged. Relevant existing properties: `UserId`, `Category`, `Channel`, `Enabled`.

The table is **sparse** — a row exists only for a cell the user explicitly set, and absence
means enabled. Adding the `Events` category therefore creates no rows and requires no backfill:
every existing user begins with Events → In-app **on** and Events → Email **on**, matching the
opt-out default of every other category. This is what satisfies FR-021 with zero migration.

What changes is only *who reads it*: four producers that never consulted the Email channel now
do.

| Producer | Category read | Channel | Status before |
|---|---|---|---|
| `PartyService.PostRequestAsync` | `InvitesAndRoster` | `Email` | Ungated |
| `PartyRosterService` (nudge) | `InvitesAndRoster` | `Email` | Ungated |
| `PartyNewsService.NotifyCrewAsync` | `TeamNews` | `Email` | Ungated |
| `MarketRequestService.DeliverInviteAsync` | `InvitesAndRoster` | `Email` | Ungated |
| `EventService.NotifyCancellationAsync` | `Events` | `Email` | Ungated (and no in-app at all) |

The **In-app** channel needs no work in any of these: `NotificationService.CreateAsync` and
`CreateManyAsync` already filter on it centrally for every producer (research D2).

### `Notification`

Shape unchanged. Gains rows of the new `Type` with the payload below.

---

## Notification payload

### `EventCancelledPayload`

Serialized into `Notification.Payload` and mirrored client-side.

| Field | Type | Notes |
|---|---|---|
| `eventId` | `Guid` | Drives the row's link target, `/events/{eventId}` |
| `eventName` | `string` | Rendered in the row title |

Deliberately minimal, and deliberately **not** reusing `PartyPayload` — a cancellation concerns
an event, not a party, and carries no team or party context. A cancellation caused by a team
sign-up still refers to the event.

**Dedupe key**: `event-cancelled:{eventId}` prefix, so `CreateManyAsync` produces
`event-cancelled:{eventId}:{userId}` per recipient. This makes a re-run idempotent — the same
guarantee the party and market fan-outs already rely on.

---

## Recipient resolution (behavioural, no schema impact)

### Cancellation recipients

Unchanged in membership, changed in identity key. Today the set is built from individual
sign-ups plus admins of signed-up teams and de-duplicated **by email address**
(`EventService.cs:388-392`). The projection must now also carry `UserId` so the same set can
drive the in-app fan-out.

De-duplication moves from email to `UserId`. This is strictly more correct: one person who is
both an individual participant and an admin of a participating team receives exactly one email
and exactly one notification, regardless of address casing.

### Culture on recipient projections

Four existing `.Select` projections gain `PreferredLanguage`:

| Projection | File |
|---|---|
| Team members for a party request | `Services/Parties/PartyService.cs` |
| Party crew for a news post | `Services/Parties/PartyNewsService.cs` |
| Event participants + team admins | `Services/Events/EventService.cs` |
| Market invite target | `Services/Marketplace/MarketRequestService.cs` |

One additional column on a query that already runs — no new round trip, no entity
materialization (research D3). The value is collapsed through
`SupportedLanguages.ResolveOrDefault`, which handles `null`, regional tags (`de-AT` → `de`),
and unsupported languages by falling back to English.

---

## Email template inventory

Templates are files on disk, not database records. Twelve new files plus three edits:

| Template | `en/` | `de/` | `es/` |
|---|---|---|---|
| `event-cancelled.html` | New | New | New |
| `party-request.html` | New | New | New |
| `party-news.html` | New | New | New |
| `market-invite.html` | New | New | New |
| `footer.html` | Edited — legal links | Edited | Edited |

Variable contracts for the four are specified in
[contracts/email-templates.md](./contracts/email-templates.md).

`LoadTemplateAsync` falls back per **file**, not per placeholder, so the three variants of each
template must carry an identical placeholder set (FR-026a).
