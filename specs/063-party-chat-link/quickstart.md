# Quickstart: The Party Page Leads Into the Party Chat

**Feature**: 063 · **Contract**: [contracts/party-chat-api.md](./contracts/party-chat-api.md) · **Model**: [data-model.md](./data-model.md)

How to show the feature works end to end. Scenario numbers map to the spec's user stories and
FRs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images. A stale backend image makes a working feature look broken, and the
frontend's root filesystem is read-only.

Accounts: **A** creates team *Rheinfeuer* (admin) and an event that takes teams, then forms a party
for it (party admin). **B** is on the team and presses *Ich bin dabei* (crew member). **N** is on the
team and does not answer. **D** is on the team and declines. **G** is not on the team; seat G as a
marketplace guest (through the market, or directly in the database as `PartyMember { Status = In,
ViaMarket = true }`). **J** is a signed-in player with no relation to the team.

## Automated checks

```powershell
# Backend: the new suite, plus 060's suite that guards the shared helper
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~PartyChatLink|FullyQualifiedName~ChatTeamChatLink|FullyQualifiedName~ChatTeamParty"

# Frontend: the party page, the chat service and the catalogue guards
cd frontend; npx nx test web --watch=false --testPathPatterns="party-manage|chat.service|catalog-"
```

Then run each whole suite once before the PR: `dotnet test backend/JuggerHub.slnx`,
`npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`.

## Manual scenarios (browser, German, 375px and desktop)

Use a Playwright context with `locale: 'de-DE'` and one browser context **per actor** (the 048
lesson: a second sign-in in the same context replaces the first actor's cookie). The screenshots
answer Gate 7.

1. **Never-opened chat, crew member (US1-2, FR-003).** Nobody has opened Chat. B opens the party
   page. **Expect**: the card *Du bist in dieser Crew.* shows *Party-Chat* and *Party verlassen*.
   Pressing *Party-Chat* opens an empty conversation tagged *Party*. No Meldungen row, no email in
   Mailpit.
2. **Same chat as the inbox (US1-1, FR-002).** B opens Chat. **Expect**: the party's chat in the
   inbox is the conversation scenario 1 landed in (same id in the URL).
3. **Party admin (US2, FR-015/FR-016).** A opens the party page. **Expect**: the readiness card shows
   *Für Event bewerben* (coral) and *Party-Chat* (secondary). Pressing *Party-Chat* opens the same
   conversation. After A applies, the card shows *Vom Event zurückziehen* and *Party-Chat*.
4. **Marketplace guest (US1-3).** G opens the party page. **Expect**: the crew card with
   *Party-Chat*, which opens the same conversation.
5. **Outside the crew (US3, FR-001, SC-005).** N sees the request card (*Ich bin dabei / Kann nicht*)
   and no *Party-Chat*. D sees the same with no *Party-Chat*. Nothing else on the page differs from
   before the feature.
6. **Joining shows the button (US3-3).** N presses *Ich bin dabei*. **Expect**: the page now shows the
   crew card with *Party-Chat*.
7. **Left since the page loaded (FR-011).** B opens the party page. In another context B leaves the
   party (or A removes B). B presses *Party-Chat*. **Expect**: B stays on the page, sees *Du bist
   nicht mehr in dieser Crew.*, and the page now shows the request card.
8. **Outsiders learn nothing (FR-005/FR-007, SC-003).** As J, call
   `GET /api/v1/chat/party/{partyId}` and `GET /api/v1/chat/party/{randomGuid}`. As N (on the team,
   not crew), call the first. **Expect**: three identical `404` bodies, and no party chat created by
   any of them.
9. **Hidden chat (FR-012).** B hides the party chat (chat details → *Aus meinen Nachrichten
   ausblenden*), goes back to the party page and presses *Party-Chat*. **Expect**: the chat opens and
   the inbox still does not list it.
10. **Disbanded (spec Context).** A disbands the party. **Expect**: the party page is *Party nicht
    gefunden*; the archived chat stays in B's inbox, readable.
11. **Layout (SC-006).** At 375px in German, the crew card and the readiness card show every label in
    full, with no truncation and no horizontal scroll. Check desktop too.
