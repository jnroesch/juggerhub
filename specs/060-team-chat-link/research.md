# Research: The Team Page Leads Into the Team Chat (060)

Every item here was settled by reading the code, not by assumption. File:line references are to
`main` at `6790d7b`.

## R1 — The defect: a Contact-admins thread passes for the team chat

**Finding.** Feature 027 put `TeamInquiry` threads on the same `Conversation.TeamId` column as the
team chat, and re-scoped the database's one-chat-per-team unique index to `Kind = 2`
(`AppDbContext.cs:1038-1041`). The two **service** checks that decide whether a team's chat exists
were never scoped the same way:

- `EnsureAutoChatsForAsync` (`ChatConversationService.cs:581-585`) selects the caller's teams
  `Where(id => !_db.Conversations.Any(c => c.TeamId == id))`. A team whose only conversation is an
  inquiry is skipped, so its chat is **never created**.
- `FindAutoAsync` (`:1073-1077`) returns the first conversation with `TeamId == ownerId`, **of any
  kind**. `EnsureForTeamAsync` would hand back an **inquiry thread's id** as "the team chat".

**Consequence today.** If someone contacts a team's admins before any member has opened Chat, no
team member ever gets the team's chat. Plain members can contact admins too (027 FR-001/FR-002), so
a member doing that first on a new team is enough to trigger it.

**Consequence for this feature.** A resolver built on `EnsureForTeamAsync` would return the inquiry's
id. `ChatGuard` would then admit an **admin** (admins are members of inquiry threads) and open
someone else's private thread on the team page. It would refuse a plain member with 404 on their
own team.

**Decision.** Add `c.Kind == ConversationKind.Team` to both checks. `FindAutoAsync`'s predicate
becomes `kind == Team ? (c.Kind == Team && c.TeamId == ownerId) : (c.Kind == Party && c.PartyId == ownerId)`.
The party half gains the kind test for symmetry; no other kind uses `PartyId` today, so its
behaviour is unchanged. `EnsureAutoAsync`'s race recovery calls `FindAutoAsync`, so it inherits the
fix. `ArchiveAutoAsync` is only used for parties, and `ArchiveForTeamAsync` already filters by kind
(`:1087-1088`).

**No migration.** The index is already kind-scoped, so inserting the missing team chat next to an
existing inquiry succeeds. Affected teams heal the next time a member opens Chat (FR-014).

**Alternatives rejected.** A data migration to create the missing chats would break with 019
research §4 (chats materialise on first sight) and do nothing that the next inbox load does not.

## R2 — Resolving the team chat without a second membership rule

**Constraint (FR-004, the issue).** `ChatGuard` is the only place that answers "may this player
read this conversation" (`ChatGuard.cs:30-50`). The resolver must not write its own version.

**Trap.** Calling `EnsureForTeamAsync(teamId)` *before* any check lets a non-member create a chat for
any team, and throws a foreign-key `DbUpdateException` (then rethrows → 500) for a made-up team id.
Guarding the creation with a new `TeamMemberships.Any(...)` check would be exactly the second rule
the issue forbids.

**Decision.** Reuse the inbox's own creation step, then ask the guard:

1. `EnsureAutoChatsForAsync(callerId)`: the step `GetInboxAsync` runs first
   (`ChatConversationService.cs:414`). It creates the missing chats of **the caller's own** teams
   and parties and nothing else. This is "exactly as opening Chat would" (the issue) by
   construction. A non-member's request cannot create the asked-about team's chat (FR-007), and a
   made-up id never reaches an insert.
2. `FindAutoAsync(Team, teamId)`, with R1's fix.
3. `_guard.ResolveAsync(id, callerId)`: the single membership rule (FR-004). A null result, or no
   chat at all, gives `ChatOutcome.NotFound` → `ChatHttp.Fail` → the same generic 404 body in
   every case (FR-005).

Step 1 reads the caller's memberships to decide what to *create*, as the inbox does. It never
decides access. Only step 3 does.

**Cost.** Two small `NOT EXISTS` queries per press, plus one lookup and one guard query. This runs
on a button press, not on page load.

## R3 — GET, retry and replay

**Decision: `GET /api/v1/chat/team/{teamId}`** (the issue's shape, the sibling of
`GET chat/contact/team/{teamId}`).

The browser's retry interceptor retries `GET`/`HEAD` on transient faults
(`retry.interceptor.ts:26,32`) with a 15 s per-attempt timeout (`:6,74`). A retried request is
**replay-safe**: the only write is the lazy creation, which is idempotent (R1's filtered unique
index plus `EnsureAutoAsync`'s catch-and-re-find), so every replay returns the same id. That is
the same property the inbox `GET` already relies on. Principle VII's "never retry a mutation"
covers requests whose repetition changes the outcome, and repeating this one does not.

**Not engaged.** Principle VII's integration rules: no outbound call is added. No rate-limit
policy: a member can cause at most one creation per team they belong to, and the inbox `GET` that
does the same has none.

## R4 — Response shape

**Decision.** A new `TeamChatRefDto(Guid ConversationId)`: non-null and nothing else (FR-006).

**Alternatives rejected.**
- Reusing `InquiryThreadRefDto(Guid? ConversationId)`: its null means "no thread yet", which this
  endpoint never returns. It answers 404 instead.
- Returning `ConversationSummaryDto` via `SummariseAsync`: that runs the whole inbox query with
  `Take = 100` (`ChatConversationService.cs:1218-1226`) and **excludes hidden chats**, so a player
  who hid the team chat would get 404 on their own team. It would also carry an unread count, which
  the owner declined.

## R5 — Frontend: resolve on press, never on load

**Decision.** `TeamDetailComponent.openTeamChat()` calls `ChatService.openTeamChat(teamId)` when the
button is pressed, then `router.navigate(['/chat', id])`. The team page's load is untouched
(FR-008, SC-004).

- **Busy**: a `teamChatBusy` signal disables the button and swaps its label to
  `teams.detail.openingTeamChat`, the idiom `postNews` uses (`team-detail.component.html:227`).
  It is a signal because the app is zoneless.
- **404** → `teamChatNotice = 'teams.detail.teamChatNotMember'` + `load()`. The notice renders
  **outside** the member-only card, under the header where `requestError` sits. After the reload
  the viewer is no longer a member, so the card is gone, and a notice inside it would vanish with
  it. The same shape as 058's `joinNotice` + `load()`.
- **Any other failure** → `teamChatError = 'teams.detail.teamChatFailed'`, shown **in the card**
  next to the button, and the button is enabled again. It branches on the status, never the
  server's English `detail` (GH #179).
- **A `<button>`, not a link.** The id is not known until the press. "Contact admins", its
  neighbour, is already a button that navigates (`team-detail.component.ts:89-95`).

**Alternatives rejected.**
- A `/chat/team/:teamId` route that resolves and redirects: a real link, but it moves errors into
  the chat pane, away from the page the player was on, and adds a route and a component for one
  button.
- Resolving on load for a real `<a href>`: one more request on every member page view, and the
  chat gets created by *viewing* a page. The owner declined the count that would have justified it.

## R6 — Placement (owner decision)

The team page's header action block (`team-detail.component.html:43-59`) renders for everyone
today. A plain member sees **Contact admins** there, because `canContactAdmins = !isAnon && !isAdmin`
(`team-detail.component.ts:81`). An admin gets an **empty** `w-full` block that wraps to its own
line and adds a row gap to the header.

**Decision.**
- Wrap the header block in `@if (!isMember())`. Non-members, signed-out visitors and players with a
  pending request see exactly what they see today (FR-017). Members get no header actions, and the
  admin's empty row goes away.
- The `team-tools` card (`:295-305`) holds, in order: **Team chat** (every member), **Contact
  admins** (`@if (canContactAdmins())`, which inside the member block means plain members only),
  **Invites** (admins), **Manage** (every member). All are `variant="secondary" size="sm"`
  (FR-019). The card's heading `teams.detail.teamTools` is unchanged.
- **Contact admins** calls the same `contactAdmins()` wherever it is shown (FR-018), and keeps its
  `data-testid`, since exactly one copy is ever rendered.

## R7 — Copy

Four keys under `teams.detail`, in all three catalogues in one commit (`catalog-parity.spec.ts`):

| Key | en | de | es |
|-----|----|----|----|
| `teamChat` | Team chat | Team-Chat | Chat del equipo |
| `openingTeamChat` | Opening… | Wird geöffnet… | Abriendo… |
| `teamChatFailed` | We couldn't open the team chat just now. | Wir konnten den Team-Chat gerade nicht öffnen. | No hemos podido abrir el chat del equipo ahora mismo. |
| `teamChatNotMember` | You're no longer on this team. | Du bist nicht mehr in diesem Team. | Ya no formas parte de este equipo. |

"Team-Chat" matches the chat catalogue's `kindTeam`. The failure sentence copies `requestFailed`'s
shape, and the not-a-member sentence mirrors `alreadyMember` (`Du bist schon in diesem Team.`).
