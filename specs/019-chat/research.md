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

## 10. Known risk — SignalR has no backplane, and 015 moves to AKS

**Not a decision — a flagged risk, carried to the plan's Complexity/Risk tracking.**

Feature 010 chose in-process SignalR with **no Redis/Azure SignalR backplane**, explicitly reasoning
that "the deployment target is a single Azure App Service instance (constitution V)". `Program.cs`
confirms it: a bare `builder.Services.AddSignalR()`.

The unmerged **`015-hosting`** branch moves hosting to **Azure AKS**, which amends the constitution off
App Services. On main today the single-instance assumption still holds, so chat inherits 010's
decision unchanged and this feature does **not** add a backplane.

But the exposure is not the same for the two features. If AKS ever runs **more than one replica**
without a backplane:
- 010 degrades *quietly and briefly* — a badge doesn't update until the next REST load.
- 019 degrades *visibly and centrally* — two players on different pods see a dead conversation. Chat's
  entire value proposition is the thing that breaks, and FR-023's "live is an enhancement, every value
  is reachable on load" is the only reason it isn't data loss.

**Recommendation**: before 015 scales the backend past one replica, either add a backplane behind the
existing `IChatRealtime`/`INotificationRealtime` seam (which both exist precisely so this can be done
without touching producers) or pin the backend to a single replica. This should be raised on the
015-hosting branch rather than solved here — building a backplane for a single-instance deployment
would be speculative work against a hosting design that has not landed.

## 11. Design conflicts with the wireframe (resolved toward DESIGN.md)

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
