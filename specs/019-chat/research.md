# Research: Chat (019)

Phase 0 for [plan.md](./plan.md). Every decision below is checked against
[the constitution](../../.specify/memory/constitution.md) and against what feature 010
(notifications) already established, since chat reuses its realtime transport.

---

## 1. Realtime transport — reuse 010's SignalR spine, push-only, per-user groups

**Decision**: Add a `ChatHub` at `/hubs/chat` modelled exactly on `NotificationHub`: `[Authorize]`
under the JWT-bearer scheme, **push-only** (no client-invokable server methods), and fanning out to
the existing **`user:{id}` group-per-user** convention rather than a group-per-conversation. To
deliver a message, the service resolves the conversation's participant user ids and pushes to each
one's own user group. The seam is `IChatRealtime` (mirroring `INotificationRealtime`), registered as
a singleton over `IHubContext<ChatHub>`.

**Rationale**: `NotificationHub`'s security property is that a connection joins **only** a group
derived from its *validated* token — never from client input — so "a client can only ever receive
its own stream" is true by construction. A group-per-conversation design would need a client-invokable
`Subscribe(conversationId)`, which reintroduces exactly the authorization decision the current design
avoids, on the transport where it is easiest to get wrong. Fanning out to per-user groups keeps that
property intact and costs one `SendAsync` per participant — trivial at Jugger roster scale (teams are
~9–30 players, groups are capped at 50 by §7). It also means chat needs **no new hub auth code**:
`ChatHub` is a near-copy of a reviewed, shipped hub.

Per constitution I ("never trust the client") and FR-022, membership is resolved server-side at
fan-out time; a stale client cannot talk itself into a stream.

**Alternatives rejected**:
- *Group-per-conversation* — fewer sends per message, but requires an authorized client-invokable
  subscribe and a rejoin-on-reconnect dance. Rejected: real security cost, negligible perf win at
  this scale.
- *Reusing `NotificationHub` itself* — conflates two features' event contracts on one connection and
  drags chat's churn through notification code. Rejected: 010's own seam exists to keep producers apart.
- *Polling* — same rationale as 010 §1 rejected it: wastes requests and lags. Chat is more
  latency-sensitive than the badge, so this is even less defensible here.

## 2. Typing indicators — a rate-limited REST endpoint, not a hub method

**Decision**: The client signals typing with `POST /api/v1/chat/conversations/{id}/typing`, debounced
client-side (fire at most once per ~3s while the composer has focus and content). The server
authorizes it like any other request, then pushes a `chatTyping` event to the *other* participants'
user groups carrying a short expiry. Nothing is persisted. The client also expires the indicator on a
timer, so a dropped typist never leaves a stuck "…".

**Rationale**: Constitution V says backend↔frontend is **primarily a REST API**, and §1's whole point
is to keep the hub push-only. A client-invokable hub method would be the only client→server socket
call in the codebase and would need its own authorization path. A debounced POST reuses the
authenticated REST pipeline, the existing rate limiter (§7), and the existing thin-controller
convention. FR-020's "expires automatically" is satisfied by expiry on both ends rather than by
tracking disconnects.

**Alternatives rejected**: (a) hub method — see above; (b) sending typing on every keystroke — a
request per keypress, rejected as abusive; (c) inferring typing from drafts — no.

## 3. Message ordering and read state — lean on UUIDv7, not client clocks

**Decision**: Order messages by their `BaseEntity.Id`. Read state is a single
`ConversationParticipant.LastReadMessageId` marker per participant, not a receipt row per message per
member. Unread for a participant = count of messages in the conversation whose `Id` is greater than
their `LastReadMessageId`, excluding their own and deleted ones. A DM read receipt is derived: my
message is *Read* when the other participant's `LastReadMessageId >= myMessage.Id`, else *Sent*.

**Rationale**: The constitution mandates `Guid.CreateVersion7()` on every entity. UUIDv7 is
**timestamp-prefixed and monotonic**, so `ORDER BY "Id"` *is* chronological order — server-assigned,
identical for every viewer, and never derived from a client clock. That satisfies FR-011 for free and
makes `Id > LastReadMessageId` a range scan on the primary key rather than a join. It also gives the
"greater than" comparison a total order across concurrent inserts, covering the simultaneous-send
edge case.

A marker also makes unread O(1) rows of state per participant instead of O(messages × members), which
is what makes FR-015 affordable on a 30-person team chat. The cost is that group "seen by N" detail
is not directly available — and the spec puts group read receipts out of scope, so nothing is lost.

**Alternatives rejected**:
- *`CreatedDate` ordering* — ties are possible within a timestamp tick, giving different viewers
  different orders. Rejected: FR-011 demands a stable total order.
- *A receipt row per message per member* — exact "seen by", but writes N rows per message and makes
  unread a `COUNT` over a huge table. Rejected: out of proportion to a feature whose spec excludes
  group receipts.
- *A client-supplied sequence number* — trusts the client to order history. Rejected outright by
  constitution I.

## 4. Auto-created team & party chats — derive membership, don't mirror it

**Decision**: For `Team` and `Party` conversations, **participation is derived from the roster**, not
stored as a duplicate list. The authorization check for a team chat *is* a `TeamMemberships` query;
for a party chat it *is* a `PartyMembers` (`Status == In`) query, which already includes marketplace
guests (`ViaMarket`). `ConversationParticipant` rows still exist for these conversations, but they
hold **only per-user state** — `LastReadMessageId`, muted, hidden — and are created lazily on first
access. For `Direct` and `Group` conversations, `ConversationParticipant` *is* the membership.

**Rationale**: FR-025 says team/party chat membership "MUST mirror the underlying roster". Two ways
to be true: maintain a copy at every roster mutation point, or make the roster the single source and
never have a copy to drift. The second is true *by construction* — there is no code path, no missed
event handler and no failed transaction that can leave someone in a team chat they were removed from.
That directly serves the constitution's security-first principle: the dangerous failure here is a
removed player still reading a team's chat, and derivation makes it unrepresentable. It also makes
FR-026 ("cannot leave independently") fall out for free — there is no membership row to delete.

The conversation itself is created idempotently (`EnsureForTeamAsync`/`EnsureForPartyAsync`) on first
access, which also satisfies FR-024's backfill requirement for existing teams/parties **without a data
migration** — the first roster member to open Chat materialises it. A migration is therefore not
needed, only the ensure-on-access path.

**Alternatives rejected**:
- *Stored participant rows synced by team/party services* — the obvious approach; rejected because
  every sync point (join, leave, remove, role change, disband, team delete, marketplace seat) is a
  chance to drift, and the failure mode is a privacy leak rather than a cosmetic bug.
- *A backfill migration creating a chat per existing team* — writes rows for teams that may never chat,
  and still needs the ensure path for teams created later. Rejected as strictly more work for less
  safety.

## 5. Link unfurl — parse our own routes, never fetch

**Decision**: At send time the server scans the message body for **JuggerHub-internal route shapes
only** and stores a resolved `(LinkKind, LinkTargetId)` on the message. At read time the card is
projected **per viewer**, re-running that viewer's own permission check; a viewer who fails it, or a
target that no longer exists, gets the plain link. Nothing is ever fetched over the network.

Real route shapes (from `frontend/apps/web/src/app/app.routes.ts` — **not** the wireframe's
illustrative `/p/…`, `/e/…`):

| Kind | Route | Resolves to |
|------|-------|-------------|
| Player | `/u/{handle}` | `PlayerProfile.Handle` |
| Team | `/t/{slug}` | `Team.Slug` |
| Event | `/events/{id}` | `Event.Id` |
| Training | `/trainings/sessions/{id}` | `TrainingSession.Id` |

**Rationale**: FR-042 forbids fetching arbitrary URLs, and this is the OWASP Top 10 concern the
feature would otherwise walk into — a classic unfurl service is an **SSRF** engine: it takes
attacker-controlled URLs and makes the server request them, reaching cloud metadata endpoints and
internal services. Pattern-matching our own routes and reading our own database means there is no
outbound request to exploit. It is not a mitigation; the capability simply never exists.

Resolving **per viewer** rather than at send time (FR-040) closes the second hole: a sender who can
see a team-only training must not be able to leak its details into a DM with an outsider by pasting a
link. Storing only `(kind, id)` — never a snapshot of the target's fields — is what makes that
enforceable, and makes FR-041's "deleted target degrades to a plain link" automatic.

**Alternatives rejected**:
- *OpenGraph/oEmbed fetch* — the general solution, and the SSRF one. Rejected under constitution I.
- *Snapshotting the target at send time* — one fewer read per message, but freezes a permission
  decision at send time and leaks to whoever the message is later read by. Rejected: violates FR-040.
- *Client-side unfurl* — the client would decide what it may see. Rejected: never trust the client.

## 6. Message search — ILIKE + unaccent, matching feature 007's convention

**Decision**: Message search uses `EF.Functions.ILike(AppDbContext.Unaccent(m.Body), Unaccent(pattern))`,
**pre-filtered to the searcher's own conversations**, paginated. No `tsvector`/GIN index in this
feature.

**Rationale**: Consistency with the established convention — `AdminUserService`, `AdminTeamService`,
`EventInvitationService` and `MarketRequestService` all search with `ILike` + `Unaccent`, and
`AppDbContext.Unaccent` exists precisely for it (feature 007). Introducing a second, different search
mechanism for one surface would fragment the codebase against constitution VI. The scoping predicate
(participant-of) runs first and is indexed, so the ILIKE only ever scans one player's own messages —
which is a small set, not the whole table.

The security property matters more than the speed here: FR-035 requires that search **never** returns
a message from a conversation the searcher isn't in, so the scope predicate is applied in the service
query itself, not as a post-filter.

**Alternatives rejected**: *Postgres full-text with a GIN index* — the right answer at large scale and
worth revisiting when message volume justifies it; rejected now as premature against a convention the
whole codebase follows. Recorded as a follow-up rather than a silent omission.

## 7. Rate limiting — new infrastructure this feature must add

**Decision**: Register ASP.NET Core's built-in `AddRateLimiter` (`Microsoft.AspNetCore.RateLimiting`,
in-framework) and apply named partitioned policies, keyed on the **authenticated user id**, to:
starting a conversation, sending a message, and the typing signal. Limits: conversation-start 10/min,
send 30/min, typing 30/min (typing is already client-debounced).

**Rationale**: FR-049a requires server-side rate limiting, and the clarification that **any player may
DM any player** is what makes it load-bearing: with open reach and no limit, one account can message
the entire community. The constitution's "lean middleware" list explicitly admits rate limiting as
core security middleware.

**This does not exist yet** — `Program.cs` registers no rate limiter today. It is new shared
infrastructure introduced by this feature. Partitioning on the authenticated user id (not IP) is
correct here because every limited endpoint is authenticated, and IP-keying would punish players
behind a shared NAT at a tournament.

**Alternatives rejected**: (a) a custom counter service — reinvents a framework primitive, rejected
under "minimal deps / use the framework"; (b) IP-keyed limits — wrong unit for authenticated abuse;
(c) no limit, relying on block — block is per-recipient and reactive, so it cannot stop a broadcast.

## 8. Scale constants

| Constant | Value | Reasoning |
|----------|-------|-----------|
| Max message length | 2 000 chars | Comfortably above a long chat message, far below a payload that would bloat a row or the inbox preview. |
| Max group members | 50 | Above the largest plausible manual group (a big team + friends); bounds fan-out cost per message (§1). Team/party chats are not subject to it — their size is the roster's business. |
| Inbox page size | 20 | Matches the existing `PaginationRequest.DefaultTake`. |
| Message page size | 30 | One screen-plus of history; keyset-paged backwards on `Id`. |
| Typing signal expiry | 5 s | Longer than the ~3 s client debounce, so a steadily-typing user's indicator never flickers. |

## 9. Pagination shape — keyset for messages, skip/take elsewhere

**Decision**: Message history uses **keyset pagination** (`WHERE Id < :before ORDER BY Id DESC LIMIT n`);
the inbox, member lists and search use the shared `PaginationRequest`/`PagedResult<T>` skip/take envelope.

**Rationale**: Constitution III says skip/take is the CRUD default but "for very large or
rapidly-changing tables, prefer keyset pagination (`WHERE Id > lastId`)" — a chat history is the
textbook case of both. It also avoids the skip/take drift where a message arriving mid-scroll shifts
every subsequent page. UUIDv7 (§3) makes `Id` a valid cursor, so no extra cursor column is needed.
The other surfaces are ordinary bounded lists and use the shared envelope for a uniform contract.

## 10. Multi-replica — Redis backplane for SignalR (DECIDED 2026-07-16)

**Decision**: Add a **Redis backplane** (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`, first-party)
behind the existing `AddSignalR()`, plus a **Redis service in docker-compose** so local matches Dev/Prod.
Both hubs — `NotificationHub` (010) and `ChatHub` (019) — get it for free, since the backplane attaches
to SignalR itself rather than to a hub.

**Why this changed**: the original plan inherited feature 010's "single App Service instance ⇒ no
backplane" reasoning and filed multi-replica as a deferred risk for the unmerged `015-hosting` branch.
The product owner has since confirmed **the deployment will run more than one replica**, so the premise
that decision rested on is simply gone. It is no longer a risk to flag; it is a requirement to build.

Without a backplane, each pod holds its own set of connections and `IHubContext.Clients.Group(...)`
only reaches the pod it ran on. Two players on different pods see a **dead conversation** — messages
persist, but never arrive. Chat's entire value proposition is the thing that breaks; only FR-023
("live is an enhancement — every value is reachable on a normal load") keeps it from being data loss.
Notifications degrade far more quietly (a stale badge), which is exactly why 010 could get away with
deferring this and 019 cannot.

**Why Redis over Azure SignalR Service**: feature 015 already runs **Postgres in-cluster** rather than
using a managed Azure database, and the constitution bans Key Vault — this codebase consistently
prefers self-hosted, portable components over managed Azure extras. An in-cluster Redis matches that
posture, keeps local/Dev/Prod identical, and costs nothing in Dev. Azure SignalR Service would be less
work to operate but would make local development structurally different from deployed, which
constitution V forbids ("differences are limited to configuration and secrets, never to architecture").

**Environment parity**: Redis is added to `docker-compose.yml` so local runs the same architecture as
deployed. The connection string arrives as configuration (`.env` locally, GitHub Environments deployed).
The backplane is registered **unconditionally** when the connection string is present and the app fails
fast when it is absent outside Development — a silently in-process backplane in a multi-replica
deployment is precisely the failure this section exists to prevent, and it would be invisible until two
users happened to land on different pods.

**Sticky sessions are still required.** A backplane fixes fan-out, not connection establishment. With
the default negotiate handshake, the negotiate response carries a connection token bound to the pod
that answered it; a follow-up request routed elsewhere fails. Options are (a) cookie affinity on the
ingress, or (b) WebSockets-only with `skipNegotiation`. **(a)** is the recommendation — `skipNegotiation`
forfeits the transport fallback that keeps SignalR working on restrictive networks. The nginx-ingress
annotation belongs on the **`015-hosting`** branch, which owns the ingress:

```yaml
nginx.ingress.kubernetes.io/affinity: "cookie"
nginx.ingress.kubernetes.io/session-cookie-name: "jugger-affinity"
nginx.ingress.kubernetes.io/session-cookie-expires: "3600"
```

Carried as **T097** so it is handed over explicitly rather than assumed.

**Alternatives rejected**: (a) *Azure SignalR Service* — see parity, above. (b) *Postgres LISTEN/NOTIFY
as a homemade backplane* — no new component, but SignalR has no such provider, so it means writing and
owning a backplane; rejected against "use the framework, minimal deps". (c) *Pin to one replica* — the
owner has ruled it out. (d) *Sticky sessions alone, no backplane* — a common misreading: affinity keeps
a client on one pod, but a message sent on pod A still never reaches a member connected to pod B. It
addresses connection establishment, not fan-out. Both are needed, for different reasons.

## 11. Multi-replica — rate limiting must be distributed (DECIDED 2026-07-16)

**Decision**: Replace the in-memory fixed-window limiter with a **Redis-backed fixed-window limiter**
(`RedisFixedWindowRateLimiter`, ~60 lines over the `StackExchange.Redis` client that the backplane
already brings in), keyed on the authenticated user id, plugged into the same
`AddRateLimiter`/`[EnableRateLimiting]` pipeline so the call sites do not change.

**Why**: this is the **second** thing multi-replica breaks, and it is much easier to miss than the
backplane because nothing looks broken. `AddRateLimiter`'s partitions live in **each pod's memory**. With
N replicas behind a round-robin ingress, a caller gets N independent buckets — the effective limit is
**N × the configured limit**. At 3 pods, `chat-start`'s 10/min is really 30/min, and the number drifts
further every time the cluster autoscales.

That is not a rounding error here. Rate limiting is load-bearing precisely *because* the product owner
chose **open DM reach** (FR-049): any player may message any other, and block is per-recipient and
reactive, so the limit is the only thing that stops one account from opening a conversation with the
entire community. A limit whose real value is "10/min × however many pods happen to be running" does not
honour FR-049a's "MUST be enforced server-side", and worse, it would read as enforced while silently
not being.

Sticky sessions do **not** rescue this either: affinity distributes *users* across pods, so each pod
still holds a partial view, and a caller who reconnects lands on a fresh bucket.

The algorithm is the standard atomic fixed window — `INCR`, and `EXPIRE` on first hit — which is why it
needs no Lua script and no third-party rate-limiting package (constitution: minimal deps).

**Fail-closed on Redis loss**: if Redis is unreachable the limiter **rejects** rather than allowing. A
rate limiter that fails open turns a cache outage into an open mass-DM window — the exact scenario
FR-049a exists to prevent. Chat is degraded for the outage; the community is not exposed.

**Alternatives rejected**: (a) *Divide the limit by replica count* — wrong the moment the autoscaler
moves, and encodes a deployment detail in application code. (b) *IP-keyed limiting at the nginx ingress*
— wrong unit (punishes a whole clubhouse behind one NAT) and unavailable to the ingress as a user
identity. (c) *A third-party distributed rate-limiting package* — a real option, but `INCR`/`EXPIRE` over
a client we already reference is smaller than a new dependency. (d) *Accept the N× drift and document it*
— rejected: the spec would then state a limit the system does not enforce.

## 13. Own-bubble contrast — DESIGN.md contradicts itself (found by measuring, 2026-07-16)

**Decision**: own message bubbles use **`brand-active` (coral-6 `#B93A17`)**, not `brand-primary`
(coral-4 `#F5623A`).

**Why**: measured in a real browser, white on coral-4 is **3.14:1**. DESIGN.md's own Do's and Don'ts
say *"Do maintain WCAG AA contrast (≥ 4.5:1 for body text)"*. A message bubble is body text — a
paragraph someone reads, not a two-word label — so 3.14:1 is not defensible. Measured across the ramp:

| Token | Hex | White text | AA (4.5:1) |
|---|---|---|---|
| `brand-primary` coral-4 | `#F5623A` | **3.14:1** | ✗ |
| `brand-primary-hover` coral-5 | `#DB4A22` | 4.19:1 | ✗ |
| **`brand-active` coral-6** | `#B93A17` | **5.71:1** | ✓ |

coral-6 is an **existing token in the same family** — no new color, no DESIGN.md amendment, and the
bubble still reads unmistakably coral. It is the smallest change that satisfies both of DESIGN.md's
own rules at once.

**The wider conflict, reported not resolved**: DESIGN.md **contradicts itself**. Its component spec
says the primary button is *"coral `brand-primary` background, white label"* — which is the same
3.14:1 that its accessibility rule forbids. So **every primary button in the app is at 3.14:1**, not
just chat's bubbles. That is a pre-existing, app-wide issue and **not this feature's to fix
unilaterally**: changing the primary button color is a brand decision affecting every screen. Raised
for the product owner. Chat only fixes its own surface, where the failure is worst because the text is
long-form.

*(Short white-on-coral labels may qualify under AA's large-text allowance at ≥18.66px bold / ≥24px;
`body-md` at 600 weight does not, so buttons are genuinely non-conforming rather than exempt.)*

**How it was caught**: not by review — by computing the contrast ratio from the *rendered* computed
styles in a headless browser. It is invisible to tests, lint and the build, and easy to wave through
in a checklist because "we used the brand token" feels like compliance.

## 12. Design conflicts with the wireframe (resolved toward DESIGN.md)

Per constitution Quality Gate 7 and CLAUDE.md, conflicts are **reported, not silently resolved**:

1. **Own-bubble color** — wireframe draws blue; DESIGN.md makes coral `brand-primary` the primary and
   reserves `blue-*` for the `info` status token, forbidding ad-hoc colors. → **Coral wins**
   (product owner confirmed). Other's bubbles use `surface-muted` (sand-2). No DESIGN.md amendment needed.
2. **Navigation** — wireframe draws Home / Teams / Events / Chat / You; the real nav (feature 008,
   `nav-model.ts`) is Home / Browse / My team / Alerts. → **Real nav wins**; Chat is appended as a
   fifth `NavId`. The wireframe's other tabs are not adopted.
3. **Link shapes** — wireframe shows `jugger.app/p/…`, `/e/…`, `/t/…`; the real routes are `/u/{handle}`,
   `/events/{id}`, `/trainings/sessions/{id}`, `/t/{slug}`. → **Real routes win** (§5).
4. **Emoji** — the wireframe's sample messages contain emoji. That is *user-authored content* and is
   unaffected by DESIGN.md's "no emoji in product UI" rule, which governs chrome. No conflict; noted
   so the next reader doesn't re-litigate it.
5. **Uppercase TEAM / PARTY tags** — DESIGN.md permits uppercase only as a styled eyebrow. The inbox
   tags use the existing eyebrow token, which is exactly that usage. No conflict.
