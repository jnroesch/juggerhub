# Quickstart & Validation: Chat

End-to-end validation for feature 019. Proves the core loops from the spec's user stories. Shapes live
in [data-model.md](./data-model.md) and [contracts/chat-api.md](./contracts/chat-api.md).

## Prerequisites

- Local stack up via docker-compose (backend + Postgres + Mailpit) per the constitution.
- `AddChat` migration applied (`dotnet ef database update` runs on startup in local).
- `DevDataSeeder` has seeded: the demo team **Rheinfeuer** with a roster, and a seeded party on an event.
- Accounts needed:
  - **Ada** — on Rheinfeuer (the viewpoint player from the wireframes)
  - **Ben** — also on Rheinfeuer (for the DM + team chat)
  - **Zoe** — on **no shared team, party or event with Ada** (proves open DM reach, FR-049)
- **Two browsers** (or one normal + one private window) so two players can be signed in at once —
  every realtime scenario needs it.

## Run

```powershell
docker compose up -d --build         # backend + db + mailpit
cd frontend; npm run start           # Nx serve → http://localhost:4200
```

## Scenario A — The 1:1 loop (User Story 1, P1)

1. Sign in as **Ada**. The nav shows a **Chat** destination.
2. First time through: **expect** a warm empty state ("No messages yet") with a "Start a chat" action —
   not a spinner, not an error.
3. Tap **+** → pick **Ben** only → no name field appears → Start.
4. Send "you coming to training tonight?" → **expect** it right-aligned in **coral** (not blue — see
   the design note below), with a time and a **Sent** state.
5. In the second browser as **Ben**: the Chat nav badge shows **1**; the inbox row shows Ada with the
   preview and a badge.
6. Open it as Ben → the badge clears and the nav total drops. Back as Ada → her message now reads **Read**.
7. **Verify idempotence**: as Ada, tap + → pick Ben again → you land in the **existing** thread, not a
   second one (FR-008).
8. **Negatives**: send an empty/whitespace message → blocked. Paste > 2 000 chars → blocked with a
   clear message (FR-010).
9. **Delete**: hover/open one of Ada's own messages → Delete → **expect** the content gone for *both*
   players, replaced by a "message deleted" tombstone that keeps its place in the thread (FR-050). Ben
   has no delete control on Ada's messages (FR-050a).

## Scenario B — It's live (User Story 2, P1)

Both browsers, same conversation open.

1. Start typing as Ben → **expect** a typing indicator in Ada's thread **and** on her inbox row.
2. Stop typing → the indicator disappears **by itself** within ~5 s (FR-020). Kill Ben's tab
   mid-typing → it still expires; it never sticks.
3. Send as Ben → it lands in Ada's open thread with **no refresh**, and her inbox row updates.
4. Scroll Ada up into the history, then send as Ben → **expect** a **"new messages" divider** and a
   **jump-to-latest pill** with a count — and Ada's **scroll position does not move** (FR-021).
5. Tap the pill → jumps to newest, pill clears, messages mark read.
6. **The important one (FR-023)**: stop the backend, reload Ada's tab, restart the backend, reload
   again → the full history, unread counts and read state are all correct. Nothing was live-only.
7. **Multi-device (FR-016)**: open Ada in a third session, read there → the first tab's badge converges
   without a refresh.

## Scenario C — Named groups (User Story 3, P2)

1. As Ada: **+** → switch to **Group** → pick **Ben and one more** → the **name field appears** →
   name it "Weekend crew" → Start.
2. **Negative**: try to submit a group with a blank name → blocked (FR-009).
3. Send a message → **expect** each other member's messages **labelled with the sender's name**; your
   own are not (FR-012).
4. Details → **Add people** → add a fourth → **expect** a quiet system line in the thread and the
   group appearing in the new member's inbox.
5. Add the **same person again** → **expect** a no-op: no duplicate member, no second system line.
6. **Leave** as the group's *creator* → a system line records it, and the group **keeps working for
   everyone else** (US3 #6, #7).

## Scenario D — Team & party chats appear by themselves (User Story 4, P2)

1. As Ada, open Chat → **expect** the **Rheinfeuer** chat already there, tagged **TEAM**, with the
   whole roster as members — nobody created it (FR-024, and it works for the *pre-existing* seeded
   team, which is the backfill requirement).
2. Open its **Details** → **expect no Leave control** — only mute and hide (FR-026, US4 #4).
3. **The security check (FR-025)**: remove a member from the Rheinfeuer roster (team settings). As that
   player, the team chat **disappears from the inbox**, and a direct
   `GET /api/v1/chat/conversations/{id}` returns **404** — not 403 (FR-048).
4. Re-add them → access and history return (edge case: rejoining).
5. Open the seeded **party** → **expect** its chat tagged **PARTY**, including any marketplace guest.
6. **Disband the party** → its chat is still **readable**, the composer is gone, and a `POST …/messages`
   returns **409** (FR-027).

## Scenario E — Mute, hide, block (User Story 5, P2)

1. **Mute** the Rheinfeuer chat → new messages there **stop raising the nav badge**, but the row still
   updates its last line and time (FR-028).
2. **Hide** a conversation → it leaves the inbox (FR-029).
3. **Block**: as Ada, block **Zoe** from Zoe's DM details.
   - As Zoe, sending to Ada is **refused server-side** — verify by calling
     `POST /api/v1/chat/conversations/{id}/messages` **directly** (curl/REST client), bypassing the UI.
     It must return `403`, not merely hide a button (FR-031, SC-005).
   - Zoe starting a **fresh** DM with Ada is also refused (FR-049b) — the block cannot be walked around.
   - Ada's inbox no longer lists the Zoe thread (R19).
4. **The group carve-out (FR-032)**: put Ada and Zoe in one group → **both keep participating
   normally**. The block is `Direct`-only.
5. **Unblock** → DMs work again and the **prior history is intact** (FR-030).

## Scenario F — Open reach & rate limiting (FR-049, FR-049a)

1. As Ada, **+** → search for **Zoe**, who shares *no* team/party/event with her → **expect** you can
   DM her, in the same number of steps as messaging Ben (SC-012). Teammates are merely listed first.
2. **The limit**: script ~15 rapid `POST /api/v1/chat/conversations` calls → **expect `429`** once past
   10/min. Same for > 30 sends/min. Must hold when driving the **API directly**, not just the UI.

## Scenario G — Links become cards (User Story 7, P3)

1. Paste a training link (`http://localhost:4200/trainings/sessions/{id}`) into a chat → send →
   **expect** a card with the training's **name and time** and a link through to its page.
2. **Expect no RSVP buttons on the card** — it is view-only; acting means following the link (FR-038).
   *(This is the deliberate scope cut from the wireframe's 9c.)*
3. Paste `https://example.com/whatever` → **expect plain text**, no card, and **no outbound request
   from the server** (FR-039, FR-042).
4. **The security check (FR-040)**: paste a link to a **team-only** training into a DM with **Zoe**
   (not on the team). As Ada → card. **As Zoe → the plain link only**, no name, no time. Same message,
   two viewers, two answers.
5. Delete the training → the message **degrades to a plain link**, no error (FR-041).
6. Send a body containing `<script>alert(1)</script>` → **expect it rendered as literal text** (FR-014).

## Scenario H — Desktop layout (User Story 8, P3)

1. Widen the window → **expect** the inbox becomes a **left rail**, the conversation fills the space
   beside it, and Details opens as a **right side panel** (not a screen replacement).
2. Click a different conversation in the rail → the pane swaps and **the URL reflects the open
   conversation**; reload → it reopens (FR-046).
3. Narrow to mobile → the same screens behave as separate pushed views with a back affordance.
4. Re-run Scenario B's live checks at desktop width → **identical behaviour**, only the layout differs
   (SC-009).

## Scenario I — Search (User Story 6, P3)

1. Tap the inbox search → type a term you know is in a Rheinfeuer message → **expect** results split
   into **messages** (from your conversations) and **people**.
2. Select a message result → jumps to that message in its conversation.
3. Select a person → opens/starts the DM.
4. **The security check (FR-035, SC-006)**: pick a term that exists **only** in a conversation Ada is
   not in → `GET /api/v1/chat/search?q=…` **directly** → **zero** message results. No count, no snippet,
   no leak.
5. A blocked player does not appear under people (FR-033).

## Design compliance

Checked in full via [checklists/ui-review.md](./checklists/ui-review.md). The two to eyeball here:

- **Own bubbles are coral** (`brand-primary`), others `surface-muted` sand. The wireframe drew blue;
  DESIGN.md wins and blue stays reserved for the `info` token (research §11).
- **Times, counts and unread badges are set in the mono face** (DESIGN.md: "numbers, scores, times
  and counts in the mono typeface"), sentence case everywhere, TEAM/PARTY tags as eyebrow-styled pills.

## Automated verification

```powershell
# backend — integration tests incl. the Chat suite
cd backend; dotnet test

# frontend
cd frontend; npx nx test web; npx nx lint web; npx nx build web
```
