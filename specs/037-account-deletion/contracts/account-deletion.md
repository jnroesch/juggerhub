# API Contract: Self-Service Account Deletion

**Feature**: 037-account-deletion | **Date**: 2026-08-01

Two endpoints on the existing [`AccountController`](backend/Controllers/AccountController.cs). Both require authentication; neither takes an account identifier — the subject is always the calling principal (FR-002).

Per constitution Principle II these are thin: they validate the shape, call `IAccountDeletionService`, and map the result status to an HTTP response.

---

## `GET /api/v1/account/deletion-preview`

Answers "what happens if I do this, and may I?" — everything User Story 2 needs to render the confirmation, without mutating anything.

**Auth**: required. **Rate limit**: standard authenticated policy.

### 200 OK

```jsonc
{
  "canDelete": false,
  "blockers": [
    {
      "kind": "SoleTeamAdmin",
      "subjectId": "0198f2a1-...",
      "subjectName": "Hamburg Hammers",
      "remedy": "MakeAnotherAdmin"
    },
    {
      "kind": "SolePartyAdmin",
      "subjectId": "0198f2b7-...",
      "subjectName": "Summer Slam party",
      "remedy": "MakeAnotherAdminOrDisband"
    }
  ],
  "retained": ["ChatMessages", "NewsPosts", "ModerationRecords", "AwardsGrantedToOthers"],
  "erased":   ["Profile", "Photo", "Email", "Memberships", "Notifications", "Sessions"]
}
```

**Field notes**

- `blockers` is **complete, never truncated** (FR-011). An empty array with `canDelete: true` is the proceed case.
- `kind` is an enum — `SoleTeamAdmin`, `SoleEventAdmin`, `SolePartyAdmin` — so the client localises the message. The server does **not** send display prose here; FR-008 requires three languages and the client owns that catalogue.
- `subjectName` is a display name for the blocking object (the team, event, or party), not for a person.
- `retained` / `erased` are **enum keys, not sentences**, for the same reason. The client renders them from its own catalogue, including the FR-025/FR-027 wording about messages surviving.
- This endpoint is advisory. Everything it reports is re-evaluated at confirmation (FR-013).

### 403 Forbidden

The caller is `Suspended`, `Banned`, or already `Deleted` (FR-005). Generic body; discloses nothing about which.

---

## `POST /api/v1/account/deletion`

Performs the erasure. Immediate, irreversible, atomic (FR-036, FR-029, FR-038).

**Auth**: required. **Rate limit**: strict — this is a credential-verifying endpoint and inherits Identity lockout via `CheckPasswordSignInAsync`.

### Request

```jsonc
{
  "password": "…",
  "confirmation": "DELETE"
}
```

- `password` — re-authentication (FR-003). Verified with `lockoutOnFailure: true`, so brute force is throttled by the existing Identity lockout.
- `confirmation` — a deliberate literal the member types (FR-004). The **expected value is supplied by the client's own language catalogue**, so a German member types a German word; the server compares against the set of accepted values for all supported languages rather than one hardcoded English string.

### 204 No Content

Erasure is complete. Auth cookies are cleared on this response. Nothing is returned — there is no longer an account to describe.

The client must treat this as a hard sign-out: clear local state and navigate to a public route. Any subsequent authenticated request will 401.

### 400 Bad Request

`confirmation` missing or not an accepted value. No password check is attempted.

### 401 Unauthorized

Password incorrect. Generic — indistinguishable from any other failed credential check (Principle I). Repeated failures trip Identity lockout.

### 403 Forbidden

Caller is `Suspended`, `Banned`, or already `Deleted` (FR-005).

### 409 Conflict

A blocking obligation exists. Body carries the **same complete `blockers` array** as the preview (FR-011, FR-012):

```jsonc
{
  "title": "Account deletion blocked",
  "status": 409,
  "blockers": [ { "kind": "SoleTeamAdmin", "subjectId": "…", "subjectName": "…", "remedy": "MakeAnotherAdmin" } ]
}
```

A 409 means **nothing was changed** — the precondition is checked inside the transaction, so a blocker acquired between preview and confirm is caught here rather than half-applied.

### 500 Internal Server Error

Erasure failed and was rolled back; the account is intact (FR-042). Generic body via the global exception middleware — no internal detail (Principle I). The failure is logged with the operation identified and no personal data (Principle VII).

---

## Idempotency and concurrency

`POST` twice, or from two devices at once:

- The second request finds `Status = Deleted` — but the account's credentials no longer exist, so it cannot authenticate at all and receives **401**.
- If both requests are in flight simultaneously, the transaction and terminal status serialise them: one commits, the other finds a state it cannot act on and fails without side effects (FR-039).

There is no scenario in which a second call performs a second erasure or reports a different outcome for the same account.

---

## What is deliberately absent

- **No `DELETE /account/{id}`.** No identifier is accepted anywhere, so there is no shape in which one member can target another (FR-002).
- **No cancel or restore endpoint.** Erasure is immediate and terminal (FR-029, FR-036); an endpoint implying otherwise would contradict the disclosure.
- **No admin-facing deletion endpoint.** Administrators have ban, which is a different remedy with different semantics. Out of scope per the spec's Assumptions.
- **No export endpoint.** Art. 15/20 is the deferred half of issue #105.

---

## Side effects outside the HTTP contract

| Effect | Requirement | Timing |
|---|---|---|
| Confirmation email to the address on file | FR-040 | **Before** the address is nulled — inside the operation, so the send is attempted against a live address |
| Profile photo object reclaimed | FR-015 | **After** commit — a blob delete cannot be rolled back (research R7) |
| All refresh tokens deleted | FR-016 | Inside the transaction |
| Auth cookies cleared | — | On the 204 response |

The email is transactional and follows the constitution's retry rule for that case (a duplicate is an annoyance, a loss is not). A failure to send MUST NOT roll back the erasure — the member asked to be erased, and failing to email them is not a reason to keep their data.
