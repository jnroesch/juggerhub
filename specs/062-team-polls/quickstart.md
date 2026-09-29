# Quickstart: Team Polls

**Feature**: 062 · **Contract**: [contracts/team-polls-api.md](./contracts/team-polls-api.md) · **Model**: [data-model.md](./data-model.md)

This guide shows the feature working end to end. Scenario numbers map to the spec's user stories,
FRs and SCs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images. The migration `AddTeamPolls` applies on backend start.

Accounts on team *Rheinfeuer*:
- **A**: admin, preferred language German. Author of the polls below.
- **D**: a second admin.
- **B**: member, German, with *Team-News* on for in-app, email and device.
- **E**: member, English, with *Team news → Email* switched **off**.
- **C**: member who will **leave** mid-way.
- **J**: a signed-in player with no relation to the team.

## Automated checks

```powershell
# Backend: the poll suites, plus every suite this touches
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~TeamPoll|FullyQualifiedName~TeamRenameRewrite|FullyQualifiedName~Push|FullyQualifiedName~NotificationCategory|FullyQualifiedName~Home|FullyQualifiedName~AccountDeletion|FullyQualifiedName~Terms|FullyQualifiedName~TemplateParity"

# Frontend: the card, the Alerts row, Needs you, the util and the catalogue guards
cd frontend; npx nx test web --watch=false --testPathPatterns="team-polls|poll-item|poll-editor|poll-close-time|notification-row|needs-you|catalog-|legal-catalog"
```

Before the PR, run every whole suite once: `dotnet test backend/JuggerHub.slnx`,
`npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`. Also run the e2e
suites that open a team page (`trainings`, `onboarding`) with
`BASE_URL=http://localhost:3000 MAILPIT_URL=http://localhost:8025`. The team page changed, and
061 showed that unit tests do not see e2e helpers break.

## Manual scenarios (browser, German, 375px and desktop)

Use one Playwright context **per actor** with `locale: 'de-DE'`. Call `waitFor()` before
asserting after a click: in the zoneless app, `isVisible()` can race the re-render (061). The
screenshots answer Gate 7 (`checklists/ui-review.md`).

1. **Start a poll (US1-1, FR-001..FR-006, SC-001).**
   - A opens `/t/rheinfeuer`, and in *Umfragen* presses *Umfrage starten*.
   - A enters "Donnerstag statt Dienstag diese Woche?" with three options, *Eine Antwort*,
     *Namen sichtbar*, *Ergebnis immer sichtbar*, and no close time, then starts the poll.
   - **Expect**: it heads the card, open, "von A · gerade eben", "0 von 6 haben geantwortet".
   - Time the flow at 375px: under a minute.
2. **Refusals are translated (FR-002..FR-005, FR-037).**
   - Try a question of 201 characters, a single option, two options "Rot"/" rot ", and a close
     time in the past.
   - **Expect**: a German sentence for each, never the server's English.
   - With 10 open polls, the 11th is refused with the cap sentence.
3. **The notice (US2-1..2, FR-026..FR-029, SC-004).**
   - B has one *Meldungen* row "Rheinfeuer fragt" with the question, and one German email in
     Mailpit linking to `/t/rheinfeuer#poll-…`.
   - E has the row and **no** email.
   - A (the author) and J have nothing.
   - On a device with notifications on, B gets "Rheinfeuer" with a fixed sentence and **no
     question**.
4. **Needs you (US2-3..5, FR-031).**
   - B's Home lists the poll under *Braucht dich*. *Antworten* lands on the poll: scrolled to it,
     heading focused.
   - After B answers, the item is gone on reload.
   - A's Home does not list it.
5. **Answer, change, withdraw (US1-2..5, FR-007..FR-010, SC-002, SC-006).**
   - B presses an option: recorded in one press, with a check and "deine Antwort", and the
     counts update.
   - B presses another option: moved, counted once.
   - B withdraws: back to unanswered.
   - On a multi-answer poll, B picks two and presses *Antwort speichern*: counted once in
     "geantwortet", with both bars up.
6. **Named poll: who has not answered (FR-014, FR-014a).**
   - A and D see "Noch nicht geantwortet: …" under the options.
   - B sees only the count.
   - Names link to profiles.
7. **Anonymous poll (US3, FR-016..FR-019, SC-003).**
   - A starts an anonymous poll, and B, E and D answer.
   - **Expect**:
     - every member sees the notice "Niemand sieht, wer was gewählt hat" before answering;
     - there are no names under options and no not-answered line for A or D.
   - In DevTools, check the JSON of `GET …/polls?state=open` for A, D and B: no other voter's
     handle or name.
   - There is no edit control for anonymity, and a hand-crafted `PUT` carrying `isAnonymous` is
     ignored.
8. **Results after answering (US1-9, FR-012a, SC-003a).**
   - On a poll started with *Ergebnis erst nach deiner Antwort*, before answering B sees the
     options and "3 von 6 haben geantwortet" but no bars or counts, and neither does A before
     answering.
   - After answering, the results appear. After withdrawing, they are hidden again.
   - In DevTools: no `count` values in the response.
9. **Closing (US4, FR-020..FR-022, SC-005).**
   - A poll set to close two minutes ahead, once that time passes: B's press is refused with
     "Diese Umfrage ist beendet", and the final result shows.
   - D closes another poll early through the menu, after confirming in the bottom-sheet dialog.
     It moves to the closed list and cannot be reopened.
   - A moves a third poll's close time later after answers exist: the answers are unchanged.
10. **Edit before the first answer; locked after (US5-1..3, FR-023, FR-030).**
    - A fixes an option's typo on an unanswered poll. B's existing *Meldungen* row shows the
      corrected question, without becoming unread again and without a new email.
    - After B answers, the content fields are locked, with an explanation. A crafted content
      change answers 409.
11. **Delete (US5-4, FR-024, SC-007).**
    - A deletes a poll: it is gone from the card, from B's and E's *Meldungen* (their unread
      badges drop), and from Home.
    - C left the team before the delete, and C's row is gone too.
12. **Membership (Edge cases, FR-015).**
    - C, who answered, leaves: C's answer disappears from counts and names at once.
    - C rejoins while the poll is still open: C's answer counts again.
13. **Outsider (US1-8, FR-032, SC-009).**
    - J opens `/t/rheinfeuer`: no *Umfragen* card, and no request to `/polls` in the network
      tab.
    - `GET /api/v1/teams/rheinfeuer/polls?state=open` as J → 404 "Team not found".
14. **Account deletion (FR-039, FR-040).**
    - The deletion preview lists "Umfragen, die du gestartet hast, bleiben beim Team, ohne
      Autor".
    - After B deletes their account, B's answers are gone from every count.
    - After A deletes theirs, A's polls stay, with the former-player placeholder.
    - The Terms (`/terms`) and privacy policy read the new generic sentence. `/register` quotes
      the new Terms version.
15. **Rename (FR-026).** A renames the team: B's poll alert shows the new name.
16. **Settings copy (FR-027).** *Einstellungen → Benachrichtigungen* reads "Neuigkeiten und
    Umfragen aus deinen Teams". There is no new row.
