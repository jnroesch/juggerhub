# Quickstart: Team News Posts Can Be Edited and Deleted

**Feature**: 057 · **Contract**: [contracts/team-news-api.md](./contracts/team-news-api.md) · **Model**: [data-model.md](./data-model.md)

How to prove the feature works end to end. The scenario numbers map to the spec's user
stories and FRs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images after changing either side. A stale backend image makes a
backend-dependent behaviour look broken (the 048 walk lesson), and the frontend container's
root filesystem is read-only, so copying a fresh `dist` in no longer works.

You need three accounts: **A** (creates the team → admin), **B** (joins, then is made admin),
**C** (joins as a plain member). Plus **D**, who joins and later leaves (the former-member
cases).

## Automated checks

```powershell
# Backend: the new suite plus the suites whose shapes this feature widens
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~TeamNewsEditDelete|FullyQualifiedName~NotificationTests|FullyQualifiedName~HomeTests|FullyQualifiedName~NewsPartyTests|FullyQualifiedName~PostErasureReadPath|FullyQualifiedName~EmailEncodingTests"

# Frontend: the touched components and the catalogue guards (Jest 30: the flag is plural)
cd frontend; npx nx test web --watch=false --testPathPatterns="team-detail|news-list|team.service|catalog-"
```

Then the whole suites once before the PR: `dotnet test backend/JuggerHub.slnx` and
`npx nx test web --watch=false`, plus `npx nx lint web` and a production build
(`npx nx build web`).

## Manual scenarios (browser, German, 375px and desktop)

Set the browser to German (a Playwright context with `locale: 'de-DE'` does it without touching
settings) and walk each at **375px** and at desktop width. Take screenshots: Gate 7 is answered
from them, not from the markup.

1. **Edit, as the author (US1, FR-001–FR-005).** A posts *"Training moves to Thursday 19:00."*.
   On the team page, A opens the post's menu → *Bearbeiten*, changes Thursday → Friday, reads
   the hint (*saving doesn't notify the team again*), saves.
   **Expect**: the text updates in place, keeps its position and date, and the meta line ends
   *· bearbeitet*. C's Meldungen (Alerts) badge doesn't change, and no new email reaches C in
   Mailpit.
2. **Edit someone else's post (FR-013).** B (admin) edits A's post.
   **Expect**: allowed. The post still reads as A's, marked *bearbeitet*.
3. **Cancel and no-op (FR-003).** Open the editor and cancel. Open it again and save without
   changing anything. **Expect**: no marker appears on a post that was never edited, and the
   network panel shows no `PATCH` for either.
4. **Refused edit keeps the text (FR-020).** With the editor open, stop the backend
   (`docker compose stop backend`) and save. **Expect**: a *Wir konnten …* line appears, the
   typed text is still in the field, and the editor stays open. Start the backend again and
   save: it works.
5. **Alerts follow the edit (US3, FR-006).** Before scenario 1, C leaves the alert unread and D
   leaves the team. After the edit, **expect**: C's Alerts row shows *Friday*, is still unread,
   and is still in the same place. D's row (D is no longer a member) shows *Friday* too.
6. **Home shows the marker (FR-016).** C opens Home and Home → *Alle anzeigen* (News).
   **Expect**: the edited team post carries *· bearbeitet*. Event and party news items never
   do.
7. **Delete (US2, FR-007–FR-009).** A opens the post's menu → *Löschen*. **Expect**: the dialog
   opens with focus on *Behalten* (Keep), and its text says the post disappears for the team
   and that emailed copies stay. Confirm. **Expect**: the post is gone from the team page, from
   C's Home and from C's and D's Alerts inboxes, and C's unread badge drops **without a
   reload** (C's tab open during the delete).
8. **Already gone (FR-019).** A and B both have the team page open. B deletes the post. A then
   edits it, or deletes it. **Expect**: A sees a short notice in the News card that the post no
   longer exists, and the post leaves A's list. Nothing else breaks.
9. **Members see no controls (FR-014).** C opens the team page. **Expect**: no menu on any
   post. A direct `PATCH` as C answers **403**.
10. **Keyboard.** Tab to a post's menu button, Enter, arrow or Tab to *Löschen*, Enter; Escape
    closes the dialog; Escape closes an open menu. **Expect**: focus is visible throughout and
    never lands behind the dialog's scrim.

## API spot checks (optional)

```powershell
# As a plain member → 403; as a non-member or with a bogus slug → 404 "Team not found"
curl -i -X PATCH "http://localhost:3000/api/v1/teams/<slug>/news/<postId>" -H "Content-Type: application/json" -b cookies.txt -d '{"body":"x"}'

# A post id that belongs to ANOTHER team, under this team's slug, as an admin of both → 404 "News post not found"
curl -i -X DELETE "http://localhost:3000/api/v1/teams/<slug>/news/<otherTeamsPostId>" -b cookies.txt
```
