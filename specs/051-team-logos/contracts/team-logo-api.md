# Contract: Team Logo API (051)

All routes are versioned `api/v1/…` and inherit `TeamsController`'s class-level `[Authorize]` —
teams are never anonymous (feature 026). Failures return RFC 7807 problem details with generic
messages only (Principle I). JSON is camelCase; enums are names.

Legend: **TA** = admin of this team (`TeamAccessGuard`) · **RL** = `[EnableRateLimiting("media-read")]`

---

## `PUT /teams/{slug}/logo` → `204` · **TA**

`multipart/form-data` with a single `file` part. Request size limit 8 MB, matching the avatar
endpoint.

| Outcome | Status |
|---|---|
| Stored | `204 No Content` |
| Caller is not an admin of the team | `403` — *"Only admins can change the team logo."* |
| Team unknown **or** caller is not a member | `404` — indistinguishable by design (FR-003) |
| No file, or an empty file | `400` — *"No image was provided."* |
| Rejected by the image pipeline | `400` with the processor's own non-technical reason |

The stored object is always a centre-cropped square WebP (FR-005), produced by the
`ImageProcessing:TeamLogo` profile. The original upload is discarded (FR-006). A rejection leaves
any existing logo untouched (FR-007).

## `DELETE /teams/{slug}/logo` → `204` · **TA**

Idempotent: removing a logo that is not there succeeds and changes nothing (FR-017). Deletes the
stored object (FR-018).

| Outcome | Status |
|---|---|
| Removed, or there was nothing to remove | `204 No Content` |
| Caller is not an admin of the team | `403` |
| Team unknown **or** caller is not a member | `404` |

## `GET /teams/{slug}/logo` → `200 image/webp` · signed-in, **not** member-gated · **RL**

Serves the bytes. Authenticated like every team read, but deliberately open to any signed-in
player rather than to members: browse lists teams to non-members, and FR-009 puts logos there.

Response headers come from the shared `MediaResponse` shaping, identical to avatars and catalogue
icons: `Cache-Control: private, no-cache` plus a strong `ETag` derived from a **hash** of the
object key (never the key itself), answering `304` when the caller already holds it.

**Every refusal is `404`** — no logo, unknown team, object missing, store unreachable. They are
deliberately indistinguishable so the endpoint is never an existence oracle, and so a client only
ever has to handle "image or no image" (FR-014).

---

## Read models

`hasLogo` is added to four existing payloads. No payload ever carries the object key or a storage
URL — the client builds `/api/v1/teams/{slug}/logo` from the slug it already has.

| DTO | Endpoint(s) | Surface |
|---|---|---|
| `TeamDetailDto` | `GET /teams/{slug}` | team settings, members' header |
| `TeamPublicDetailDto` | `GET /teams/{slug}/public` | team page header |
| `TeamCardDto` | `GET /teams` | browse rows, onboarding suggestions |
| `MyTeamDto` | `GET /profiles/me/teams`, `GET /home` | "My team" rows |

```jsonc
// TeamCardDto — logoInitial stays; it is the fallback, not a competitor
{ "slug": "rheinfeuer", "name": "Rheinfeuer", "location": { … },
  "playerCount": 14, "beginnersWelcome": true, "logoInitial": "R", "hasLogo": true }
```

### Chat

`ConversationAvatarDto.url` is no longer always `null` for a team conversation. For
`kind: "Team"`, and for `kind: "TeamInquiry"` **as seen by the asking player**, it carries
`/api/v1/teams/{slug}/logo` when the team has one, and stays `null` otherwise. The admin's side of
a team inquiry keeps the asking player's avatar URL (FR-010). Party and event conversations are
unchanged (FR-015).

```jsonc
{ "kind": "Team", "userId": null, "teamId": "0199…", "url": "/api/v1/teams/rheinfeuer/logo" }
```

---

## Configuration

```jsonc
"ImageProcessing": {
  "TeamLogo": { "ResizeMode": "SquareCrop", "MaxDimension": 512, "Quality": 80, "MaxOutputBytes": 524288 }
}
```

Safe built-in defaults, identical in shape across local/Dev/Prod (Principle V, Gate 6). The
shared `MaxInputBytes`, `MaxDecodePixels` and `AllowedContentTypes` are not per-profile and are
not changed by this feature. No secret is involved.
