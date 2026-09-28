# Quickstart: Join Requests Reach the People Who Decide Them

**Feature**: 058 · **Contracts**: [join-requests-api](./contracts/join-requests-api.md),
[notifications](./contracts/notifications.md), [home-needs-you](./contracts/home-needs-you.md) ·
**Model**: [data-model.md](./data-model.md)

How to prove the feature works end to end. Scenario numbers map to the spec's user stories and FRs.

## Prerequisites

```powershell
docker compose up -d --build backend frontend   # nginx at http://localhost:3000 proxies /api; Mailpit at :8025
```

Rebuild **both** images after changing either side — a stale backend image makes a
backend-dependent behaviour look broken (the 048 walk lesson), and the frontend container's root
filesystem is read-only.

Accounts: **A** creates the team (admin), **B** joins and is made admin, **M** joins as a plain
member, **J** is the player who asks to join (no team). Give A the language **de** and J **en**
(Settings → language), so the recipient-language rule is visible. Sign each actor in on its **own**
browser context — sharing one silently swaps the session cookie (the 048 lesson).

## Automated checks

```powershell
# Backend: the new suites plus every suite whose shape this feature changes
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~JoinRequest|FullyQualifiedName~NeedsYou|FullyQualifiedName~NotificationCategoryMapping|FullyQualifiedName~PushComposer|FullyQualifiedName~NotificationChannelIndependence|FullyQualifiedName~TemplateParity|FullyQualifiedName~TemplateRenderMatrix|FullyQualifiedName~MyInvitations|FullyQualifiedName~NotificationTests"

# Frontend: touched components and the catalogue guards (Jest 30: the flag is plural)
cd frontend; npx nx test web --watch=false --testPathPatterns="notification-row|needs-you-card|team-detail|onboarding|catalog-"
```

Then, before the PR: the whole suites (`dotnet test backend/JuggerHub.slnx`,
`npx nx test web --watch=false`), `npx nx lint web`, and a production build (`npx nx build web` —
it catches what the looser Jest config does not).

## Manual scenarios (browser — German at 375px, and desktop)

Walk each at **375px** and desktop width in German (`locale: 'de-DE'` on the Playwright context)
and screenshot — Gate 7 is answered from the screenshots. Read the driver's output; do not trust a
green run without looking.

1. **The admins hear (US1, FR-001–FR-006).** J opens the team page → *Beitritt anfragen* (the
   dialog now says J will be told the answer) → send.
   **Expect**: A and B each have one new *Meldungen* row "J … möchte Hamburg Hammers beitreten",
   M has none, J has none. Mailpit holds one email to A **in German** and one to B, none to M or J.
   Opening the row lands on the team page with J in the queue.
2. **Once only (FR-003).** J presses *Request to join* again via the API
   (`POST …/join-requests`). **Expect**: `204`, no new row, no new email.
3. **Home (US4, FR-018–FR-019a).** A opens Home. **Expect**: *Braucht dich* shows
   "J … möchte beitreten · Hamburg Hammers" with *Annehmen*/*Ablehnen*, the name links to J's
   profile, and every other item in the card is German too (seed a team invitation for A to see
   the old kinds translated). M's Home shows no join-request item.
4. **Approve from Home (US2, FR-008, FR-011).** A taps *Annehmen*. **Expect**: the item goes; J is
   on the roster; J gets one row "You're in: Hamburg Hammers accepted your request" (English — J's
   language) that opens the team page, and one email; no admin's name appears anywhere in either.
   B's alert now reads as no longer waiting; B's Home no longer lists it.
5. **Decline (FR-009).** Repeat with a fresh player K; B declines on the team page. **Expect**: K
   gets one row "Hamburg Hammers declined your request" opening the team browser, plus an email.
6. **Two admins at once (FR-012).** With a fresh player L waiting, A and B both have Home open; A
   approves, then B presses *Ablehnen* on the stale item. **Expect**: B sees the notice that the
   request no longer needs them and the item disappears; L is a member and received **only** the
   accepted notice. (Automated test: two concurrent answers → one `204`, one `404`, one notice.)
7. **Withdrawal (US3, FR-022).** Fresh player P requests; A leaves the alert unread; P withdraws.
   **Expect**: the alert is gone from A's and B's inboxes, and A's unread badge drops **without a
   reload** (A's tab open during the withdrawal).
8. **The limit (FR-023).** As one player, send 10 requests (to different teams, or
   request/withdraw one team) within the hour, then an 11th. **Expect**: the 11th is refused with
   *"Du hast in kurzer Zeit viele Anfragen geschickt …"* on the team page (and the onboarding team
   step shows the same meaning in its own place); no admin received an 11th alert or email.
   Withdrawing still works while over the limit.
9. **Joined another way (FR-020).** Q requests to join; then A sends Q a personal invitation and Q
   accepts it. **Expect**: the request is gone from the queue and from Home; the admins' alerts
   about it are gone; Q received no answer notice.
10. **Ban and erasure (FR-005, FR-021).** R requests; a platform admin bans R. **Expect**: the
    request leaves A's queue and Home; A's alert names no one ("Ein ehemaliger Spieler …") and
    reads as no longer waiting. Unban R: the request waits again. Separately, S requests and then
    deletes their account: A's alert names no one and no longer waits.
11. **Keyboard + 375px layout.** Tab through the *Needs you* item's link and both buttons; the
    notice is announced (`role="status"`). At 375px, a long German name plus a long team name
    wraps without truncation or horizontal scroll in the card, in both new Alerts rows and in the
    refusal message.

## API spot checks (optional)

```powershell
# The 11th request in an hour → 429 with nothing stored
curl -i -X POST "http://localhost:3000/api/v1/teams/<slug>/join-requests" -b cookies.txt

# Answering a request that no longer waits → 404 "Request not found", nothing changes
curl -i -X POST "http://localhost:3000/api/v1/teams/<slug>/join-requests/<answeredId>/decline" -b admin-cookies.txt

# The stored alert carries no name — read it back
docker exec juggerhub-database psql -U postgres -d juggerhub -Atc "SELECT \"Payload\", \"ActorUserId\" FROM \"Notifications\" WHERE \"Type\" = 9 ORDER BY \"CreatedDate\" DESC LIMIT 1;"
```
