# Quickstart & Validation: Chat Inbox Search by People and Conversation Names

End-to-end validation for feature 046. Proves the spec's four user stories and the success
criteria. Shapes live in [data-model.md](./data-model.md) and
[contracts/chat-inbox-search-api.md](./contracts/chat-inbox-search-api.md).

## Prerequisites

- Local stack up via docker-compose (backend + Postgres + Redis + Mailpit), per the constitution.
  No migration is involved in this feature — if startup runs one, something else is on the branch.
- Accounts (register them, or reuse the seeded ones from 019's quickstart):
  - **Ada** — the viewpoint player.
  - **Lena B.** — will share a DM, a group and a team chat with Ada.
  - **Ben** — on the same team; no DM with Ada.
  - **Zoe** — shares **nothing** with Ada (proves the new-chat picker still reaches her, US4).
- A team **Hamburg Jugger** with Ada, Lena and Ben on its roster (its team chat exists on first
  inbox view).
- A group **Tournament trip** created by Ada with Lena and Ben.

## Run

```powershell
docker compose up -d --build         # backend + db + redis + mailpit
cd frontend; npm run start           # Nx serve → http://localhost:4200
```

## Automated checks

```powershell
# Backend — the chat collection, including the new ChatInboxSearchTests and the trimmed ChatSearchTests
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Chat"

# Frontend — chat service + inbox component specs, and the i18n catalogue parity guard
cd frontend
npx nx test web --watch=false --testPathPattern="chat|catalog-parity"
npm run lint                         # nx run-many -t lint
npm run build                        # nx build web --configuration=production
```

Expected: all green. The parity spec is the one that fails first if a catalogue key was removed
in `en.json` but not in `de.json`/`es.json`.

## Scenario A — Find a conversation by a person's name (User Story 1, P1)

1. Sign in as **Ada**, open **Chat**. The search field's hint reads *"Find a chat by name…"*
   (not "Search messages"); a screen reader announces "Search your chats by name" (FR-009).
2. Type `len`. **Expect** the list to narrow to exactly three rows — the DM with Lena, the
   *Tournament trip* group, and the *Hamburg Jugger* team chat — each looking exactly like its
   normal inbox row (avatar, name, last-message preview, time, unread badge, tag), in
   most-recently-active order (FR-005, FR-006, SC-001).
3. Type `ben`. **Expect** the group and the team chat, but **not** the DM with Lena.
4. Tap the team chat row → it opens as usual. Back → the term is still applied or cleared,
   either way consistent.
5. Clear the field → **expect** the full inbox, unchanged (FR-007, SC-007).
6. Type `xyzq` → **expect** a plain *"Nothing matched “xyzq”."* — no error, no spinner (FR-008).
7. Type a single character → **expect** the full inbox, untouched (no search fires).
8. **Beyond page one (SC-004)**: with 21+ conversations for Ada (seed extra DMs), search for the
   member of the oldest one → **expect** it found although it never appeared in the inbox list.

## Scenario B — Message text is never searched (User Story 2, P1)

1. As Ada, send *"bringing the banana bread"* to the DM with Lena.
2. Search `banana` → **expect** *"Nothing matched"* (FR-001, SC-002). Search `len` → the DM is
   listed, with the banana message as its ordinary last-line preview — a preview, not a hit
   (FR-011).
3. There is no "In your messages" heading anywhere, and no message excerpt in any result.
4. **By direct request**: `GET /api/v1/chat/search?q=banana` (signed in as Ada) → the JSON has a
   `people` object and **no `messages` property** (FR-010).
   `GET /api/v1/chat/conversations?q=banana` → `items: []`, `totalCount: 0`.

## Scenario C — Find a conversation by its name (User Story 3, P2)

1. Search `trip` → **expect** the *Tournament trip* group (no member is called "trip").
2. Search `hamb` → **expect** the *Hamburg Jugger* team chat.
3. Sign in as **Zoe**, open the Hamburg Jugger team page and use **Contact admins** to start an
   admin-contact thread; then as Ada (a team admin) search `zoe` → **expect** the thread (its
   admin-side label is "Zoe · Hamburg Jugger"); search `hamb` → **expect** both the team chat and
   the thread.
4. Rename nothing, but note: a team chat matched by *both* its name and a member is listed
   **once**.
5. Search `party` → **expect** no party chat listed on the strength of its "Party chat" label
   alone (research §2 — fallback labels are not names).

## Scenario D — Starting a chat with someone new is unaffected (User Story 4, P2)

1. As Ada: **+** → type `zoe` in the new-chat picker → **expect** Zoe offered although they share
   nothing (019 FR-049). Pick her → compose view → send → a DM exists.
2. Open Zoe's profile → **Message** → **expect** the *existing* DM to open (019 FR-008 / 022).
3. As Ada, **block** Ben, then in the new-chat picker type `ben` → **expect** Ben **not** offered
   (019 FR-033). Unblock.

## Scenario E — Eligibility mirrors the inbox (edge cases)

1. **Hidden**: as Ada, open the group's details → **Hide**. Search `trip` and `len` → **expect**
   the group absent from both (owner decision; the un-hide gap is tracked separately in the
   issue filed from this feature).
2. **Blocked DM**: block Lena → search `len` → **expect** the DM absent; the group and team chat
   still listed (019 FR-032). Unblock.
3. **Former member**: as Lena, leave *Tournament trip* → as Ada search `len` → **expect** the
   group absent; the DM and team chat still listed. (Membership is as of now.)
4. **Own name**: search Ada's own name → **expect** no conversation listed on that basis alone.
5. **Accents and case**: give a member the display name *Jörg* → search `jorg` and `JÖRG` →
   **expect** the same conversations.

## Design check (Gate 7)

Walk `checklists/ui-review.md` against the diff. The visible surface is the existing inbox row
list, the search field's copy, the empty state and a `role="status"` loading line; there is no
new component, colour or spacing. Any conflict with DESIGN.md is reported, not resolved
silently.
