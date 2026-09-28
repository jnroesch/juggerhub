# Quickstart: Team Details — Editable Name, Type and City, a Description, and Links

**Feature**: 061 · **Contract**: [contracts/team-details-api.md](./contracts/team-details-api.md) · **Model**: [data-model.md](./data-model.md)

This guide shows the feature working end to end. Scenario numbers map to the spec's user stories
and FRs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images. The migration `AddTeamDescriptionAndLinks` applies on backend start.

Accounts:
- **A** creates team *Rheinfuer* (City team, Köln) and is its admin.
- **B** joins it as a plain member.
- **C** joined, received alerts, then **left**.
- **J** is a signed-in player with no relation to the team.

Seed alerts before renaming. Invite someone, change B's role, post news, and let J request to
join and be declined. That gives A, B and C delivered alerts that name *Rheinfuer*. If results
are wanted, connect *Rheinfuer* to a placement of a past tournament (050).

## Automated checks

```powershell
# Backend: the new suite, plus the suites whose rows a rename rewrites
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~TeamDetails|FullyQualifiedName~TeamTests|FullyQualifiedName~TeamBrowse|FullyQualifiedName~Results"

# Frontend: settings, team page, wizard, the link-host util and the catalogue guards
cd frontend; npx nx test web --watch=false --testPathPatterns="team-settings|team-detail|team-create|link-host|catalog-"
```

Before the PR, run every whole suite once: `dotnet test backend/JuggerHub.slnx`,
`npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`.

## Manual scenarios (browser, German, 375px and desktop)

Use one Playwright context **per actor** with `locale: 'de-DE'` (the 048 lesson). The screenshots
answer Gate 7 (`checklists/ui-review.md`).

1. **Rename reaches everything (US1-1..3, FR-008..FR-010, SC-002/SC-003).**
   - A opens *Verwalten* → *Teamdetails* is the first section. A corrects the name to
     *Rheinfeuer* and saves.
   - **Expect**:
     - *Gespeichert* appears, and the address stays `/t/rheinfuer`.
     - The team page, the Browse list, A's nav and *Mein Team*, and the team chat in the inbox
       all say *Rheinfeuer*.
     - B's and **C's** *Meldungen* rows say *Rheinfeuer*, with the same read state and the same
       order.
     - B's Home "role changed" entry says *Rheinfeuer*.
     - The connected placement and its matches say *Rheinfeuer*, and the result's
       "last changed" date did not move.
     - Nothing new appeared in Mailpit.
2. **Unchanged name touches nothing (FR-011).** A saves again without changes. **Expect**: 200.
   In the database, no `Notifications` or `TournamentPlacements` row's `ModifiedDate` moved.
3. **City and type (US1-4..6, FR-002/FR-003/FR-012).**
   - A picks Düsseldorf and saves. **Expect**: the team page shows Düsseldorf, and Browse's
     city filter finds the team under Düsseldorf.
   - A switches to *Mixteam* and saves. **Expect**: no city, and the page says Mixteam.
   - A switches back to *Stadtteam* without a city and saves. **Expect**: refused with a German
     message that a city is needed, and nothing changed.
4. **Name rules (US1-7).** A enters `X` and saves. Then A enters 51 characters. **Expect**: both
   are refused in German, naming the 2–50 limit.
5. **Description (US2, FR-013/FR-014).**
   - A writes two paragraphs, including the text `**fett**` and `www.example.com`, and saves.
   - As J, open the team page. **Expect**: an *Über das Team* card at the top of the main
     column. The line break is kept. `**fett**` shows literally. The address is plain text, not
     a link.
   - A empties the field and saves. **Expect**: the card is gone (no links either).
6. **Links (US3, FR-015..FR-019).**
   - A adds *Website* → `https://rheinfeuer.de`, *Instagram* → `instagram.com/rheinfeuer`, and
     *Discord* → an invite URL, then saves.
   - **Expect**: on the page the three appear in that order, each label underlined with the host
     beside it (`rheinfeuer.de`, `instagram.com`, …). The Instagram link reads
     `https://instagram.com/rheinfeuer` in the form after saving. Clicking opens a new tab. In
     that tab, `window.opener === null` and `document.referrer === ''`.
7. **Refusals (US3-3/4, SC-005).** For each case A saves and gets a German refusal pointing at the
   row, and nothing changes:
   - `http://rheinfeuer.de`
   - `javascript:alert(1)`
   - `mailto:team@rheinfeuer.de`
   - `https://instagram.com@example.net`
   - a duplicate address
   - an empty label
   - a 31-character label

   A sixth link cannot be added: *Link hinzufügen* is disabled at five. Sending six with
   `curl` → 400 `tooManyLinks`.
8. **Homograph (FR-017).** A adds `https://іnstagram.com/x` (Cyrillic `і`). **Expect**: the page
   shows the host as `xn--nstagram-…`, not `instagram.com`.
9. **Who may edit (US1-8, FR-005).** B opens *Verwalten*. **Expect**: no *Teamdetails* section.
   As B, `PUT /api/v1/teams/rheinfuer/details` returns 403. As J it returns 404, with the same
   body as for `/teams/doesnotexist/details`.
10. **Independence (FR-007).** A toggles *Anfänger willkommen* and uploads a logo, then saves
    details. **Expect**: the flag and the logo are unchanged by the save, and the details are
    unchanged by the toggle.
11. **Wizard (US4, FR-020..FR-022, SC-007).**
    - A creates *Nordlichter Kiel*. After the logo step comes *Erzähl etwas über euer Team*, and
      the button reads *Überspringen*. A types text; the button reads *Weiter*. A continues to
      the invite step, and the team page shows the text.
    - A creates another team and presses *Überspringen*. **Expect**: no `PUT` in the network
      log; the team has no description.
    - Stop the backend at the about step and press *Weiter*. **Expect**: a German error, the
      text kept, and a secondary *Überspringen*. A retry after restart saves it.
12. **Layout (FR-024, SC-008).** At 375px in German:
    - the *Teamdetails* section with five link rows (label and address stacked, the remove
      control reachable);
    - the *Über das Team* card with a long label and a long host;
    - the wizard step.

    **Expect**: nothing cut off, no horizontal scroll.
