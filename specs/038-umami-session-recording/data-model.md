# Data Model: Umami Session Recording

**Feature**: `038-umami-session-recording`

No entity is added to the application database. Everything here lives in the **`umami`
database**, owned by the scoped `umami` role established in 033, and is created by
migrations already applied by the `umami:3.2.0` image. This document records the shape
because retention (FR-012) and configuration (FR-006) act directly on it.

**No `backend/` change, no EF entity, no application migration.** Constitution Principle
III (`BaseEntity`, UUIDv7, projections) does not apply — this is a third-party schema we
read and delete from, never one we define.

---

## Boundary

| Owns | Schema | Reached by |
|---|---|---|
| Application | `public` in `appdb` | EF Core, as always |
| Umami | `public` in `umami` | Umami's own Prisma client; **and**, new in this feature, one scheduled `DELETE` |

The retention job is the **first thing this repository writes to Umami's schema**. It is a
delete-only statement against two tables, run as the same scoped role, and it never
touches `appdb` — the role is explicitly revoked from it (033 FR-025).

---

## `website` — two columns carry the privacy behaviour

Existing row, seeded per environment by `scripts/umami-seed-website.sql` (033). Recording
adds two columns to the seed:

| Column | Type | Meaning here |
|---|---|---|
| `recorder_enabled` | `BOOLEAN NOT NULL DEFAULT false` | Master switch. `false` ⇒ the config endpoint answers `{"enabled": false}` and the recorder stops before capturing anything. Renamed from `replay_enabled` by migration `20_add_heatmap`. |
| `replay_config` | `JSONB` | The settings below. |

`replay_config` keys, validated by Umami's `getRecorderConfig` — anything else is
discarded, and an invalid `maskLevel` silently falls back to `moderate`:

| Key | Type | Default if absent | This feature |
|---|---|---|---|
| `replayEnabled` | `boolean` | `false` | `true` |
| `heatmapEnabled` | `boolean` | `false` | **`false`** — separate capture mechanism, out of scope |
| `sampleRate` | `number` | `0.15` | **`1`** — every session (spec Assumptions) |
| `heatmapSampleRate` | `number` | `0.15` | irrelevant while heatmaps are off |
| `maskLevel` | `'strict' \| 'moderate'` | `'moderate'` | **`'moderate'`** — FR-006 |
| `maxDuration` | `number` (ms) | `300000` | `300000` (5 min) |
| `blockSelector` | `string` | `''` | `''` — unused; the FR-006a escape hatch |

**Why this is a data-model concern and not a settings note**: these columns *are* the
enforcement of FR-005, FR-006, and the sampling assumption. They are editable from the
dashboard by anyone who can log in, with no release and no git trace. Seeding them is what
makes the spec's masking requirement true of the running system.

---

## `session_replay` — the recordings

```sql
CREATE TABLE "session_replay" (
  "replay_id"   UUID NOT NULL PRIMARY KEY,
  "website_id"  UUID NOT NULL,
  "session_id"  UUID NOT NULL,
  "visit_id"    UUID NOT NULL,
  "chunk_index" INTEGER NOT NULL,
  "events"      BYTEA NOT NULL,
  "event_count" INTEGER NOT NULL,
  "started_at"  TIMESTAMPTZ(6) NOT NULL,
  "ended_at"    TIMESTAMPTZ(6) NOT NULL,
  "created_at"  TIMESTAMPTZ(6) DEFAULT CURRENT_TIMESTAMP
);
```

Indexed on `website_id`, `session_id`, `(website_id, session_id)`,
`(website_id, visit_id)`, `(website_id, created_at)`, `(session_id, chunk_index)`.

**One recording is many rows.** Chunks share a `session_id` and are ordered by
`chunk_index`; `events` holds the serialised rrweb payload. Three consequences:

1. **Retention must delete by session, not by row** — see [contracts/retention.md](./contracts/retention.md).
   Deleting rows older than the cutoff would truncate the beginning of a session that
   straddles it, leaving a replay that starts in the middle.
2. **`events BYTEA` is the storage growth** (FR-030). It is the only column that scales
   with usage, and it sits in the instance shared with the application.
3. **`(website_id, created_at)` is already indexed**, so the retention query has the index
   it needs without adding one to a third-party schema.

### What is *not* in here

No account id, no username, no email, no IP address — consistent with FR-008. The
identifying content is **inside `events`**, because it is a picture of the screen. That is
the whole substance of FR-022: the schema looks anonymous and the payload is not.

---

## `session_replay_saved` — deliberately kept replays

```sql
CREATE TABLE "session_replay_saved" (
  "saved_replay_id" UUID NOT NULL PRIMARY KEY,
  "name"            VARCHAR(100) NOT NULL,
  "website_id"      UUID NOT NULL,
  "visit_id"        UUID NOT NULL,
  "created_at"      TIMESTAMPTZ(6) DEFAULT CURRENT_TIMESTAMP,
  "updated_at"      TIMESTAMPTZ(6),
  UNIQUE ("website_id", "visit_id")
);
```

A pointer, by `visit_id`, to a replay the owner named and kept. It holds **no events** —
the payload stays in `session_replay`.

**Retention decision, taken here because the schema forces it**: saved replays expire on
the same 30-day clock and the saved row is deleted with the chunks. Two reasons — a saved
row outliving its chunks points at nothing, and an unbounded "save" would otherwise be a
retention bypass that makes the policy's "kept 30 days" untrue for exactly the recordings
someone found interesting enough to keep. If the owner later wants durable saved replays,
that is a policy change first and a schema change second, not a default.

---

## `heatmap_event` — present, unused

Created by migration `20_add_heatmap`. Populated only when `heatmapEnabled` is `true`,
which this feature keeps `false`. Listed so that a future reader finds it named rather
than discovering it in the schema and wondering whether recording writes there.

If heatmaps are ever enabled, `heatmap_event` needs its own retention rule — it is not
covered by this feature's job, and it is a separate capture mechanism with its own
disclosure obligation.

---

## Relationships

```text
website (1) ──< session_replay (many chunks per session)
   │                  │
   │                  └── session_id ──> Umami's own session/visit records (033)
   │
   └──< session_replay_saved (0..1 per visit_id, UNIQUE(website_id, visit_id))
```

`session_id` and `visit_id` are Umami's existing rotating, server-derived identifiers from
033 — they do not persist on the device (FR-021) and are not linked to a platform account.
