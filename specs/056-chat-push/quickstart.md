# Quickstart: seeing chat push work

**Feature**: 056-chat-push | **Contracts**: [chat-push-api.md](./contracts/chat-push-api.md)

Validation only — no implementation code here. Scenarios 1–6 run against the local stack; 7 is the
owner's device walk and 8 is the legal check that no automated guard covers.

## Prerequisites

- The local stack up (`docker compose up -d`), migrations applied.
- **VAPID keys configured** in `.env` — feature 055 fails startup without them. Generate with
  `scripts/New-VapidKeyPair.ps1` if this is a fresh checkout.
- Two accounts, signed in in two different browsers (not two tabs — the push subscription is per
  browser profile).
- Notifications enabled for account A's browser: Settings → Notifications → the device section at
  the top. Chrome or Firefox on the desktop is enough for everything except scenario 7.

**No local timing override is needed, and there deliberately isn't one.** The shipped values are
`QuietDelaySeconds: 5` and `PollIntervalSeconds: 2` in every environment, so a notification arrives
5–7 seconds after the message and what you walk through here is exactly what runs in Prod.

---

## 1. A missed message arrives (US1, FR-001, FR-019)

1. As A, enable notifications and then **close every JuggerHub tab**.
2. As B, send A a direct message: `Are we training tomorrow?`
3. Wait out the quiet delay.

**Expect**: one OS notification, headed **`B's display name`**, reading **`Are we training
tomorrow?`**. Clicking it opens `/chat/{conversationId}` with the message there.

**Then**: check the Alerts inbox as A. **It must be empty of anything about this** — FR-004 is the
decision the whole design protects, and this is the cheapest place to catch a regression.

## 2. Reading it cancels it (US1, FR-009, SC-003)

1. As A, open the conversation and leave it open.
2. As B, send another message.
3. Wait out the quiet delay plus a poll interval.

**Expect**: **nothing**. The message was read before the pass looked at it.

## 3. Several messages collapse into one (FR-022, SC-005)

1. As A, close every tab again.
2. As B, send four messages a few seconds apart into a **team** chat A belongs to.
3. Wait.

**Expect**: exactly **one** notification on A's device, headed by the **team chat's name** with the
body `B: <the newest message>`. Not four, and not one per pass — the tag is `chat:{conversationId}`,
so a later arrival replaces the earlier notification.

## 4. Mute keeps its promise (US2, FR-010, SC-004)

1. As A, open the team conversation → details → **Mute**. Close every tab.
2. As B, post to it. Wait.

**Expect**: **nothing**, on any device.

3. Unmute. Have B post again. **Expect**: a notification.

> **Do not also test "hide suppresses it"** — it does not, and that is correct. A member-written
> message un-hides the conversation for everyone (048 FR-007) before the pass ever looks at it. The
> only case FR-011 covers is archiving *during* the quiet delay, which is scenario 4b below.

### 4b. Archiving during the delay (FR-011)

The window is only 5 seconds, so raise `QuietDelaySeconds` temporarily to give yourself room: have B
send, then as A hide the conversation inside the window, then wait. **Expect**: nothing. (Put the
value back afterwards — the integration suite covers this case deterministically in
`ChatPushEligibilityTests`, so this walk is a sanity check rather than the guard.)

## 5. The off switch (US3, FR-027, FR-028)

1. As A: Settings → Notifications. **Expect** a **Chat** entry alongside Invites, Team news,
   Trainings and Events, with a working **Push** toggle and In-app / E-mail shown as **not
   available** — visibly different from a switch that is off.
2. Turn Chat's Push off. Close every tab. Have B send. Wait. **Expect**: nothing.
3. With it still off, have B invite A to a team. **Expect**: that notification still arrives —
   FR-028a, the categories are independent.
4. Open the app. **Expect**: the chat badge and unread messages exactly as before — FR-028b, the
   toggle governs devices only.

Then the server-side half, which the interface will not let you do:

```bash
curl -X PUT "$API/api/v1/notification-preferences/Chat/Email" \
     -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
     -d '{"enabled":true}'
```

**Expect `400`**, and **no row** in `NotificationPreferences` for `(Chat, Email)`.

## 6. Nothing leaks into the logs (FR-021c, SC-011)

After running scenarios 1–5:

```bash
docker compose logs backend | grep -i -E "training tomorrow|<B's display name>|<the team's name>"
```

**Expect no matches.** Message text, sender names and conversation names must appear in no log
line, at any level. This is stricter than the rest of the platform and it is deliberate.

## 7. The device walk (owner, required)

Not optional and not replaceable by the desktop run — iOS only delivers push to a Home-Screen
installed app, which is why 054 existed at all.

- **Android**, installed from Chrome: scenario 1 with the phone locked. The notification appears on
  the lock screen with the sender and the text. Tapping it opens the installed app on the
  conversation, not a new browser tab.
- **iPhone**, Added to Home Screen: same. Confirm it arrives with the app fully closed.
- **Settings → Notifications at 375px, in German**: the Chat row reads correctly, the unavailable
  cells do not look like switches that are merely off, and nothing overflows.
- **Settings → Notifications at `md` (768px), in German**: five rows in the four-column grid. This
  is the binding case — German is the longest copy and the fifth row is new.
- Screenshots to the PR.

## 8. The privacy policy (FR-029, SC-012)

**No automated guard covers this.** `legal-catalog.spec.ts` compares key *sets*, so a stale value
passes.

Open `/privacy` in **German** (the authoritative version) and read the paragraph about the
notification delivery service. It must no longer say that nothing you wrote is in a notification.
Then check English and Spanish say the same thing, and that the Spanish no longer enumerates only
teams, events and trainings.

**This must be true in an environment before any real member can receive a chat notification
there.** Deploying the feature ahead of the text publishes a false statement in a binding document.

---

## Running the tests

```bash
# Backend — needs Docker for Testcontainers
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~ChatPush"

# The extraction in Phase 3 changed nothing
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Chat"

# Frontend
cd frontend && npx jest --testPathPatterns "notification-settings"
```

> Jest 30 takes `--testPathPatterns` (plural). The old `--testPathPattern` is silently ignored and
> runs the whole suite.
