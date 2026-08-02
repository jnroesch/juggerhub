# Quickstart: Validating Self-Service Account Deletion

**Feature**: 037-account-deletion | **Date**: 2026-08-01

How to prove this feature works end to end. Scenarios map to the spec's success criteria; details of shape live in [contracts/account-deletion.md](contracts/account-deletion.md) and [data-model.md](data-model.md) rather than being repeated here.

## Prerequisites

```powershell
docker compose up -d          # Postgres, Redis, Mailpit, Azurite
cd backend;  dotnet run       # API
cd frontend; npx nx serve web # SPA
```

Mailpit at <http://localhost:8025> — needed for the confirmation email (FR-040).

Seed data: the dev seeder gives you members with profiles, teams, events, and chat history. You need **at least two accounts** — the one being erased and an observer, since User Story 3 is verified from the observer's side.

## Test commands

```powershell
# Backend — the disposition inventory and the predicate audit
cd backend; dotnet test --filter "FullyQualifiedName~AccountDeletion"

# The seven `!= Banned` predicates (research R3) — highest-risk area
cd backend; dotnet test --filter "FullyQualifiedName~DeletedAccountVisibility"

# Frontend
cd frontend; npx nx test web

# End to end
cd frontend; npx nx e2e web-e2e --grep "account deletion"
```

---

## Scenario 1 — The happy path (US1 · SC-001, SC-003, SC-006, SC-008)

1. Sign in as a member with a profile photo, a team membership (**not** as sole admin), notifications, and chat history.
2. Go to `/account`. A danger-zone section is present without scrolling hunting.
3. Open the deletion dialog. Read the disclosure — verify it says messages and posts **remain** (FR-025).
4. Enter the password and the confirmation word. Confirm.

**Expect**: signed out immediately; landed on a public route; the whole thing took under two minutes.

**Then verify erasure**:

- Sign-in with the old credentials fails, with the same generic message as any wrong password.
- `/u/<their-handle>` does not resolve.
- Player search for their name and handle returns nothing.
- Their photo URL 404s.
- Mailpit shows the confirmation email, sent to the real address.

---

## Scenario 2 — Blocked as sole admin (US2 · SC-007, SC-011)

1. Sign in as the **only** admin of two teams.
2. Open the deletion flow.

**Expect**: refused, with **both** teams named in one message and a stated remedy (FR-011, FR-012). Not one, then the other.

3. Promote another admin in one team. Return.

**Expect**: the remaining team is still named — the check re-ran rather than serving a cached answer (FR-013).

4. Resolve the second. Retry.

**Expect**: the flow proceeds.

**Race check**: open the preview (clean), then in a second session make yourself sole admin of a team, then confirm in the first. **Expect a 409, and the account intact** — the precondition is re-checked inside the transaction.

---

## Scenario 3 — The observer's view (US3 · SC-003, SC-004, SC-005)

As the *second* account, after Scenario 1, open each of:

- the shared conversation → history reads in order; the departed member shows as **"A former player"**; their message bodies are still there (FR-024)
- the team roster → renders; they are gone from it
- an event participant list they were on → renders; they are gone
- a team news post they authored → post text intact, author is the placeholder
- a notification where they were the actor → still renders, identifies no one

**Expect**: zero errors, zero blank names, zero half-rendered rows. Nothing anywhere exposes their name, handle, email, or photo.

**Re-attribution check (SC-005)**: as a platform administrator, try to find out who wrote the retained message using existing admin tooling. You should not be able to.

---

## Scenario 4 — The archived-conversation trap (FR-028)

The one most likely to be missed. See [data-model.md §3](data-model.md).

1. Create a team, chat in it with two members, then **delete the team** — this archives the conversation, materialising the roster and **freezing display names into `Conversation.Name`**.
2. Now erase one of those members.
3. As the other member, open the archived thread.

**Expect**: no trace of the erased member's name, including in the conversation's own title. Inspect the `Conversation.Name` column directly — a cascade cannot reach a frozen string, so this must have been handled by name.

---

## Scenario 5 — Refusals and atomicity (FR-005, FR-038, FR-042)

| Case | Setup | Expect |
|---|---|---|
| Suspended | Admin-suspend an account, call the endpoint directly with a still-valid token | 403; account unchanged |
| Banned | Same, banned | 403; account unchanged |
| Wrong password | Correct confirmation, wrong password | 401, generic; repeated attempts trip lockout |
| Bad confirmation | Correct password, wrong word | 400; **no password attempt made** |
| Mid-flight failure | Force a fault inside the transaction | Account fully intact — profile, memberships, chat all present; 500 with no internal detail |
| Repeat | Confirm twice from two devices | One succeeds; the other fails harmlessly; no second erasure |

---

## Scenario 6 — Re-registration: ban bars, deletion permits (FR-031/032/034 · SC-012, SC-013)

Two halves of one behaviour. **Run both** — the whole point is that they differ.

### 6a — After self-deletion, the address works again

After Scenario 1, register a new account with the **same email address**.

**Expect**: registration **succeeds and an account exists**. Sign in with it.

> Do not stop at the HTTP 200. Registration returns a deliberately neutral acceptance whether or not it created anything ([AuthService.cs:115-122](backend/Services/Auth/AuthService.cs#L115-L122)), so a broken release reports success while creating nothing. **Sign in, and confirm the row exists.** This is the specific failure mode `UserName` collision produces — see research R4.

The new account has no profile content, no teams, no history, and nothing links it to the old one. Their old messages still read as "A former player" — **not** re-attributed to the new account (FR-035).

### 6b — After a ban, the address stays barred

1. Register a fresh account, then admin-**ban** it.
2. Attempt to register again with that same address.

**Expect**: no new account is created. The response is the same neutral acceptance as any other registration (anti-enumeration — it must not reveal that the address is banned), but no row appears and sign-in with the new password fails.

**The contrast is the assertion.** Same endpoint, same address shape, opposite outcome — banned bars, deleted permits.

---

## Scenario 7 — Three languages (FR-008, FR-043, FR-044, FR-045)

1. Switch the interface to German, then Spanish. Open the deletion dialog in each.

**Expect**: disclosure, blocker messages, and the confirmation word are all localised. The confirmation word accepted by the server matches what the dialog asks for in that language.

2. Open `/privacy` in all three languages and read the rights section.

**Expect**: each describes the in-product control; the manual route remains as a fallback; all three agree that erasure is immediate, that messages are retained under a neutral author, and that the address may be reused. German is authoritative.

---

## What "done" looks like

- [ ] All seven `!= Banned` predicates have an explicit test proving a deleted account is excluded
- [ ] Scenarios 1–7 pass
- [ ] `Conversation.Name` verified clean after Scenario 4
- [ ] Backend, frontend, and e2e suites green
- [ ] UI review checklist completed against DESIGN.md
- [ ] Privacy policy updated in three languages
