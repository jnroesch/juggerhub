# Quickstart: The Team Page Leads Into the Team Chat

**Feature**: 060 · **Contract**: [contracts/team-chat-api.md](./contracts/team-chat-api.md) · **Model**: [data-model.md](./data-model.md)

How to show the feature works end to end. Scenario numbers map to the spec's user stories and
FRs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images. A stale backend image makes a working feature look broken, and the
frontend's root filesystem is read-only.

Accounts: **A** creates team *Rheinfeuer* (admin). **B** joins it (plain member). **J** is a
signed-in player with no relation to the team. **R** has a pending join request.

## Automated checks

```powershell
# Backend: the new suite plus the chat suites whose behaviour the defect fix touches
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~ChatTeamChatLink|FullyQualifiedName~ChatTeamParty|FullyQualifiedName~ChatInquiry|FullyQualifiedName~ChatArchive"

# Frontend: the team page, the chat service and the catalogue guards
cd frontend; npx nx test web --watch=false --testPathPatterns="team-detail|chat.service|catalog-"
```

Then run each whole suite once before the PR: `dotnet test backend/JuggerHub.slnx`,
`npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`.

## Manual scenarios (browser, German, 375px and desktop)

Use a Playwright context with `locale: 'de-DE'` and one browser context **per actor** (the 048
lesson: a second sign-in in the same context replaces the first actor's cookie). The screenshots
answer Gate 7.

1. **The defect, reproduced first (US2, FR-013/FR-014).** Before anyone on the team opens Chat, J
   presses *Admins kontaktieren* on the team page and sends *"Wann trainiert ihr?"*.
   **Expect**: B's Chat inbox lists the *Rheinfeuer* team chat. Before the fix it never appeared.
   J's thread is still in A's inbox, unchanged.
2. **Admin opens the team chat (US1-3, FR-002).** A opens the team page. **Expect**: nothing
   beside the team's name. The side-column card shows *Team-Chat*, *Einladungen*, *Verwalten*.
   Pressing *Team-Chat* opens the conversation tagged TEAM named *Rheinfeuer*, **not** J's
   thread.
3. **Plain member (US1-1, US3-1, FR-016/FR-018).** B opens the team page. **Expect**: nothing beside
   the team's name. The card shows *Team-Chat*, *Admins kontaktieren*, *Verwalten*. *Team-Chat* opens
   the same conversation id as in scenario 2. *Admins kontaktieren* opens a new message to the
   admins, as it did from the header.
4. **Never-opened chat (US1-2, FR-003).** A creates a second team; nobody opens Chat. A presses
   *Team-Chat*. **Expect**: the chat opens, empty. No Meldungen row, no email in Mailpit, no push.
5. **Non-members see the header as today (US3-3, FR-017).** J sees *Admins kontaktieren* and
   *Beitritt anfragen* at the top and no card. R sees *Angefragt* and *Anfrage zurückziehen*.
   Signed out, the top shows *Zum Beitreten anmelden*.
6. **Removed while the page is open (FR-011).** B opens the team page. A removes B. B presses
   *Team-Chat*. **Expect**: B stays on the page, sees *Du bist nicht mehr in diesem Team.*, and the
   page now shows the non-member view.
7. **Outsiders learn nothing (FR-005/FR-007, SC-003).** As J, call
   `GET /api/v1/chat/team/{rheinfeuerId}` and `GET /api/v1/chat/team/{randomGuid}`. **Expect**: two
   identical `404` bodies, and no new conversation for either team in the database.
8. **Hidden chat (FR-012).** B hides the team chat (chat details → *Aus meinen Nachrichten
   ausblenden*), goes back to the team page and presses *Team-Chat*. **Expect**: the chat opens
   and the inbox still does not list it.
9. **Layout (SC-006).** At 375px in German, the card's buttons show their full labels, with no
   truncation and no horizontal scroll. Check desktop too.
