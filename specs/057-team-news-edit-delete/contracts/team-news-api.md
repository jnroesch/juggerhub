# Contract: editing and deleting team news

**Feature**: 057 | **Spec**: [../spec.md](../spec.md) | **Model**: [../data-model.md](../data-model.md) | **Research**: [../research.md](../research.md)

Two new endpoints on `TeamsController`, two existing response shapes widened, and three new
methods on the notification engine's interface. The route prefix is
`/api/v1/teams/{slug}/news`, beside the existing `GET` (feed) and `POST` (composer).

All four news endpoints require authentication (the controller's
`[Authorize(JwtBearer)]` and 026's fallback policy). No endpoint gets `[AllowAnonymous]` and no
rate-limit policy is added (research R7).

---

## `PATCH /api/v1/teams/{slug}/news/{postId}` — edit a post's text

| Part | Value |
|---|---|
| `slug` | Team slug, normalised server-side (`TeamSlugPolicy.Normalize`) |
| `postId` | `guid` route constraint; any other shape is a routing 404 |
| Body | `{ "body": "string" }`, `EditTeamNewsRequest([Required, MinLength(1), MaxLength(1000)] string Body)` |
| Who | **Any current admin of the team**, for **any** of its posts (FR-013) |
| Idempotent | Yes. Re-sending the same text is a no-op (FR-003) |
| Browser retry | Never (`PATCH` is outside the retry interceptor's `GET`/`HEAD` set) |

### Responses

| Status | When | Body |
|---|---|---|
| `200` | Saved, or the trimmed text equals the current text (no write, `editedDate` unchanged) | `TeamNewsDto` of the post as it now stands |
| `400` | Body missing, empty, whitespace-only, or over 1,000 characters | Validation problem details. The post is unchanged |
| `401` | Not signed in | Standard challenge |
| `403` | Signed-in **member** who is not an admin | Problem, `title: "Forbidden"`, `detail: "Only admins can edit team news."` |
| `404` | Unknown team **or** caller not a member | Problem, `title: "Team not found"` (identical to the feed's own 404, no membership oracle) |
| `404` | The post does not exist **in this team**: deleted, never existed, or belongs to another team | Problem, `title: "News post not found"`, `detail: "That post doesn't exist, or was deleted."` |

**Evaluation order**: team/membership → admin → post (research R6). A plain member always
gets 403, whether or not the post exists.

**Side effects on a real change** (one transaction, research R3):

- `Body` = the trimmed text. `EditedDate` = now. `ModifiedDate` = now.
- Every `TeamNews` Alerts row written for the post (`DedupeKey` starts with `news:{postId}:`)
  gets its payload rewritten with the corrected excerpt. `IsRead`, `ReadDate` and
  `CreatedDate` are untouched.
- **Nothing is sent**: no notification row is created, no email, no push, no SignalR event
  (FR-005, FR-006).

### `200` body

```json
{
  "id": "0199a7c2-3d41-7b10-9e2f-5c7d1a0b4e21",
  "authorDisplayName": "Anna Berg",
  "authorHandle": "anna",
  "authorRole": "Admin",
  "createdDate": "2026-09-27T19:02:11.482Z",
  "editedDate": "2026-09-28T08:15:40.107Z",
  "body": "Training moves to Friday 19:00."
}
```

---

## `DELETE /api/v1/teams/{slug}/news/{postId}` — delete a post

| Part | Value |
|---|---|
| Who | **Any current admin of the team**, for **any** of its posts (FR-013) |
| Browser retry | Never (outside the `GET`/`HEAD` set) |

### Responses

| Status | When | Body |
|---|---|---|
| `204` | Deleted | — |
| `401` | Not signed in | Standard challenge |
| `403` | Signed-in member who is not an admin | Problem, `detail: "Only admins can delete team news."` |
| `404` | Unknown team or caller not a member | Problem, `title: "Team not found"` |
| `404` | The post does not exist in this team, including one already deleted | Problem, `title: "News post not found"` |

A second `DELETE` of the same post answers `404`, not `204`. The client treats that 404 as
"already gone" and removes the post from view (FR-019).

**Side effects** (one transaction, research R3):

- The post row is deleted (hard delete).
- Every `TeamNews` Alerts row written for the post is deleted, for current and former members
  alike (FR-009).
- **After commit, best effort**: each recipient who lost an **unread** row gets their current
  unread count pushed over the existing notifications hub (`unreadCount` event), so an open tab's
  badge drops without a reload. A failed push is logged and changes nothing else.
- Nothing is sent otherwise: no notification, email or push (FR-010).

---

## Widened responses

### `TeamNewsDto`: `GET /teams/{slug}/news` items, `POST /teams/{slug}/news` (201), `PATCH` (200)

Adds `id` (string, UUID) and `editedDate` (ISO date-time or `null`). Existing fields are
unchanged. `POST` returns `editedDate: null`.

### `HomeNewsDto`: `GET /home` (`news`), `GET /home/news` items

Adds `editedDate` (ISO date-time or `null`). It is always `null` for `source: "event"` and
`source: "party"`.

Additive JSON fields. The Angular models gain them in the same change, and no other client
exists.

---

## Internal contract: `INotificationService` additions

These are producer-agnostic (party news and event news will reuse them). `dedupeKeyPrefix` is
the **same string the producer passed to `CreateManyAsync`**, for example `news:{postId}`. The
methods match `DedupeKey` values starting with `dedupeKeyPrefix + ":"`, which is exactly how
`CreateManyAsync` composed them.

```csharp
/// Rewrites the payload of every notification of `type` written under `dedupeKeyPrefix`.
/// Read state, inbox order and every timestamp but ModifiedDate are untouched. Pushes
/// nothing, so it is safe inside the caller's transaction. Returns the number of rows changed.
Task<int> ReplacePayloadAsync(NotificationType type, string dedupeKeyPrefix, object payload, CancellationToken ct = default);

/// Deletes every notification of `type` written under `dedupeKeyPrefix`. Pushes nothing (safe
/// inside the caller's transaction). Returns the distinct recipients who lost an UNREAD row,
/// so the caller can refresh their badges after committing.
Task<IReadOnlyCollection<Guid>> DeleteManyAsync(NotificationType type, string dedupeKeyPrefix, CancellationToken ct = default);

/// Best-effort: pushes each recipient's current unread count to their open clients. Never
/// throws for a delivery problem. Call only after the change it reflects has committed.
Task RefreshUnreadBadgesAsync(IReadOnlyCollection<Guid> recipientUserIds, CancellationToken ct = default);
```

**Naming**: no method is called `Push…` (the 055 rule, since `PushAsync` in the same class
means SignalR while "push" in the product means web push).

## Internal contract: `ITeamNewsService` additions

```csharp
Task<TeamNewsEditResult> EditAsync(string slug, Guid postId, Guid actorUserId, string body, CancellationToken ct = default);
Task<TeamNewsDeleteStatus> DeleteAsync(string slug, Guid postId, Guid actorUserId, CancellationToken ct = default);

public enum TeamNewsEditStatus   { Updated, NotFoundOrNotMember, Forbidden, PostNotFound }
public enum TeamNewsDeleteStatus { Deleted, NotFoundOrNotMember, Forbidden, PostNotFound }
public sealed record TeamNewsEditResult(TeamNewsEditStatus Status, TeamNewsDto? Post);
```

This follows the existing per-operation shape (`TeamNewsPostStatus`/`TeamNewsPostResult`,
`DeleteTeamStatus`, `RevokeStatus`). The controller maps each status to the tables above.
