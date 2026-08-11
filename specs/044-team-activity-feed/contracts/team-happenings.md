# Contract: `GET /teams/{slug}/happenings`

**Feature**: 044 | **Spec**: [../spec.md](../spec.md) | **Model**: [../data-model.md](../data-model.md)

The team's recent internal happenings. **Members only.** One new endpoint; nothing existing
changes shape.

## Request

```
GET /api/teams/{slug}/happenings
```

| Part | Value |
|---|---|
| Auth | Required (global `FallbackPolicy`, feature 026). No `[AllowAnonymous]`. |
| `slug` | Team slug; normalised server-side via `TeamSlugPolicy.Normalize` |
| Query | **None.** No `skip`, no `take` — the result is hard-capped (spec FR-013) |
| Rate limit | Inherits the controller's existing policy; no new policy |

## Responses

| Status | When | Body |
|---|---|---|
| `200` | Caller is a member or admin of the team | `TeamHappeningDto[]` — **0 to 10 items**, newest first |
| `401` | Not signed in | Standard challenge |
| `404` | Slug unknown **or** caller is not a member | `TeamNotFound()` problem details — *deliberately identical for both*, so team existence is not disclosed |

### `200` body

A bare JSON array. **Not** a `PagedResult<T>` envelope — the endpoint does not paginate, and a
`totalCount` would advertise a "show more" the feature does not offer. See the plan's
[Complexity Tracking](../plan.md#complexity-tracking) for the constitution deviation this
represents and why it is justified.

```json
[
  {
    "kind": "TrainingSessionCancelled",
    "params": {
      "actorName": null,
      "recognitionName": null,
      "trainingName": "Tuesday practice",
      "sessionDate": "2026-08-18"
    },
    "linkTarget": "0198c4a1-5e2b-7c33-9f10-2a44bb01cd77",
    "occurredAt": "2026-08-11T09:14:02.318Z"
  },
  {
    "kind": "MemberJoined",
    "params": {
      "actorName": "Nik",
      "recognitionName": null,
      "trainingName": null,
      "sessionDate": null
    },
    "linkTarget": "nik-berlin",
    "occurredAt": "2026-08-09T17:02:51.004Z"
  },
  {
    "kind": "RecognitionAwarded",
    "params": {
      "actorName": null,
      "recognitionName": "Fair Play",
      "trainingName": null,
      "sessionDate": null
    },
    "linkTarget": null,
    "occurredAt": "2026-07-29T11:00:00.000Z"
  }
]
```

### Field contract

| Field | Type | Notes |
|---|---|---|
| `kind` | `string` enum | `MemberJoined` \| `RecognitionAwarded` \| `TrainingSeriesCreated` \| `TrainingSessionCancelled`. Serialized **by name**. Clients MUST ignore an unrecognised kind rather than render a blank line. |
| `params.actorName` | `string \| null` | `MemberJoined` only. `null` when the profile is suppressed or neutralised — the client substitutes a **translated** stand-in, never an English one (spec FR-024). |
| `params.recognitionName` | `string \| null` | `RecognitionAwarded` only. |
| `params.trainingName` | `string \| null` | Both training kinds. |
| `params.sessionDate` | `string \| null` | `TrainingSessionCancelled` only; ISO date (`YYYY-MM-DD`), no time. |
| `linkTarget` | `string \| null` | Handle, session id, or `null`. Meaning is kind-dependent — see [data-model §5](../data-model.md#5-link-targets). |
| `occurredAt` | `string` | ISO-8601 UTC. The domain moment, **not** the row's insertion moment. |

**The server never sends a rendered sentence.** It cannot: the viewer's language is a client-side
runtime choice (feature 031), so any prose composed here would be the wrong language and would be
invisible to the catalogue parity guard. See `ActivityParamsDto`'s doc comment.

## Guarantees

| # | Guarantee | Spec |
|---|---|---|
| G1 | Never more than **10** items | FR-011, SC-005 |
| G2 | No item's `occurredAt` is older than **30 days** | FR-011, SC-005 |
| G3 | Ordered `occurredAt` descending, with a total tie-break — two calls over unchanged data return the identical order | FR-015 |
| G4 | A non-member cannot obtain any item, and cannot distinguish "not a member" from "no such team" | FR-002, FR-003, SC-003 |
| G5 | Creating a recurring training series adds exactly **one** item, whatever the session count | D3, SC-004 |
| G6 | A revoked award, a departed member's join, and an un-cancelled session are absent on the next call | Edge cases |
| G7 | No item describes an event the team played | FR-008 |
| G8 | Departures and role changes never appear | D1, FR-009 |

## Unchanged endpoints

Explicitly **not** touched by this feature:

| Endpoint | Why it is listed |
|---|---|
| `GET /teams/{slug}/public` | Feeds the team page; its `recentActivity` array keeps its exact contents and cap of 6 (FR-016). Only the **heading** the client renders above it changes. |
| `GET /teams/{slug}/activity` | The paginated members-only **event** history. Out of scope (FR-018) — still `PagedResult<ActivityItemDto>`, still event-shaped, still without a caller in the app. |
| `GET /home` (`ActivityEntryDto`) | The dashboard feed. Byte-identical before and after (FR-027, SC-009). |
| `GET /profiles/{handle}/activity` | Profile event history. Untouched (FR-028). |
