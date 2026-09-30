# Research: Event and Party News Posts Can Be Edited and Deleted

**Feature**: 059-event-party-news-edit-delete · **Date**: 2026-09-28 · **Spec**: [spec.md](./spec.md)

Every decision below was settled by reading the code on `main` at `b3872bc` (058 merged). No
`NEEDS CLARIFICATION` remains: the three product questions were answered by the owner (spec →
Clarifications), and the rest are engineering choices recorded here. 057's research is the
parent document; where a decision carries over unchanged it is cited, not repeated.

---

## R1 — Party-news Alerts rows quote nothing, so an edit does not touch them

**Decision**: Editing a party post runs **one** `ExecuteUpdate` on the post and nothing else. No
`ReplacePayloadAsync`, no transaction.

**Rationale**: `PartyNewsService.NotifyCrewAsync` writes the payload
`{ partyId, eventId, teamSlug, eventName, teamName }` and no body or excerpt. The Alerts row
renders `alerts.row.partyNewsTitle` (*"Party update · {team} @ {event}"*) and
`partyNewsSupporting` (*"New update for the {event} party"*); the push composer uses the event
name and the fixed `partyNews.body` sentence. So nothing an admin wrote is in a party-news row
or a party-news push. GH #368's premise ("each holding a copy of the post") is wrong. Its
question "refresh the excerpt on edit?" has nothing to act on (spec → Context).

Rewriting the rows anyway (same payload, new `ModifiedDate`) would be a write with no effect,
and it would move the audit field on rows nobody changed: the 056 defect class that 057's R4
exists to avoid.

**Alternatives considered**: *Add an excerpt to the party-news payload so it matches team news*.
That is a change to what posting does, with its own design questions (the email already carries
the excerpt; the push must not). Out of scope.

---

## R2 — A party post's Alerts rows are found by the dedupe prefix `party-news:{postId}`

**Decision**: `DeleteManyAsync(NotificationType.PartyNews, $"party-news:{postId}")`, 057's
producer-agnostic operation, unchanged. The prefix is spelled **once** in `PartyNewsService`
(`NewsDedupePrefix(postId)`), and both `NotifyCrewAsync` and the delete use it, the 057
`TeamNewsService` shape.

**Rationale**: `NotifyCrewAsync` passes `dedupeKeyPrefix: $"party-news:{postId}"` to
`CreateManyAsync`, which writes `{prefix}:{recipient}` per row. The prefix therefore reaches
**everyone the post ever reached**, including crew who have since declined or been removed. A
roster join would silently miss them (057 R1). The `Type == PartyNews` filter plus the anchored
`prefix + ":"` keep team news' `news:{postId}:` rows out of reach. Party news, team news and
058's `join-request:` rows do not share a prefix.

---

## R3 — Party delete is one transaction; event edit, event delete and party edit are single statements

**Decision**:

| Operation | Statements | Transaction |
|-----------|-----------|-------------|
| Event edit | `ExecuteUpdate(Body, EditedDate, ModifiedDate) WHERE Id ∧ EventId` | none needed |
| Event delete | `ExecuteDelete WHERE Id ∧ EventId` | none needed |
| Party edit | `ExecuteUpdate(Body, EditedDate, ModifiedDate) WHERE Id ∧ PartyId` | none needed |
| Party delete | `ExecuteDelete post WHERE Id ∧ PartyId` → 0 rows ⇒ not found → `DeleteManyAsync(PartyNews, prefix)` | **one**, through `CreateExecutionStrategy` |

After a party delete commits, `RefreshUnreadBadgesAsync(recipients)` runs, best effort.

**Rationale**:

- **Party delete is 057's R3 case exactly**: without a transaction, a failure between the two
  statements leaves the post gone and its alerts announcing it, with no way back (a second delete
  answers "not found"). The notification statement runs only after the post statement matched a
  row **of the addressed party**. That is what keeps another party's post id away from that
  party's rows (FR-013).
- **The other three are one statement each**. The provider's execution strategy
  (`EnableRetryOnFailure`) already retries a lone statement. Each writes fixed values, so a
  replay converges; a replayed delete whose first attempt committed answers 0 rows, i.e. "not
  found", which the client treats as "already gone" (FR-020), the same as 057's second delete.
  Wrapping a single statement in a user transaction adds nothing.
- **`ModifiedDate` MUST be set on both edit paths** (constitution Principle III / Gate 2):
  `ExecuteUpdate` bypasses `AuditFieldsInterceptor`. It is the likeliest gate failure, and a test
  asserts it moves on each.
- Side effects after commit, never inside the retried delegate (the `MutateMembershipAsync` rule).

---

## R4 — `EditedDate` on both entities, one migration

**Decision**: `EventNewsPost.EditedDate` and `PartyNewsPost.EditedDate` (`DateTime?`,
`timestamp with time zone`, null = never edited), set only by the edit path and only when the
trimmed text changed. **One** migration, `AddEventAndPartyNewsEditedDate`, two `AddColumn`s, no
default, no backfill.

**Rationale**: 057 R4 unchanged: `ModifiedDate` moves on writes nobody would call an edit, and
null on every existing row *is* FR-018. One migration because the two columns ship together and
neither is useful alone. **Generate it with a build** (the 056 lesson: `--no-build` emitted an
empty migration), then read it.

---

## R5 — Who may act, and in what order the server decides

**Decision**:

| | Event news | Party news |
|---|---|---|
| Resolve | `EventAdminGuard.ResolveAsync(eventId, caller)` | `PartyGuard.ResolveAsync(partyId, caller)` |
| Not found (same as reading) | no such event → **404 "Event not found"** | no such party, or caller neither crew nor party admin → **404 "Party not found"** |
| Refused | not an event admin → **403** | crew but not a party admin → **403** |
| Post | `Id == postId ∧ EventId == eventId`, absent → **404 "News post not found"** | `Id == postId ∧ PartyId == partyId`, absent → **404 "News post not found"** |

Any current admin, any post (owner decision). Authorship is not consulted anywhere.

**Rationale**:

- The not-found step mirrors what **reading** answers today, so edit and delete add no oracle.
  Event news is readable by every signed-in player (`GetFeedAsync` 404s only an unknown event),
  so a non-admin gets 403, exactly as posting does. Party news is private to the crew
  (`ListAsync` 404s anyone not `In`), so an outsider, including a member of the party's team who
  is not in the crew, gets the **same** 404 as reading, not the 403 posting gives (SC-004).
  Posting's own 403-for-outsiders is pre-existing and out of scope; party ids are UUIDv7.
- **Role before post**, as in 057 R6, so a plain member cannot probe post existence.
- `IsPartyAdmin` is `MyRole == Admin`. Declining a party demotes to `Member`
  (`PartyRosterService.cs:183-184`), so "party admin" and "in the crew" coincide in practice.
  The check mirrors posting's `IsPartyAdmin` rather than inventing a stricter rule.
- **Neither the event's nor the party's state restricts edit/delete** (FR-015). The event page
  stays readable after cancellation and the end, so a wrong post there stays harmful. Posting
  already ignores event state server-side; only the event page hides its composer on a cancelled
  event, which is unchanged.

**Result types**: each service gets its own per-operation statuses, the 057 shape
(`EventNewsEditStatus`/`EventNewsDeleteStatus`, `PartyNewsEditStatus`/`PartyNewsDeleteStatus`).
Party news does **not** reuse `PartyOutcome`: the enum is shared with `MarketController`, and
both controllers' `Fail` switches end in a `_ =>` arm that would silently turn a new
`PostNotFound` member into a 400 "Request failed" anywhere it was not mapped.

---

## R6 — Contract: `PATCH`/`DELETE` beside the existing `GET`/`POST`

**Decision**: see [contracts/news-api.md](./contracts/news-api.md).

- `PATCH /api/v1/events/{id}/news/{postId}` → 200 `EventNewsDto` / 400 / 403 / 404 / 404
- `DELETE /api/v1/events/{id}/news/{postId}` → 204 / 403 / 404 / 404
- `PATCH /api/v1/parties/{id}/news/{postId}` → 200 `PartyNewsDto` / 400 / 403 / 404 / 404
- `DELETE /api/v1/parties/{id}/news/{postId}` → 204 / 403 / 404 / 404

Request bodies are new records with the **posting** attributes of their kind:
`EditEventNewsRequest([Required, MaxLength(2000)] string Body)` (as `CreateNewsRequest`) and
`EditPartyNewsRequest([Required, MaxLength(1000)] string Body)` (as `CreatePartyNewsRequest`).
`[Required]` rejects empty and whitespace-only strings under `[ApiController]` (400), the same
way posting does. The service trims before comparing and storing (057 R5: unchanged text is a
server-side no-op returning 200 with the current post).

`EventNewsDto` gains `EditedDate` and `PartyNewsDto` gains `EditedDate`, both appended after
`CreatedDate`. Neither gains a permission flag: under "any admin, any post" the client already
knows (`viewer.isAdmin` on the event, `myRole === 'Admin'` on the party), 057 R10.

---

## R7 — Home already renders the marker for every source; only the projections change

**Decision**: `HomeService.LoadNewsAsync` projects `n.EditedDate` for event and party posts
instead of `(DateTime?)null`. Nothing else on Home changes.

**Rationale**: 057 put `EditedDate` on the shared `HomeNewsDto` and made the news list render
`· edited` whenever it is set, regardless of source (`news-list.component.html:14`). 057's R8
anticipated this: "when those sources gain editing, their projections change and nothing else
does". Two tests written for 057 describe event/party items as *never* edited. Their assertions
stay true (they seed never-edited posts), but their names and comments go stale and are updated:
`Home_marks_an_edited_team_post_and_never_event_news` and the `news-list.component.spec.ts`
header.

---

## R8 — One shared `jh-news-post` component; the team page moves onto it (owner decision)

**Decision**: `shared/news-post/news-post.component.{ts,html,css,spec.ts}` renders **one**
post's controls: the ellipsis menu, the in-place editor and the delete confirmation. The host
keeps what differs, which is the list, each item's frame, and the body and meta line, passed in
by content projection. Used by the team page, the event page's news feed, the party page and
the party's news page.

**Shape**:

- Inputs: `postId`, `body` (seeds the editor), `canManage`, `maxLength` (2000 event, 1000
  team/party), `editHint` and `deleteBody` (translation keys, the only copy that differs), and
  two **functions**, `save(postId, body) → Observable<T>` and `remove(postId) →
  Observable<unknown>`. The component stays free of any one service, and the host keeps owning
  its list. The class is generic in `T`, so the `saved` output is typed from the host's `save`
  binding.
- Outputs: `saved(T)` (host replaces the item) and `removed({ gone })` (host drops the item;
  `gone` means it was already deleted elsewhere, so the host shows *"This post no longer
  exists."*, FR-020). The host moves focus to its news heading after a removal, because the
  component that held focus has just been destroyed.
- **The projected display is hidden, not destroyed, while editing** (`[hidden]` on a wrapper).
  Angular's guidance is not to put `<ng-content>` inside `@if`. Content is created once and
  re-projection under a conditional is fragile, and hiding keeps it simple.
- **The editing state** (which post's editor is open, and the typed draft) lives in
  `NewsPostEditing`, **provided by each page component** (`providers: [NewsPostEditing]`), not in
  the component and not in root. *Refined during implementation*: the team page replaces its whole
  content with a spinner whenever it reloads, and approving a join request reloads it. 057 kept the
  editor and its draft on the page for exactly that reason ("approving a join request reloads too,
  mid-edit"), and a per-instance state would lose the typed text. Page-scoped, the state survives
  the rebuild and dies with the page. A root service would leak an open edit onto the next page.
  The team page still resets it on a team switch, as 057 did. The lock, "while one post is being
  edited, no menu opens" (057), counts only a post that is **on screen**: instances register on
  init and unregister on destroy, so a post deleted elsewhere cannot lock the page for good.
- **One menu open at a time** needs no shared state: each instance closes its menu on a document
  click **outside its own menu wrapper**. Checking the generic `[data-news-menu]` selector (057's
  page-level form) would keep menu A open when menu B's trigger is clicked, since that click is
  inside *a* menu.
- **Escape** is handled per instance (close own menu → focus its trigger; dismiss own dialog).
  Tab is trapped inside the open dialog (057's `trapTab`, moved).
- Every `data-testid` 057 introduced is kept verbatim (`news-edit`, `news-edit-input`,
  `news-delete-confirm`, `data-news-menu-trigger="{id}"`, …). **`team-detail.component.spec.ts`'s
  twelve news tests are the regression net for FR-023/SC-007** and must pass unchanged.

  > **Amended by GH #392**: the delete dialog is now the shared `jh-confirm-dialog` (feature 064),
  > which owns the focus, Tab and Escape rules this component used to carry by hand. Its four ids
  > became the shared dialog's — `news-delete-confirm` → `confirm-dialog`, `news-delete-keep` →
  > `confirm-dialog-keep`, `news-delete-submit` → `confirm-dialog-confirm`, `news-delete-error` →
  > `confirm-dialog-error` — and the twelve tests pass with only those selectors changed. The menu's
  > and the editor's ids are untouched.

**Rationale**: the four surfaces must not drift (FR-022). The logic being shared is behaviour,
not layout: focus order, the no-op on unchanged text, status-branching errors, and the lock.
The 052 precedent extracted exactly this kind of rule-bearing piece (the invite search's
`@switch`), and 053 copied a row that was pure presentation. The body and meta line differ per
surface and stay with the host, which is why they are projected.

**Alternatives considered**: *copy 057's markup three more times* (owner declined); *a shared
component for event + party only* (owner declined: two implementations). *A native `<dialog>`
with `showModal()`* would escape any ancestor's stacking context for free, but jsdom does not
implement `showModal`, and every test that opens the dialog would need a polyfill. The
positioned overlay is verified safe instead (R9).

---

## R9 — The menu needs an unclipped card; the dialog does not

**Decision**: every card that contains a post menu gets the card's existing `overflowVisible`
input: the team page's News card, the event page's News card, and **each post card** on the two
party pages (they render one `jhCard` per post).

**Rationale**: `jh-card` clips by default (`overflow: hidden`, `card.component.css`), and the menu
is `position: absolute` below its trigger. A party post card is one or two lines tall, so its
menu (two 44px items) would be cut off at the card edge. The roster's menu already solves this
with `overflowVisible` ("safe only without the accent strip"; none of these cards has one). The
team page's News card lacks it today, so 057's menu on a *short last* post may already clip. The
browser walk checks that case, and setting the input fixes it either way.

The delete dialog is `position: fixed`. Overflow does not clip a fixed box. Only an ancestor
with a `transform`, `filter` or `contain` would re-anchor it, and the only card rule with one is
`.jh-card--interactive:hover` (a lift), which none of these cards uses. `jhRise` (a transform
animation) is used on the dashboard and browse pages, **not** on any of the four news surfaces
(grep). Verified by reading; confirmed in the walk.

---

## R10 — Copy: the shared strings move to a `news` namespace

**Decision**: 057's team-page strings that are not about the team move, with their values
unchanged, from `teams.detail.news*` to a shared `news.*` namespace, in all three catalogues in
one commit (`catalog-parity` fails otherwise):

`news.manage`, `news.edit`, `news.delete`, `news.edited`, `news.editLabel`, `news.saving`,
`news.saveFailed`, `news.gone`, `news.deleteTitle`, `news.deleteKeep`, `news.deleteConfirm`,
`news.deleting`, `news.deleteFailed`.

The two strings that name who is affected become per-kind keys:

| Key | English | German | Spanish |
|-----|---------|--------|---------|
| `news.editHint.team` | Saving won't notify the team again. *(057, moved)* | *(057, moved)* | *(057, moved)* |
| `news.editHint.event` | Saving won't notify anyone. | Beim Speichern wird niemand benachrichtigt. | Al guardar no se avisa a nadie. |
| `news.editHint.party` | Saving won't notify the crew again. | Beim Speichern wird die Crew nicht noch einmal benachrichtigt. | Al guardar no se vuelve a avisar al equipo. |
| `news.deleteBody.team` | *(057, moved)* | *(057, moved)* | *(057, moved)* |
| `news.deleteBody.event` | It disappears from the event page and from everyone's Home. | Er verschwindet von der Event-Seite und von der Startseite aller. | Desaparece de la página del evento y de la página de inicio de todos. |
| `news.deleteBody.party` | It disappears for the whole crew, together with its alerts. Anyone who got it by email keeps their copy. | Er verschwindet für die ganze Crew, samt den Meldungen dazu. Wer ihn per E-Mail bekommen hat, behält diese Kopie. | Desaparece para todo el equipo, junto con sus avisos. Quien la recibió por correo conserva su copia. |

**Terminology** (read from the catalogues): the post is *post* / *Beitrag* (masculine, hence
*Er*) / *novedad* (feminine, hence *editada*, *la recibió*). A party's members are the *crew* /
*Crew* / *equipo* (`parties.news.hint`). German writes *Event-Seite*
(`events.contactsManage.subtitle`) and *Startseite* for Home. Alerts are *alerts* / *Meldungen* /
*avisos*. Errors keep the house *We couldn't…* / *Wir konnten…* / *No pudimos…* voice that
`catalog-punctuation.spec.ts` enforces. German `–`, never `—`.

**Rationale**: keeping the team's keys under `teams.detail` while the other two pages read
`news.*` would put one component's copy in two namespaces. The English values are unchanged, so
the team spec's text assertions (*"Manage post"*, *"This post no longer exists."*, …) hold.

---

## R11 — Things fixed in passing, because this feature touches the same lines

- **`EventNewsService.PostAsync`** falls back to a hard-coded English `"An organiser"`. 037 already
  replaced that string in the feed with `MemberPlaceholder`, and it only survived in the post
  path. The post response and the edit response now come from one shared projection (057 R10's
  `Project`), so the fallback is the placeholder everywhere.
- **`EventNewsPost`'s summary** says "shown … on the public event page. Read by everyone". Since
  026 the page is signed-in only. The summary is rewritten to describe the entity as it is (057
  R11), including `EditedDate`. Same for `PartyNewsPost`.
- **Both party pages render the server's English `detail`** when posting fails
  (`err.error?.detail ?? …`), which is GH #179's defect class. Both components are being
  rewritten around the shared list item, so their post error becomes the translated
  `parties.news.error` only (the 052 rule: branch on status, never on the message). The 400 case
  that `detail` used to explain cannot come from the UI: the textarea caps at 1,000 and empty text
  is not sent.
- **The party page's news label** is a `<p class="text-eyebrow">`. It becomes an `<h2>` with the
  same classes, so there is a heading for focus to land on after a delete, as the team page has.

---

## R12 — Principle VII and rate limiting: not engaged

No outbound call is added: nothing is emailed or pushed on edit or delete (FR-005/FR-010). The
SignalR badge refresh already exists and is best-effort. On the browser hop,
`retry.interceptor.ts` retries only `GET`/`HEAD`, so the four new mutations are **never**
auto-retried. FR-021's "try again" is a press, with the typed text kept. The one multi-step
write (party delete) runs through the execution strategy (R3). No rate limit is added: posting
has none, and edit/delete send nothing outside the product (057 R7).

---

## R13 — Deliberately not done

- **Recalling party-news email** (sent) or **withdrawing a shown push** (carries no text).
- **An excerpt in party-news alerts** (R1).
- **Edit history, "edited by", audit record** (owner accepted, 057's decision carried over).
- **Live update of other people's open pages**. Only the badge moves live, on a party-post delete.
- **Posting rules**, including posting's 403 for party outsiders and the event composer's
  cancelled-event gate.
- **Event news notifications**: event news stays un-notified.

---

## R14 — Test strategy

- **Backend integration** (real API + Postgres Testcontainer):
  - `Events/EventNewsEditDeleteTests.cs` ("Events" collection): any admin edits any post
    (co-admin edits the creator's); in place, author and date kept, `editedDate` set,
    `ModifiedDate` moved (Gate 2); unchanged text is a no-op; empty / whitespace / >2000 → 400;
    non-admin 403, unknown event 404 "Event not found", another event's post 404 "News post not
    found" even for an admin of both; demoted co-admin 403; delete removes it from the feed and
    Home; second delete 404; cancelled event still editable and deletable; Home marks the edited
    event post; nothing sent (`TestEmailSender`, `FakePushDispatcher`, `FakeNotificationRealtime`
    unchanged).
  - `Parties/PartyNewsEditDeleteTests.cs` ("Parties" collection, `PartyTestSupport`): any party
    admin edits any post; edit leaves every alert row **byte-identical** (payload, read state,
    `ModifiedDate`) and sends nothing; >1000 → 400; crew non-admin 403; a team member outside the
    crew and a stranger get the **same** 404 as the feed ("Party not found"); another party's
    post 404; delete removes the post, its alert rows for current **and former** crew (a member
    who declined after the post), leaves another post's rows alone, and pushes a lower unread
    count to an unread recipient only; Home marks the edited party post.
  - Rename 057's `Home_marks_an_edited_team_post_and_never_event_news`.
- **Frontend (Jest 30, `--testPathPatterns`)**:
  - `shared/news-post/news-post.component.spec.ts` (new): the behaviour in R8, against a small
    host (menu for `canManage` only, edit/cancel/unchanged/failed save/404, delete
    confirm/keep/failed/404, focus targets, the lock across two instances, one open menu,
    Escape).
  - `team-detail.component.spec.ts`: **unchanged and green** (FR-023).
  - `features/events/event-detail/components/news-feed.component.spec.ts` (new): controls only for
    admins, marker, save replaces the item, removal drops it and shows the notice for `gone`.
  - `features/parties/party-news/party-news.component.spec.ts` (new): the same, for the party news
    page, plus the translated post error.
  - `news-list.component.spec.ts`: header corrected, plus event and party items with an
    `editedDate` show the marker.
  - `event.service` / `party.service`: the two new calls each (new specs, `HttpTestingController`).
  - Catalogue guards (`catalog-parity`, `catalog-punctuation`) as they are.
- **Browser walk** (owner's standing rule): German at 375px and desktop, with screenshots: the
  menu on the **last, shortest** post of each surface (the clipping case in R9), editor, dialog
  and marker on the event page, the party page and the party news page, the marker on Home, and
  the team page unchanged. Gate 7 is answered from those screenshots.
