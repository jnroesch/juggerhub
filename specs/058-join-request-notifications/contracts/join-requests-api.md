# Contract: Join-request endpoints (existing routes, new behaviour)

**Feature**: 058 · Base: `/api/v1/teams` · All routes `[Authorize(JwtBearer)]` (unchanged)

No route is added or removed. Status codes keep their meaning; what changes is **who is told**,
**how often a request may be sent**, and that **an answer happens at most once**.

---

## `POST /teams/{slug}/join-requests` — ask to join

| Outcome | Status | Side effects |
|---------|--------|--------------|
| Created | `204` | request stored; **every current admin notified** (in-app / push / email, each by their own *Invites & roster* setting) — best-effort, never fails the request |
| Already waiting | `204` | **nothing** (no second announcement, FR-003) |
| Already a member | `409` | nothing |
| Unknown team | `404` | nothing |
| **Over the limit** (NEW) | `429` | **nothing stored, nobody notified** |

**Rate limit** (NEW): policy `join-request`, 10 per authenticated user per fixed clock hour, across
all teams. The `429` is **our own fail-closed limit**: clients MUST NOT retry it automatically
(Principle VII); the browser retry interceptor already skips `429`. The client maps the status —
never the body — to a translated message.

Only this route is limited. Withdrawing and answering are not.

---

## `DELETE /teams/{slug}/join-requests/mine` — withdraw

| Outcome | Status | Side effects |
|---------|--------|--------------|
| Withdrawn | `204` | request deleted **and every admin alert about it removed, in one transaction**; unread badges refreshed after commit |
| Nothing waiting (incl. answered a moment ago) | `204` | nothing (idempotent, as today) |
| Unknown team | `404` | nothing |

The delete is **conditional on `Pending`** — a withdrawal racing an answer never deletes the
answered row (research R5). Nobody is notified.

---

## `GET /teams/{slug}/join-requests` — the admin queue

Unchanged shape (`PagedResult<JoinRequestDto>`, admin-only, `403`/`404` as today). **Now lists only
waiting requests** (research R3): a banned player's request and a request from someone who is
already a member no longer appear. Order unchanged (arrival, then id).

---

## `POST /teams/{slug}/join-requests/{requestId}/approve` and `…/decline`

| Outcome | Status | Side effects |
|---------|--------|--------------|
| Answered | `204` | approve: status + membership in one transaction; decline: status. Then **the player is told** (in-app / push / email by their settings), naming the team only |
| Not waiting — answered by another admin, withdrawn, player banned, player already a member | `404` `title: "Request not found"` | **nothing** changes, nobody is notified |
| Not an admin | `403` | nothing |
| Unknown team / not a member | `404` (team) | nothing |

**Exactly once**: the answer is a conditional claim; of two simultaneous answers, one gets `204` and
the other `404` (research R4). Clients distinguish the two `404`s by the problem `title`, never by
`detail` (English).

---

## Internal interfaces (not HTTP)

```csharp
// ITeamJoinRequestService — NEW member
/// A player joined the team by another route (an invitation). End their waiting request exactly as
/// a withdrawal would: request deleted, admins' alerts removed, nobody notified (FR-020).
/// Best-effort for the caller: never throws for a notification problem.
Task EndForMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default);

// IPushFanOut — one optional parameter added (feature 055 seam)
Task FanOutAsync(
    IReadOnlyCollection<Guid> recipientUserIds,
    NotificationType type,
    string payloadJson,
    string? dedupeKey,
    Guid? actorUserId = null,          // NEW — resolved to a display name once per fan-out
    CancellationToken ct = default);

// IPushContentComposer — one optional parameter added
PushContent Compose(NotificationType type, string payloadJson, string culture, string tag,
    string? actorName = null);         // NEW — the actor's current name, or null
```

`TeamInvitationService.AcceptAsync` calls `EndForMemberAsync` after a `Joined` or `AlreadyMember`
outcome.
