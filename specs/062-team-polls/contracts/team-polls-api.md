# Contract: Team Polls API (062)

**Base path**: `/api/v1/teams/{slug}/polls`.

**Access**:
- Every route needs authentication (026). Anonymous callers get **401**.
- Every route is **member-only**: a caller who is not on the team, or a team slug that does not
  exist, gets the same **404** `title: "Team not found"`. There is no membership oracle, and no
  way to learn whether the team has polls (FR-032).
- Routes that name a poll answer **404** `title: "Poll not found"` when the poll is not in this
  team, including a poll that was deleted.
- Admin-only routes answer **403** to a plain member.

**Errors**:
- They are ProblemDetails (`application/problem+json`).
- Every user-facing refusal carries `extensions.code`. Refusals about one option also carry
  `extensions.option`, the 0-based index.
- Clients map `code` to their own text and never display `detail` (GH #179).

**Retries**: no mutation here is retried automatically by the browser (constitution VII).

---

## Shapes

### TeamPollDto

The same shape comes from every endpoint, built for the **caller**.

```jsonc
{
  "id": "0192…",
  "question": "Thursday instead of Tuesday this week?",
  "allowsMultiple": false,
  "isAnonymous": false,
  "resultsAfterAnswer": false,
  "createdDate": "2026-09-28T18:02:11Z",
  "closesAt": "2026-10-04T18:00:00Z",   // scheduled close; null = none. Present on closed polls too (history)
  "closedAt": null,                     // the moment it closed (early close, or closesAt once passed); null while open
  "isOpen": true,
  "authorName": "Ada K.",               // null once the author is banned or their account deleted → client placeholder
  "authorHandle": "ada",                // null likewise
  "memberCount": 12,                    // current members (not banned) — the "of N"
  "answeredCount": 7,                   // current members who have answered
  "resultsVisible": true,               // false ⇒ every option.count and option.voters is null
  "hasAnswers": true,                   // any stored answer: content fields can no longer change (FR-023)
  "myOptionIds": ["0192…a1"],           // the caller's own answer; [] when unanswered
  "options": [
    {
      "id": "0192…a1",
      "text": "Thursday works",
      "count": 5,                        // null when !resultsVisible
      "voters": [                        // null when isAnonymous or !resultsVisible
        { "name": "Ada K.", "handle": "ada" }
      ]
    }
  ],
  "notAnswered": [                       // ONLY for a current admin, ONLY on a named poll; otherwise null
    { "name": "Ben T.", "handle": "ben" }
  ]
}
```

**Invariants the tests assert on the raw JSON:**
- `isAnonymous: true` ⇒ `voters` is null on every option and `notAnswered` is null, **for every
  caller including the author and every admin**. No other member's handle, name or user id
  appears anywhere in the body (SC-003).
- `resultsVisible: false` ⇒ `count` and `voters` are null on every option (SC-003a).
  `answeredCount` is still present.
- `resultsVisible = !resultsAfterAnswer || !isOpen || myOptionIds.length > 0`, and the same rule
  holds for the author and admins.
- Counts, `answeredCount`, `memberCount`, `voters` and `notAnswered` include only current,
  non-banned members (FR-015).
- `options` are in the admin's order.

### CreateTeamPollRequest

```jsonc
{
  "question": "Which jersey colour?",
  "options": ["Black", "Orange", "Teal"],
  "allowsMultiple": false,
  "isAnonymous": true,
  "resultsAfterAnswer": true,
  "closesAt": "2026-10-04T18:00:00Z"      // ISO instant with offset, or null. Client converts from the viewer's local time.
}
```

### UpdateTeamPollRequest

This is the create request **minus `isAnonymous`**, which cannot be changed (FR-016).

```jsonc
{ "question": "…", "options": ["…"], "allowsMultiple": false, "resultsAfterAnswer": false, "closesAt": null }
```

### AnswerTeamPollRequest

```jsonc
{ "optionIds": ["0192…a1"] }
```

---

## Endpoints

### `GET /api/v1/teams/{slug}/polls?state=open|closed&skip=0&take=20`

The team's polls, member-only.

| `state` | Order | Notes |
|---|---|---|
| `open` | newest first | never more than 10 (the cap); the card asks `take=10` |
| `closed` | most recently closed first (`closedAt`) | paged; the card asks `take=5` and offers "Show older polls" |

- **200** `PagedResult<TeamPollDto>`.
- **400** for an unknown `state`.
- **404** "Team not found".

### `POST /api/v1/teams/{slug}/polls`

Start a poll, admin-only.

- **201** `TeamPollDto`. Every current member except the author is then notified once on each
  channel their *Team news* setting allows. This happens after the poll is committed and is
  best-effort: a notification failure never fails the request.
- **400** with `code` ∈ `question`, `optionCount`, `optionLength`(+`option`),
  `optionDuplicate`(+`option`), `closesAtPast`, `closesAtTooFar`.
- **409** `code: tooManyOpen` when the team already has 10 open polls.
- **403** for a plain member; **404** "Team not found".

### `PUT /api/v1/teams/{slug}/polls/{pollId}`

Change a poll, admin-only. It is a full replace of the editable fields.

- The poll must be open, otherwise **409** `closed`.
- The question, options (text and order), `allowsMultiple` and `resultsAfterAnswer` may differ
  from the stored values only while the poll has no answer. Otherwise the answer is **409**
  `answered`, and nothing at all is applied.
- Sending them unchanged is not a change. `closesAt` may always change while the poll is open.
- When the question changes, the poll's delivered Alerts rows show the new question silently:
  not unread again, not moved, nothing re-sent.
- **200** `TeamPollDto`.
- **400** with the same codes as create.
- **403**; **404** team; **404** poll.

### `PUT /api/v1/teams/{slug}/polls/{pollId}/answer`

Give or replace the caller's answer, member-only.

- **200** `TeamPollDto`. With hidden results, it now carries them.
- **400** `choiceCount`: a one-answer poll given anything but exactly 1 option, or a
  multi-answer poll given 0. **400** `choiceUnknown`: an id not in this poll. Duplicate ids count
  once.
- **409** `closed`.
- **404** team; **404** poll.

### `DELETE /api/v1/teams/{slug}/polls/{pollId}/answer`

Withdraw the caller's answer, member-only.

- **200** `TeamPollDto`. This is idempotent: withdrawing when nothing is there is still 200.
- **409** `closed`.
- **404** team; **404** poll.

### `POST /api/v1/teams/{slug}/polls/{pollId}/close`

Close now, admin-only. Final.

- **200** `TeamPollDto` with `isOpen: false`.
- **409** `closed` when it was already closed, by another admin or by its time.
- **403**; **404** team; **404** poll.

### `DELETE /api/v1/teams/{slug}/polls/{pollId}`

Delete, admin-only, open or closed.

- The poll, its options, every answer and **every Alerts row it produced** go in one transaction
  (FR-024). Former members' rows go too: rows are found by the dedupe prefix `poll:{pollId}`,
  never through the roster. Unread badges are refreshed after the commit.
- **204**.
- **403**; **404** team; **404** poll (a second delete answers 404, not 204).

---

## Changed contracts (existing endpoints)

| Endpoint | Change |
|---|---|
| `GET /api/v1/notifications` | A new `type: "TeamPoll"` with payload `{ teamSlug, teamName, pollId, question }`, an actor (the author), and `resolved: false` |
| `GET /api/v1/home` | `needsYou[]` may contain `kind: "TeamPoll"`, `id` = poll id, `params: { teamName, teamSlug, question }`, `linkTarget` = team slug |
| `GET /api/v1/notification-preferences` | The *Team news* description now mentions polls (all three languages); the categories are unchanged |
| `GET /api/v1/account/deletion-preview` (037) | `retained` may include `"Polls"` |
| `POST /api/v1/auth/register` (041) | The Terms version it must quote moves to the new version (R13) |

**Push payload** for `TeamPoll`:
- title: the team name;
- body: a fixed sentence in the recipient's language, with **no question** (FR-028);
- url: `/t/{slug}#poll-{pollId}`;
- tag: the dedupe key.
