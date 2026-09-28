# Contract: editing and deleting event news and party news

**Feature**: 059 | **Spec**: [../spec.md](../spec.md) | **Model**: [../data-model.md](../data-model.md) | **Research**: [../research.md](../research.md)

Four new endpoints, two response shapes widened, one shared UI component. Every endpoint sits
under its controller's existing `[Authorize(JwtBearer)]` and 026's fallback policy; none is
`[AllowAnonymous]`, none gets a rate-limit policy (research R12), and the browser never retries
them (`PATCH`/`DELETE` are outside the retry interceptor's `GET`/`HEAD` set).

**Evaluation order** for all four: resolve the event/party and the caller's role → refuse →
the post, scoped to that event/party (research R5). "Unchanged text" is a no-op answered `200`
with the post as it stands.

---

## Event news — `/api/v1/events/{id}/news/{postId}`

Beside the existing `GET` (feed, any signed-in player) and `POST` (event admins).

### `PATCH` — edit a post's text

| Part | Value |
|---|---|
| `id`, `postId` | `guid` route constraints |
| Body | `{ "body": "string" }` — `EditEventNewsRequest([Required, MaxLength(2000)] string Body)` |
| Who | **Any current admin of the event**, for **any** of its posts |

| Status | When | Body |
|---|---|---|
| `200` | Saved, or trimmed text equal to the current text (no write) | `EventNewsDto` as it now stands |
| `400` | Missing, empty, whitespace-only, or over 2,000 characters | Validation problem details; post unchanged |
| `401` | Not signed in | Standard challenge |
| `403` | Signed in, not an admin of this event | `title: "Forbidden"`, `detail: "Only an event admin can edit news."` |
| `404` | No event with that id | `title: "Event not found"` (the feed's own 404) |
| `404` | No such post **in this event** (deleted, never existed, another event's) | `title: "News post not found"`, `detail: "That post doesn't exist, or was deleted."` |

Side effects on a real change: `Body` = trimmed text, `EditedDate` = now, `ModifiedDate` = now. **Nothing
is sent** — no notification row, email, push or SignalR event. The event's state (cancelled,
ended) does not matter (FR-015).

### `DELETE` — delete a post

| Status | When |
|---|---|
| `204` | Deleted |
| `401` / `403` / `404` / `404` | As for `PATCH` (`detail` for 403: `"Only an event admin can delete news."`) |

A second `DELETE` answers `404 "News post not found"`; the client treats it as "already gone".
Hard delete; nothing is sent (event news never produced alerts).

---

## Party news — `/api/v1/parties/{id}/news/{postId}`

Beside the existing `GET` (feed, crew only) and `POST` (party admins).

### `PATCH` — edit a post's text

| Part | Value |
|---|---|
| Body | `{ "body": "string" }` — `EditPartyNewsRequest([Required, MaxLength(1000)] string Body)` |
| Who | **Any current party admin**, for **any** of the party's posts |

| Status | When | Body |
|---|---|---|
| `200` | Saved, or unchanged (no write) | `PartyNewsDto` as it now stands |
| `400` | Missing, empty, whitespace-only, or over 1,000 characters | Validation problem details |
| `401` | Not signed in | Standard challenge |
| `403` | In the crew, not a party admin | `title: "Forbidden"`, `detail: "Only a party admin can edit news."` |
| `404` | No such party, **or** the caller is not in its crew (a member of the party's team who is not in the crew included) | `title: "Party not found"` — identical to the feed's 404, no oracle (SC-004) |
| `404` | No such post **in this party** | `title: "News post not found"` |

Side effects on a real change: the post's `Body`, `EditedDate`, `ModifiedDate`. **The post's
Alerts rows are not touched** (they quote no text, FR-006). Nothing is sent.

### `DELETE` — delete a post

| Status | When |
|---|---|
| `204` | Deleted |
| `401` / `403` / `404` / `404` | As for `PATCH` (`detail` for 403: `"Only a party admin can delete news."`) |

Side effects, **one transaction** (research R3):

- the post row is deleted;
- every `PartyNews` Alerts row written for it (`DedupeKey` starts with `party-news:{postId}:`) is
  deleted, for current and former crew alike (FR-009);
- **after commit, best effort**: each recipient who lost an **unread** row gets their current
  unread count pushed over the existing notifications hub (`unreadCount` event).

Nothing else is sent. Email already delivered keeps its text.

---

## Widened responses

- `EventNewsDto`: adds `editedDate` (ISO date-time or `null`). `POST` returns `null`.
- `PartyNewsDto`: adds `editedDate` (ISO date-time or `null`). `POST` returns `null`.
- `HomeNewsDto`: shape unchanged; `editedDate` is now set for edited **event** and **party**
  items too (it was always `null` for them).

Additive JSON fields; the Angular models gain them in the same change.

---

## Internal contracts

```csharp
// IEventNewsService
Task<EventNewsEditResult> EditAsync(Guid eventId, Guid postId, Guid actorUserId, string body, CancellationToken ct = default);
Task<EventNewsDeleteStatus> DeleteAsync(Guid eventId, Guid postId, Guid actorUserId, CancellationToken ct = default);
public enum EventNewsEditStatus   { Updated, EventNotFound, Forbidden, PostNotFound }
public enum EventNewsDeleteStatus { Deleted, EventNotFound, Forbidden, PostNotFound }
public sealed record EventNewsEditResult(EventNewsEditStatus Status, EventNewsDto? Post);

// IPartyNewsService
Task<PartyNewsEditResult> EditAsync(Guid partyId, Guid postId, Guid actorUserId, string body, CancellationToken ct = default);
Task<PartyNewsDeleteStatus> DeleteAsync(Guid partyId, Guid postId, Guid actorUserId, CancellationToken ct = default);
public enum PartyNewsEditStatus   { Updated, PartyNotFound, Forbidden, PostNotFound }
public enum PartyNewsDeleteStatus { Deleted, PartyNotFound, Forbidden, PostNotFound }
public sealed record PartyNewsEditResult(PartyNewsEditStatus Status, PartyNewsDto? Post);
```

`PartyOutcome` is deliberately not reused (research R5). `INotificationService` is unchanged:
057's `DeleteManyAsync` and `RefreshUnreadBadgesAsync` are used as they are.

---

## UI contract: `jh-news-post` (`shared/news-post/`)

One post's controls — ellipsis menu (admins), in-place editor, delete confirmation — for the
team page, the event page, the party page and the party news page (FR-022).

```html
<li …host's own frame…>
  <jh-news-post
    [postId]="n.id" [body]="n.body" [canManage]="isAdmin()" [maxLength]="1000"
    editHint="news.editHint.party" deleteBody="news.deleteBody.party"
    [save]="saveNews" [remove]="deleteNews"
    (saved)="replaceNews($event)" (removed)="dropNews(n.id, $event.gone)">
    <!-- projected: the host's own body + meta line (hidden, not destroyed, while editing) -->
  </jh-news-post>
</li>
```

| Input / output | Type | Meaning |
|---|---|---|
| `postId` | `string` | The post's id; also `data-news-menu-trigger="{postId}"` on the menu button |
| `body` | `string` | The current text; seeds the editor |
| `canManage` | `boolean` | Renders the menu. Convenience only — the server decides |
| `maxLength` | `number` | The editor's `maxlength` (2000 event, 1000 team/party) |
| `editHint` / `deleteBody` | translation key | The only copy that differs per kind |
| `save` | `(postId, body) => Observable<T>` | Called on Save with the trimmed text, only when it changed |
| `remove` | `(postId) => Observable<unknown>` | Called on confirm |
| `saved` | `T` | The server's post; the host replaces its item |
| `removed` | `{ gone: boolean }` | The host drops the item; `gone` ⇒ it was already deleted elsewhere (show `news.gone`); the host then focuses its news heading |

Behaviour (moved verbatim from 057): unchanged text closes the editor without a request; a
404 from either call ⇒ `removed({ gone: true })`; any other failure keeps the editor with the
typed text (`news.saveFailed`) or keeps the dialog (`news.deleteFailed`); while any post is
being edited every menu trigger is disabled (`NewsPostEditing`, provided by the page, which keeps
an open editor and its draft across a rebuild of the list); Escape closes the open
menu (focus → its trigger) or the dialog; Tab stays inside the dialog; initial dialog focus is
*Keep post*; Save is `secondary` (the page's composer keeps the one coral CTA); delete is
`danger`.
