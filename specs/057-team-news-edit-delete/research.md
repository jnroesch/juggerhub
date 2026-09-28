# Research: Team News Posts Can Be Edited and Deleted

**Feature**: 057-team-news-edit-delete · **Date**: 2026-09-28 · **Spec**: [spec.md](./spec.md)

Every decision below was settled by reading the code on `main` at `6eba124`. No
`NEEDS CLARIFICATION` remains: the two product questions the issue left open were answered
by the owner (spec → Clarifications), and the rest are engineering choices recorded here.

---

## R1 — A post's Alerts rows are found by the dedupe-key prefix, not by the roster

**Decision**: Select `Notifications` where `Type == TeamNews` and
`DedupeKey` starts with `news:{postId}:`.

**Rationale**: `TeamNewsService.PostAsync` fans out with
`CreateManyAsync(..., dedupeKeyPrefix: $"news:{post.Id}")`, and `CreateManyAsync` writes every
row's key as `$"{prefix}:{recipientId}"` (`NotificationService.cs:148`). So the prefix names
exactly the rows one post produced, for every recipient it ever reached. That includes
**members who have since left the team**, whom FR-006 and FR-009 name explicitly and whom
a roster-based match would miss. The `Type` filter is belt and braces: party news uses
`party-news:{postId}`, which an anchored prefix already cannot match.

**Alternatives considered**:

- *Current roster × exact key* — can use the `(RecipientUserId, DedupeKey)` unique index, but
  silently misses former members. That's the failure FR-009 exists to prevent: a member
  who left keeps the wrong-team post's text in their inbox.
- *`payload->>'newsPostId'`* — the payload carries the id too, but a jsonb path predicate has
  no EF translation here and needs raw SQL for the same full scan.
- *A foreign key from `Notification` to the post* — 010 references sources **inside the
  payload, not by FK**, on purpose (`Notification.cs` remarks: "deleting a source degrades the
  row gracefully … instead of cascading"). An FK would give the generic engine a nullable
  column per producer and a migration on the busiest table for one feature.
- *An index for the prefix search* (`varchar_pattern_ops` on `DedupeKey`) — declined for now.
  The query runs only when an admin edits or deletes a post, and a sequential scan of this
  table stays in the low milliseconds at the product's scale. An index would instead tax
  **every** notification insert across all nine producers. Recorded as a residual: the table
  has **no retention sweep** (only refresh tokens and push subscriptions are swept), so it
  only grows. If it ever makes this scan slow, the index is a one-line migration.

---

## R2 — The notification store owns the row mutations

**Decision**: Add three methods to `INotificationService`:

| Method | Does | Realtime |
|--------|------|----------|
| `ReplacePayloadAsync(type, dedupeKeyPrefix, payload, ct)` | `ExecuteUpdate` of every row written under the prefix: `Payload` + `ModifiedDate` only | none |
| `DeleteManyAsync(type, dedupeKeyPrefix, ct)` | Reads the distinct recipients whose **unread** rows match, then `ExecuteDelete` of every matching row; returns those recipients | none |
| `RefreshUnreadBadgesAsync(recipientUserIds, ct)` | Best-effort: pushes each recipient's current unread count over the existing SignalR channel | yes |

The prefix argument is the **same string the producer passed to `CreateManyAsync`**
(`news:{postId}`); the methods append the `:` themselves, mirroring how `CreateManyAsync` joins
prefix and recipient. The API is symmetric by construction.

**Rationale**:

- The payload is serialized with `NotificationService`'s private `PayloadJson` options
  (web defaults, camelCase), and the Angular client reads the payload without remapping. A
  second serializer in `TeamNewsService` is exactly how a casing mismatch would ship: the
  row would still render, just with an empty excerpt.
- Realtime unread counts already have one owner (`PushUnreadCountAsync`, private). Exposing a
  batch of it keeps the SignalR channel in one place.
- The methods are producer-agnostic, so the party-news and event-news follow-ups reuse them
  unchanged (`party-news:{postId}` already follows the shape).
- **Naming trap (055)**: `NotificationService.PushAsync` already means *SignalR*, and 055 ruled
  that nothing new is called `Push…` unqualified. Hence `RefreshUnreadBadgesAsync`, which
  names the effect (the bell badges converge), not the transport.
- **`ReplacePayloadAsync` and `DeleteManyAsync` do no realtime work** so they can run inside
  the caller's transaction (R3). Their XML docs say so, because every other method on this
  interface does push.

**Alternatives considered**: `TeamNewsService` touching `_db.Notifications` directly (duplicates
the serializer and the badge push); one `DeleteManyAsync` that also pushes (it would push
before commit, see R3).

---

## R3 — The post and its Alerts rows change in one transaction; the badge refresh follows the commit

**Decision**: Both mutations run through the connection-resiliency execution strategy
(constitution Principle VII, the `TeamService.MutateMembershipAsync` shape):

```text
strategy.ExecuteAsync:
  BEGIN
  edit:   UPDATE post (Body, EditedDate, ModifiedDate) WHERE Id = @post AND TeamId = @team
          → 0 rows ⇒ post not found (return, no commit)
          UPDATE notifications SET Payload, ModifiedDate WHERE Type = TeamNews AND key LIKE 'news:@post:%'
  delete: DELETE post WHERE Id = @post AND TeamId = @team
          → 0 rows ⇒ post not found (return, no commit)
          SELECT DISTINCT unread recipients …; DELETE notifications WHERE …
  COMMIT
after commit (delete only): RefreshUnreadBadgesAsync(recipients)   -- best effort
```

**Rationale**:

- **FR-009 is load-bearing for the wrong-team case** (SC-003: the text appears *nowhere*).
  Without a transaction, a failure between the two statements leaves the post gone and its
  Alerts rows still quoting it. There is then no way back: re-deleting answers "not found",
  because the post is gone. In one transaction the admin sees an error and nothing has
  changed, so trying again works.
- The same holds for an edit: a post saying *Friday* while every inbox still says *Thursday*,
  with the admin told it saved, is the state FR-006 forbids.
- **Every statement is `ExecuteUpdate`/`ExecuteDelete` with fixed values**, so a replayed
  delegate converges on the same end state (constitution: "all state mutation inside the
  retried delegate, verified replay-safe"). No change-tracker entity is involved, so there is
  nothing to `Clear()` between attempts, and an `UPDATE … WHERE Id AND TeamId` that matches 0
  rows is an answer ("gone"), not the `DbUpdateConcurrencyException` a tracked save would
  throw.
- **Team scoping is enforced by ordering**: the notification statement runs only after the
  post statement matched a row **of the caller's team**. A post id from another team matches
  nothing, so it can never reach that team's Alerts rows (FR-012).
- **Lock order is identical in both operations** (post row, then notification rows), so a
  concurrent edit and delete of the same post serialize on the post row instead of
  deadlocking. The loser sees 0 rows and answers "not found" (spec edge case).
- **Side effects after commit** (the codebase rule in `MutateMembershipAsync`: "a replayed
  delegate would send them twice"). A badge pushed before commit could show a count that a
  rollback then contradicts.

**Alternatives considered**: *best-effort after the post write* (the `PostAsync` fan-out
style) leaves an unrepairable half state, as above. *Ordering tricks without a transaction*
(delete rows first, then the post) converge only if the admin retries, and still show the
inconsistent state until they do.

---

## R4 — "Edited" is its own nullable column, `EditedDate`

**Decision**: `TeamNewsPost.EditedDate` (`DateTime?`, `timestamp with time zone`, null =
never edited), set to the edit time by the edit path only. It is exposed on `TeamNewsDto` and
`HomeNewsDto`. One migration, one nullable column, **no backfill**.

**Rationale**: `ModifiedDate` is an audit field that moves on *any* write, and this codebase has
already shipped a `ModifiedDate` that moves on rows nobody edited (056:
`ChatMessages.ModifiedDate` moves when push consideration is recorded, documented "so it
doesn't read as a defect"). "Edited" must mean *a person changed the text* (FR-004, FR-017).
Null on every existing row **is** FR-017, with no data step.

**Alternatives considered**: `ModifiedDate > CreatedDate` (fragile, see above; it also
equates any future data fix with an edit); `bool IsEdited` (drops *when*, for no saving); an
edit-history table (the spec keeps no history).

---

## R5 — Unchanged text is a server-side no-op

**Decision**: The service compares the trimmed new text with the stored text (ordinal). If
they are equal it writes nothing and returns the current post (200). Only a real change sets
`EditedDate` and rewrites the Alerts rows.

**Rationale**: FR-003. The client also skips the request when nothing changed, but the rule
lives where it can't be bypassed. Comparing the *trimmed* text means a stray space doesn't
mark a post edited.

---

## R6 — Contract: `PATCH` and `DELETE /teams/{slug}/news/{postId}`, with two different not-founds

**Decision**: see [contracts/team-news-api.md](./contracts/team-news-api.md). The evaluation
order is the one `PostAsync` already uses, extended by one step:

1. Resolve the caller's role on the team (`TeamMembershipGuard`). Unknown team or non-member
   ⇒ **404 "Team not found"** (no membership oracle; identical to reading the feed, FR-011).
2. Not an admin ⇒ **403**.
3. The post, **scoped to that team** (`Id == postId AND TeamId == team`), absent ⇒ **404 "News
   post not found"** (FR-012: another team's post id is indistinguishable from a missing one).

**Rationale**: checking the role *before* the post means a plain member can't probe post
existence. Members can read every post id in the feed anyway, so this is consistency, not
secrecy. The two 404s carry different `title`s for API consumers. The client treats both the
same way ("that post is gone"), since both mean the post can't be acted on from this page.

**Request body**: `EditTeamNewsRequest([Required, MinLength(1), MaxLength(1000)] string Body)`,
the same attributes as `PostTeamNewsRequest`. `[Required]` already rejects whitespace-only
strings under `[ApiController]` model validation (400), exactly as posting does
(`NotificationTests` covers `""` → 400 for post). The service trims before storing.

**Why `PATCH`, not `PUT`**: the issue's own proposal, and the post has one editable field out
of several. The body replaces the text and nothing else.

---

## R7 — Principle VII: not engaged as an integration; the browser rule holds by construction

**Decision**: No resilience code is added.

**Rationale**: No outbound call is added. Email and push are *not* sent on edit or delete, and
the SignalR badge push already exists and is best-effort. On the browser hop,
`retry.interceptor.ts` retries only `SAFE_METHODS = ['GET', 'HEAD']`, so the new `PATCH` and
`DELETE` are **never** auto-retried, which is the rule ("never retry a mutation on the browser
hop"). The spec's "try again" (FR-020) is a **press**, with the typed text kept. Wrapping
either call in retry or backoff would be review-rejectable.

**Rate limiting**: none added. Posting (which fans out email) carries no rate-limit policy
today. Edit and delete send nothing outside the product and touch at most one post's
recipients, so they open no new amplification path. They sit behind the same admin role as
posting.

---

## R8 — Home's news item gains `EditedDate`, set only for team posts

**Decision**: `HomeNewsDto` gains `DateTime? EditedDate`. `HomeProjections.NewsRaw` carries
it. The team source projects `n.EditedDate`, and the event and party sources project
`(DateTime?)null`. `HomeNewsMerge` passes it through. The news-list component renders the
marker when it is non-null.

**Rationale**: The issue says "Home's news module and the team page both render
`TeamNewsDto`"; they don't. Home renders its own `HomeNewsDto`, shared by three sources
(spec → Context). A nullable date on the shared item is the smallest contract that lets
FR-016 hold while event and party news *structurally* never show the marker, because their
projections write null. When those sources gain editing, their projections change and nothing
else does.

---

## R9 — The interface: an inline editor, a per-post menu, and the page's own confirmation dialog

**Decisions** (DESIGN.md governs; Gate 7 checks them):

- **Per-post `ellipsis` menu, admins only**, beside each post. It follows the roster rows on the
  same card column (same trigger, same menu surface), with the accessible wiring of
  `profile-quick-actions`: `aria-haspopup="menu"`, `aria-expanded`, `role="menu"` /
  `role="menuitem"`, a visible focus ring on the items. It closes on Escape (the page's
  existing `document:keydown.escape` listener) and on an outside click. Items: **Edit**,
  **Delete** (the latter in the danger text colour the roster's *Remove* uses).
- **Editing happens in place**: the post's text becomes a textarea holding the current text.
  Below it sit a caption hint (*"Saving won't notify the team again."*, FR-005) and **Cancel**
  (`ghost`) / **Save** (`secondary`). *Save is secondary on purpose*: the composer's **Post
  news** is the page's coral CTA and stays visible while an edit is open, so a coral Save would
  make two (DESIGN.md: one per view). The 053 precedent made its Accept secondary for the same
  reason. The textarea takes focus on open (`afterNextRender`, the zoneless-safe hook, per the
  #344 lesson). Save is disabled while the text is empty or a save is in flight.
- **Not a modal for editing**: a bottom sheet with a textarea puts the software keyboard over
  a `fixed` sheet on iOS, and editing in place keeps the post in its context.
- **Delete confirmation reuses the page's modal** (the join/withdraw dialog: scrim
  `bg-surface-inverse/40`, bottom sheet on phones, centred on `sm+`, `role="dialog"`,
  `aria-modal`). The destructive button is `jhButton variant="danger"`. **Initial focus goes to
  *Keep post***, so Enter on arrival can't delete. The copy states the two facts FR-008 requires.
- **Errors are translated and chosen by status**, never the server's English `detail` (#179;
  the 052 rule "branch on status, never on the message"). A 404 from either call means *that
  post is gone*: it leaves the list, and a short notice appears inside the News card, where the
  admin is looking (the page-level error line sits at the top of the page, a screen away at
  375px). Any other failure keeps the editor open with the typed text (FR-020) or keeps the
  dialog open, with a *We couldn't …* line. That is the one error voice
  `catalog-punctuation.spec.ts` enforces.
- **`track n.id`** replaces `track $index` in the feed. With rows that can vanish, index
  tracking would move the open editor or menu onto the neighbouring post.
- **Post bodies render `whitespace-pre-line` + `break-words`**, as party news already does
  (`party-news.component.html:28`). The editor shows the author's line breaks, so a feed that
  collapses them would disagree with the editor about the same post. `break-words` keeps a
  long URL inside the narrower flex column the menu creates. This is a visible change to
  existing posts that contain line breaks, recorded in the PR.
- **The marker** is caption text appended to the existing meta line
  (*"Anna · 27 Sep 2026 · edited"*), in the same `text-muted` step, on the team page and in
  Home's news list. Sentence case holds: it is a lower-case continuation of the meta line, as
  the middot joint implies.

**Terminology** (read from the catalogues): English *post*; German *Beitrag* (the composer
says *News posten*, and a single post reads naturally as *Beitrag*); Spanish *novedad*
(`postNews` is *Publicar novedad*, so the marker is the feminine *editada*). The Alerts inbox
is *Alerts* / *Meldungen* / *Avisos* (its page title in each catalogue).

---

## R10 — `TeamNewsDto` gains `Id` and `EditedDate`, and no permission flags

**Decision**: `TeamNewsDto(Guid Id, string AuthorDisplayName, string? AuthorHandle, TeamRole
AuthorRole, DateTime CreatedDate, DateTime? EditedDate, string Body)`. The feed and the edit
response share one projection (`Project(IQueryable<TeamNewsPost>)`), so the shape is built in
one place. `PostAsync`'s hand-built DTO adds `post.Id` and `EditedDate: null`.

**Rationale**: FR-018 needs the id. Under the owner's rule (any admin, any post) the client
already knows everything a `CanEdit` flag would say (`isAdmin()`), so a per-post flag would
only be a second place the rule could drift. The server remains the boundary (FR-011).

---

## R11 — The stale entity comment is wrong twice

`TeamNewsPost.cs:4-7` says creating and editing posts is "a later iteration" and that the feed
is "seeded in Development". The composer shipped with 010, editing and deleting ship now,
and the Development news seeder no longer exists (rebuilding dev seed data is GH #72). The
summary is rewritten to describe the entity as it is, including what `EditedDate` means and
why it is not `ModifiedDate` (R4).

---

## R12 — Deliberately not done

- **No live update of other people's open pages**: the team page never received new posts live
  either. Spec → Assumptions.
- **No recall of email** (sent) or **push** (carries no post text; a shown notification can't be
  withdrawn, the 056 residual).
- **No edit history, no "edited by", no audit record.** The marker says *edited*, and the owner
  accepted that it doesn't say by whom (spec → Clarifications).
- **Event and party news** keep today's behaviour. Follow-up issues carry this pattern, and R2's
  methods are already producer-agnostic.
- **The roster's own menu keeps its current wiring.** Aligning it with the news menu's ARIA
  wiring is a separate, unrequested change.

---

## R13 — Test strategy

- **Backend integration** (`backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamNewsEditDeleteTests.cs`,
  real API + Postgres Testcontainer, "Teams" collection): permissions (any admin, another
  admin's post, member 403, non-member / unknown team 404, a post under another team's slug
  404, demoted author 403), validation (empty / whitespace / >1000 → 400), no-op, order and
  dates kept, `EditedDate` set; Alerts rows refreshed with read state and order kept, including
  a former member's; **no** new notification, email (`TestEmailSender`), push
  (`FakePushDispatcher`) or live alert on edit; delete removes the post from feed and Home
  and every row including a former member's, lowers the unread count, and pushes the lower
  count to the unread recipient; Home's `editedDate` for team vs event news; `ModifiedDate`
  moves on both `ExecuteUpdate` paths (Gate 2).
- **A recording `FakeNotificationRealtime`**, registered in `JuggerHubApiFactory` the way
  `FakeChatRealtime` and `FakePushDispatcher` are, makes "no live alert on edit" and "the badge
  drops on delete" assertable. No existing test connects to the notification hub (verified),
  so replacing the SignalR implementation in tests is safe.
- **Frontend (Jest)**: `team-detail.component.spec.ts` gains a news block (menu for admins
  only, edit saves and marks, cancel and unchanged send nothing, a failed save keeps the text, a
  404 removes the post, the delete dialog confirms and removes, initial focus). A new
  `news-list.component.spec.ts` covers the marker. `team.service` covers the two calls. The
  catalogue guards (`catalog-parity`, `catalog-punctuation`) run as they are.
- **Browser walk** (owner's standing rule): German at 375px and desktop, with screenshots:
  menu, editor, dialog, marker on the team page and on Home. Gate 7 is answered from those
  screenshots.
