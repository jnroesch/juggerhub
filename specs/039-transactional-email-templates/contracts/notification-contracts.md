# Contract: Notification Type, Category & Gating

**Feature**: 039-transactional-email-templates | **Date**: 2026-08-01

Client-facing contracts for the new notification type and preference category, plus the
server-side gating contract the four producers must satisfy.

**No endpoint is added or changed.** The existing notification and preference endpoints carry
the new values through their existing shapes.

---

## NC-1. New notification type

Enums serialize as their **names** (global `JsonStringEnumConverter`), so the client sees
`"EventCancelled"`.

### API shape (existing `GET /api/v1/notifications`)

```json
{
  "id": "0198...",
  "type": "EventCancelled",
  "createdDate": "2026-08-01T10:15:00Z",
  "isRead": false,
  "actorDisplayName": "Mira Kessler",
  "resolved": false,
  "payload": {
    "eventId": "0198c4f2-...",
    "eventName": "Hamburg Autumn Open"
  }
}
```

| Rule | Statement |
|---|---|
| NC-1.1 | `type` is `"EventCancelled"`. |
| NC-1.2 | `payload` carries exactly `eventId` and `eventName`. |
| NC-1.3 | `resolved` is always `false` — the type carries no inline actions. |
| NC-1.4 | `actorDisplayName` is the cancelling organiser when known, else `null`. |
| NC-1.5 | The row links to `/events/{eventId}`; the cancelled event page stays viewable. |

### Client type additions

```ts
// notification.models.ts
export type NotificationType = … | 'EventCancelled';

export interface EventCancelledPayload {
  eventId: string;
  eventName: string;
}

export function isEventCancelled(n: AppNotification):
  n is AppNotification & { type: 'EventCancelled'; payload: EventCancelledPayload };
```

`EventCancelledPayload` joins the `NotificationPayload` union.

### Renderer contract

| Element | Behaviour |
|---|---|
| Icon | Falls to the existing `@default` arm (generic bell) unless a dedicated case is added; the `@default` guarantees no broken row. |
| Icon colour | `bg-surface-secondary-soft text-secondary` — the existing default branch. |
| Title | `alerts.row.eventCancelledTitle` with `{ event }` |
| Supporting | `alerts.row.eventCancelledSupporting` |
| Link | `/events/{eventId}` |
| Inline actions | None |

New i18n keys in `en`, `de`, and `es`. **Both** the `title` and `supporting` computeds must be
extended — an unhandled type falls through to `alerts.row.fallbackTitle` and an empty
supporting line, which renders as a visibly broken row even though the icon degrades safely.

---

## NC-2. New preference category

### API shape (existing `GET /api/v1/notification-preferences`)

The matrix gains a fourth entry in `categories`:

```json
{
  "category": "Events",
  "label": "Events",
  "description": "Changes to events you signed up for",
  "channels": { "inApp": true, "email": true }
}
```

| Rule | Statement |
|---|---|
| NC-2.1 | `category` is `"Events"`. |
| NC-2.2 | Both channels default to `true` for every existing user — preferences are sparse, so no stored row means enabled. |
| NC-2.3 | `label` and `description` are server-owned and localized to the request language (en/de/es), like every other category. |
| NC-2.4 | The category appears in the server's category ordering, so both layouts render it without a client-side list. |
| NC-2.5 | Toggling uses the existing cell endpoint — no new route. |

### Client type addition

```ts
// notification-preferences.models.ts
export type NotificationCategoryId = 'InvitesAndRoster' | 'TeamNews' | 'Trainings' | 'Events';
```

This union member is the **only** frontend change the preferences screen needs: rows are
rendered from the server-supplied `categories` array with no hardcoded list.

### Copy (draft, pending #77 native review)

| Lang | Label | Description |
|---|---|---|
| en | Events | Changes to events you signed up for |
| de | Veranstaltungen | Änderungen an Events, für die du angemeldet bist |
| es | Eventos | Cambios en los eventos a los que te apuntaste |

---

## NC-3. Email gating contract

Every one of the four producers MUST satisfy this before sending.

| Rule | Statement |
|---|---|
| NC-3.1 | The recipient set is filtered through `GetEnabledRecipientsAsync(userIds, category, NotificationChannel.Email)` **before** any send call. |
| NC-3.2 | Filtering is one batched call per fan-out, never a per-recipient check inside the loop. |
| NC-3.3 | An empty filtered set short-circuits — no address lookup, no send. |
| NC-3.4 | The gate is wrapped so a preference failure never fails the originating action, matching the existing team-news treatment. |
| NC-3.5 | Gating the Email channel MUST NOT alter the in-app fan-out, which `NotificationService` already gates on the In-app channel independently. |

### Producer → category map

| Producer | Category | Notes |
|---|---|---|
| `PartyService.PostRequestAsync` | `InvitesAndRoster` | Initial party request |
| `PartyRosterService` nudge path | `InvitesAndRoster` | **Second call site — easily missed** |
| `PartyNewsService.NotifyCrewAsync` | `TeamNews` | |
| `MarketRequestService.DeliverInviteAsync` | `InvitesAndRoster` | Single recipient — still uses the batch call for one id |
| `EventService.NotifyCancellationAsync` | `Events` | New category |

Categories are derived from the existing `NotificationCategories.For` mapping so the email
gate and the in-app gate can never disagree about which category a message belongs to.

---

## NC-4. Backward compatibility

| Rule | Statement |
|---|---|
| NC-4.1 | Both enum members are **appended**; no persisted integer changes meaning. |
| NC-4.2 | No migration runs. No `NotificationPreference` row is created, altered, or deleted. |
| NC-4.3 | A client built before this feature receives `"EventCancelled"` rows it does not know; the row component's `@default` icon arm prevents a render break, though title/supporting would be empty until the client updates. Server and client ship together, so this is a resilience property, not a supported state. |
| NC-4.4 | Users who previously received these four emails unconditionally may now receive fewer — this is the intended correction, not a regression. Nobody receives *more* mail than before. |
