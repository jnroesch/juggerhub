# Research & Decisions: In-App Notification System

## 1. Real-time transport — ASP.NET Core SignalR (in-process)

**Decision**: Use SignalR hosted in the same API process, mapped at `/hubs/notifications`. No
Redis/Azure SignalR backplane in this release.

**Rationale**: SignalR ships in the ASP.NET Core framework (no third-party dependency, satisfies
"pin major, minimal deps"), speaks WebSockets with automatic fallback, and integrates with the
existing JWT auth. The deployment target is a single Azure App Service instance (constitution V —
env parity, no Key Vault, single-instance today), so no backplane is needed for cross-node fan-out.
If the app later scales out, a backplane can be added behind the same `INotificationRealtime`
abstraction without touching producers.

**Alternatives rejected**: (a) Polling — simpler but wastes requests and lags the badge; the
product owner chose real-time. (b) Raw WebSockets — reinvents groups/reconnect/auth that SignalR
provides. (c) Server-Sent Events — one-directional and less battle-tested in this stack.

## 2. Hub authentication & per-user isolation

**Decision**: The hub is `[Authorize]` under the default JWT-bearer scheme. The JWT is already read
from the httpOnly cookie by `JwtBearerEvents.OnMessageReceived`; for the initial negotiate/WebSocket
handshake the browser sends the cookie automatically on same-origin, so no query-string access
token is required. On connect, the hub adds the connection to a group named `user:{sub}` derived
from the authenticated principal — never from client input. All pushes target that group.

**Rationale**: Never-trust-the-client (constitution I). A client cannot name another user's group;
the server derives it from the validated token. Anonymous connections are rejected (FR-008).

**Note**: WebSocket upgrade requests do not always carry custom headers, but cookies are sent on
same-origin WS handshakes — so cookie-based JWT works. If a future cross-origin setup breaks this,
fall back to SignalR's `accessTokenFactory` + `OnMessageReceived` query-string read for
`/hubs/*` paths only.

## 3. Push abstraction for testability

**Decision**: Introduce `INotificationRealtime` with `PushCreatedAsync(recipientId, dto)` and
`PushUnreadCountAsync(recipientId, count)`, implemented over `IHubContext<NotificationHub>`.
`NotificationService` depends on the abstraction.

**Rationale**: Keeps `NotificationService` unit/integration-testable without a live socket, and
lets producers stay ignorant of transport. Thin controllers / service-centric (constitution II).

## 4. Fan-out & non-blocking delivery

**Decision**: Producers call `INotificationService.CreateAsync(...)` (or a batch `CreateManyAsync`
for news fan-out) **after** their own action has committed. Notification creation failures are
caught and logged; they never roll back or fail the originating action (FR-016). Persist the
notification row(s) first (durable), then push over SignalR (best-effort — an offline recipient
still gets it on next REST load).

**Rationale**: The originating action (invite issued, role changed, news posted) is the source of
truth and must not be held hostage to notification delivery. Realtime is an enhancement over a
durable store.

**Implementation note**: Fan-out for news is bounded by roster size (tens–hundreds); a single
`AddRange` + `SaveChanges` is fine. If rosters ever grow large this can move to a background queue
behind the same service method — no contract change.

## 5. Idempotency / duplicate suppression

**Decision**: Optional `DedupeKey` with a partial unique index `(RecipientUserId, DedupeKey)`.
Invite notifications use `invite:{invitationId}`. On unique-violation the create is treated as
already-done (same pattern as `TeamInvitationService.IsUniqueViolation`). Role-change and news
notifications either omit the key (each event is distinct) or key on the source row id.

**Rationale**: FR-017 — avoid spamming identical unread notifications when a producer fires twice.

## 6. Invite inline actions — reuse, don't reimplement

**Decision**: Inline Accept/Decline call the existing `POST /invitations/{token}/accept|decline`
endpoints (authoritative, idempotent, already reconcile expired/revoked). The notification's
`resolved` flag is computed at read time by joining the invite's live status, so out-of-band
resolution (via the invite screen or email link) is reflected without a stored duplicate state
(US2 AC-5, FR-012).

**Rationale**: Single source of truth for invite semantics; no divergence between the invite screen
and the notification row.

## 7. Team-news posting (new, admin-only)

**Decision**: Add `TeamNewsService.PostAsync(slug, actorUserId, body)` gated by
`TeamMembershipGuard` requiring admin. Reuses the existing `TeamNewsPost` entity (no schema
change). Body validated (non-empty, ≤ existing 1000-char max). Endpoint `POST
/api/v1/teams/{slug}/news`.

**Rationale**: The team-news notification producer needs a real trigger; only feed *reading*
existed. Admin-only because it fans out to the whole roster. This is intentional, documented
drift into feature 005-team-space (recorded in spec Assumptions).

## 8. Unread badge delivery to the shell

**Decision**: A single app-scoped `NotificationService` (Angular `providedIn: 'root'`) exposes an
`unreadCount` signal. It seeds from `GET /notifications/unread-count` on init and updates from
SignalR pushes; the top-nav and bottom-nav bind the badge to that signal. Mark-read calls update
the signal optimistically and reconcile from the server response.

**Rationale**: One source of truth for the badge across both nav bars (which already share
`nav-model`), consistent with the existing signal-based Angular style.
