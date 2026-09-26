# Contract: chat push (056)

**No new endpoint.** The one API surface that changes is the notification preferences matrix, which
gains a fifth category and a statement of which cells exist. Everything else is a server-internal
seam and a payload that crosses to the device through machinery feature 055 already built.

---

## `GET /api/v1/notification-preferences` — changed response

`PreferenceCategoryDto` gains `availableChannels`. Additive: every existing field keeps its meaning
and every existing consumer keeps working.

```jsonc
{
  "categories": [
    {
      "category": "InvitesAndRoster",
      "label": "Invites & roster changes",
      "description": "Team invites, people joining or leaving",
      "channels": { "inApp": true, "email": true, "push": true },
      "availableChannels": ["InApp", "Email", "Push"]
    },
    // … TeamNews, Trainings, Events, all three channels …
    {
      "category": "Chat",
      "label": "Chat messages",
      "description": "New messages in your conversations. Chat has its own inbox and badge, so there's nothing to send in-app or by e-mail.",
      "channels": { "inApp": true, "email": true, "push": true },
      "availableChannels": ["Push"]
    }
  ],
  "alwaysOn": [ /* unchanged */ ]
}
```

| Aspect | Contract |
|---|---|
| Auth | Signed in, as before |
| Order | `Chat` is appended last in `CategoryOrder`, after `Events` |
| `channels.inApp` / `channels.email` for `Chat` | **Still present and still `true`.** They are the sparse default and mean nothing for this category. The client MUST branch on `availableChannels`, never on these values. Omitting them, or sending `false`, would both read as "switched off" rather than "not a thing" |
| `availableChannels` | Never empty. A category with no deliverable channel would be a row that cannot do anything |

### Why `channels` is not narrowed for `Chat`

Narrowing it would be the tidier-looking choice and is wrong twice over: `false` is
indistinguishable from a member's own choice to switch something off, and making the record
nullable pushes a three-state decision onto every consumer of a DTO that four other categories use
correctly today. `availableChannels` says the thing that is actually true — *these are the cells
that exist* — and leaves the value semantics alone.

---

## `PUT /api/v1/notification-preferences/{category}/{channel}` — narrowed

| Request | Response |
|---|---|
| `PUT …/Chat/Push` with `{ "enabled": false }` | `204` — stored like any other cell |
| `PUT …/Chat/InApp` with any body | **`400`** — `"That notification category isn't delivered on that channel."` |
| `PUT …/Chat/Email` with any body | **`400`** — same |
| Every existing `(category, channel)` pair | unchanged |

The refusal sits beside the existing `Enum.IsDefined` guard and reads from
`NotificationCategories.Supports`. It is the never-trust-the-client rule (Principle I) applied to a
cell the interface does not render: without it a client can store a `(Chat, Email)` row that means
nothing and that something could later read back as though it meant something.

The enum binds **by name**, so `Chat` needs no route change — the same property feature 055 relied
on when it added `Push`.

---

## Server-internal seam: what chat hands to the dispatcher

Chat calls `IPushDispatcher.DispatchAsync` **directly**, writing no `Notification` row. This is the
seam feature 055 built for exactly this caller, and its own doc comment names #309 as the reason.

```csharp
await _dispatcher.DispatchAsync(
    recipientUserIds,                    // already filtered: see FR-008..FR-014, FR-027
    new PushContent(Title, Body, Url, Tag),
    ct);
```

**`IPushFanOut` is not used and must not be.** That interface is the notification store's own narrow
seam — it takes a `NotificationType` and the in-app row's payload JSON, neither of which chat has.
Going through it would require inventing a fake notification type, which is the reversal of 019
FR-051 that this whole design exists to avoid.

| Field | Chat's value |
|---|---|
| `Title` | `Direct` → the sender's display name. Every other kind → the conversation's name, from the shared `ChatDisplayName` helper, evaluated for **this recipient** |
| `Body` | `Direct` → the message text. Every other kind → `"{sender}: {text}"`. Truncated to 120 characters with an ellipsis. Falls back to "sent you a message" when the body cannot be read, "sent an attachment" when there is no text |
| `Url` | `/chat/{conversationId}` — app-relative, built from a `Guid`, so the service worker's absolute-URL refusal can never fire |
| `Tag` | `chat:{conversationId}` — **per conversation, not per message**, which is what makes several messages collapse to one notification across passes as well as within one |

---

## What crosses to the device

**The device contract is unchanged.** `sw.js` reads exactly four fields — `title`, `body`, `url`,
`tag` — and this feature sends exactly those four. No new field, no new listener, no change to
`notificationclick`, and the 054 guard spec that forbids `fetch` / `caches` / `importScripts` is
untouched.

What *is* new is the content: `body` now carries text a member wrote. That is a change to what
leaves the platform, not to the wire format, and it is why FR-029 exists.

---

## Configuration

New section `ChatPush`, alongside the existing `WebPush` and `Retention` sections. All values have
safe built-in defaults (Principle VII), and every one of them is **identical in value, not merely in
shape, across local / Dev / Prod** (Principle V). The timing pair was briefly split so local
verification did not cost half a minute per attempt, which meant nobody ever experienced the
production timing while developing — so there is deliberately no `ChatPush` override in
`appsettings.Development.json`.

| Key | Default | Meaning |
|---|---|---|
| `ChatPush:Enabled` | `true` | Whether the worker runs. The integration suite turns it off so a timer cannot race assertions that drive the pass directly — the same reason `Retention:Enabled` exists |
| `ChatPush:QuietDelaySeconds` | `5` | FR-001. The window in which reading the message cancels the notification — this IS the presence check, so it is shortened but never set to zero |
| `ChatPush:PollIntervalSeconds` | `2` | How often a pass runs. Worst-case felt latency is the delay plus this, so it stays well under the delay. Pure latency: cheap to lower, and it changes when a notification goes out, never whether |
| `ChatPush:MaxMessageAgeMinutes` | `60` | FR-025. Older messages are marked considered and **never dispatched** |
| `ChatPush:MaxMessagesPerPass` | `500` | Principle VII: the batch is bounded. Unbounded work over a backlog is the same defect as an unbounded wait |
| `ChatPush:PassTimeoutMinutes` | `5` | Principle VII: nothing waits forever, background loops included |
| `ChatPush:PreviewLength` | `120` | FR-020 |

No secret is added. The VAPID key path from feature 055 is unchanged and untouched.
