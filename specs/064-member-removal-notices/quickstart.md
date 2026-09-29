# Quickstart: Removing a Member Is Confirmed, and the People It Concerns Are Told

**Feature**: 064 · **Contracts**: [notifications](./contracts/notifications.md) ·
[team members API](./contracts/team-members-api.md) · [UI](./contracts/ui.md) ·
**Model**: [data-model.md](./data-model.md)

How to show the feature works end to end. Scenario numbers map to the spec's user stories and FRs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images: a stale backend image makes a working feature look broken, and the
frontend's root filesystem is read-only.

Accounts: **A** creates team *Rheinfeuer* (admin); **T** is a second admin; **J**, **L** and **M** are
members. A creates an event that takes teams and forms a party for it; **J** presses *Ich bin dabei*
and **M** declines. Set **J**'s language to German.

## Automated checks

```powershell
# Backend: the new suites plus the guards the new types must satisfy
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~TeamDeparture|FullyQualifiedName~InviteAcceptRateLimit|FullyQualifiedName~NotificationCategoryMapping|FullyQualifiedName~PushComposer|FullyQualifiedName~TeamRenameRewrite|FullyQualifiedName~Template"

# Frontend: the dialog, both pages, the Alerts row, the five accept sites, the catalogue guards
cd frontend; npx nx test web --watch=false --testPathPatterns="confirm-dialog|team-detail|party-manage|notification-row|alerts.component|my-team|onboarding|invite-accept|needs-you|catalog-"
```

Before the PR run each whole suite once: `dotnet build backend/JuggerHub.slnx` (gate on its exit
code), `dotnet test backend/JuggerHub.slnx`, `npx nx test web --watch=false`, `npx nx lint web`,
`npx nx build web`.

## Manual scenarios (browser, German, 375px and desktop)

One Playwright context **per actor** with `locale: 'de-DE'` (a second sign-in in one context replaces
the first actor's cookie). The screenshots answer Gate 7.

1. **Remove asks first (US1, FR-001–FR-003).** A opens Rheinfeuer, ⋯ beside J → *Entfernen*.
   **Expect**: a bottom sheet naming J and Rheinfeuer; *J behalten* has focus. Escape → nothing
   changed, focus back on J's ⋯. Again → *Aus dem Team entfernen* → J is gone from the roster.
2. **J is told (US2, FR-010–FR-012).** Signed in as J. **Expect**: one Meldungen row *Du bist kein
   Mitglied von Rheinfeuer mehr* that opens `/t/rheinfeuer`; Mailpit has one German email with the
   same message and a button to the team; neither names A.
3. **T is told, A is not (US3, FR-014/FR-015).** T has *J wurde aus Rheinfeuer entfernt*; A has
   nothing new; Mailpit has one email to T and none to A.
4. **Leaving tells every admin (US3-2).** L leaves from *Team verwalten*. **Expect**: A and T each
   have *L hat Rheinfeuer verlassen*; L has nothing.
5. **Removed meanwhile (edge, FR-004).** Two admin tabs; T removes M; A (stale page) removes M.
   **Expect**: A's dialog closes with a note that M is no longer on the team; the roster refreshes;
   M has exactly one notice.
6. **Party Remove and Disband (US4, FR-006–FR-009).** A on the party page: *Dabei* tab → Remove
   beside J → dialog *J aus der Party nehmen?*; *Abgesagt* tab → Remove beside M → dialog about
   clearing M's answer; Disband → the in-page dialog (no browser box). Confirm the removal of J.
   **Expect**: J gets no Meldung, no email.
7. **Invitation limit (US5, FR-024/FR-025).** As one player, join Rheinfeuer by its shared link and
   leave, ten times; the eleventh join. **Expect**: *Du bist in kurzer Zeit vielen Teams beigetreten…*
   on the invite page, nothing added, no automatic retry (one request in the network log).
8. **Language and layout (SC-008).** Every dialog at 375px and 1280px in German: full title, full
   body, both answers unclipped, no horizontal scroll.
