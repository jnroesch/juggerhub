# Quickstart: Event and Party News Posts Can Be Edited and Deleted

**Feature**: 059 · **Contract**: [contracts/news-api.md](./contracts/news-api.md) · **Model**: [data-model.md](./data-model.md)

How to prove the feature works end to end. Scenario numbers map to the spec's user stories and
FRs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images after changing either side (a stale backend image fakes a broken
feature; the frontend root filesystem is read-only).

Accounts: **A** creates a team and an event (event admin, team admin). **B** is invited as the
event's co-admin. **C** joins A's team. **D** joins A's team. A forms a party for the event from
the team; C and D join the party (crew). A makes C a party co-admin. **E** is a signed-in player
with no relation to anything.

## Automated checks

```powershell
# Backend: the two new suites plus the suites whose shapes this feature widens
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~EventNewsEditDelete|FullyQualifiedName~PartyNewsEditDelete|FullyQualifiedName~TeamNewsEditDelete|FullyQualifiedName~EventTests|FullyQualifiedName~PartyTests|FullyQualifiedName~HomeTests|FullyQualifiedName~NewsPartyTests|FullyQualifiedName~NotificationTests"

# Frontend: the shared component, its four hosts, Home, the services and the catalogue guards
cd frontend; npx nx test web --watch=false --testPathPatterns="news-post|news-feed|party-news|team-detail|news-list|event.service|party.service|catalog-"
```

Then the whole suites once before the PR: `dotnet test backend/JuggerHub.slnx`,
`npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`.

## Manual scenarios (browser, German, 375px and desktop)

Playwright context `locale: 'de-DE'`, one browser context **per actor** (the 048 lesson: a second
sign-in in the same context replaces the first actor's cookie). Screenshots answer Gate 7.

1. **Event: edit someone else's post (US1, FR-001–FR-005, FR-012).** A posts *"Check-in ab
   08:00."* on the event page. B opens the post's menu → *Bearbeiten*, reads the hint (*niemand
   wird benachrichtigt*), changes it to 09:00, saves.
   **Expect**: the text updates in place, keeps its position, author and date; the meta line ends
   *· bearbeitet*. Nobody receives anything (Mailpit, Meldungen badge).
2. **Event: no controls for non-admins (FR-014).** E opens the event page. **Expect**: the news
   is readable, no menu on any post.
3. **Event: delete (US1-5/6, FR-008).** B deletes the post. **Expect**: the dialog says it
   disappears from the Event-Seite and everyone's Startseite; *Beitrag behalten* has focus;
   confirming removes it from the page, and from C's Home (C's team is signed up) after reload.
4. **Party: edit leaves Alerts alone (US2, FR-006).** A posts *"Treffen 07:00 an der Aral an der
   A7."* on the party page. D leaves the alert unread. C (party co-admin) edits it on the
   **party news page**. **Expect**: both party pages show the new text marked *bearbeitet*; D's
   Meldungen row is unchanged and still unread; D's badge did not move; no new email for D.
5. **Party: delete removes the alerts, former crew included (US2-7, FR-009).** A posts a second
   update; D declines the party afterwards (former crew, still holding the alert). A deletes the
   post from the **party page**. **Expect**: the dialog mentions the alerts and the email copies;
   after confirming, the post is gone from both party pages and Home; D's alert for it is gone and
   D's badge dropped by one (live if D has a tab open).
6. **Party: outsiders (FR-011, SC-004).** E calls `PATCH /api/v1/parties/{id}/news/{postId}` and
   a random party id: both `404 "Party not found"`. A crew member who is not an admin: `403`.
7. **Already gone (FR-020).** Two admins open the same page; one deletes a post; the other then
   saves an edit to it. **Expect**: *Diesen Beitrag gibt es nicht mehr.* appears in the news
   section and the post leaves their list; focus lands on the section heading.
8. **Home marker (US3, FR-017).** C opens Home and *Alle anzeigen* (News). **Expect**: the edited
   event and party posts carry *· bearbeitet*; unedited ones don't.
9. **Menus are not clipped (research R9).** On the party page, the party news page, the event page
   and the team page, open the menu of the **last, one-line** post. **Expect**: both items fully
   visible, not cut off by the card.
10. **Team page unchanged (FR-023).** Re-run 057's quickstart scenarios 1–3 and the delete dialog
    on the team page. **Expect**: identical behaviour and copy.
11. **375px layout (SC-006).** For each of the three new surfaces: menu, editor (hint and buttons
    wrap without clipping), dialog as a bottom sheet, meta line with *· bearbeitet*.
    `document.documentElement.scrollWidth - clientWidth <= 1` with the editor open and with the
    dialog open.
