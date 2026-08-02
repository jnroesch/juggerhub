# Contract: 30-Day Retention

**Feature**: `038-umami-session-recording` | **Requirements**: FR-012, FR-012a, SC-006

Umami v3.2.0 has no expiry column, no retention setting, and no cleanup job. The platform
has no automated deletion anywhere (GH #106). This is therefore **new capability**, and
spec FR-012a makes recording contingent on it: if this does not work, recording does not
ship.

The reason is not tidiness. The privacy policy will state "kept 30 days" (FR-016), and an
unenforced retention promise in a published legal document is worse than making no promise
at all.

---

## The statement

```sql
-- scripts/umami-replay-retention.sql
-- Deletes whole sessions, not individual chunks. See "Why by session" below.
WITH expired AS (
    SELECT session_id
    FROM   session_replay
    GROUP  BY session_id
    HAVING MAX(created_at) < NOW() - make_interval(days => :retention_days)
),
deleted_saved AS (
    DELETE FROM session_replay_saved s
    USING  session_replay r
    WHERE  r.session_id IN (SELECT session_id FROM expired)
      AND  s.visit_id = r.visit_id
)
DELETE FROM session_replay
WHERE  session_id IN (SELECT session_id FROM expired);
```

`:retention_days` is supplied by the job as a psql variable, defaulting to `30` — the same
mechanism 033 already uses in `umami-db-init.sql` and `umami-seed-website.sql`.

### Why by session, not by row

`session_replay` is chunked: one recording is many rows sharing a `session_id`, ordered by
`chunk_index`. The obvious statement —

```sql
DELETE FROM session_replay WHERE created_at < NOW() - INTERVAL '30 days';  -- WRONG
```

— deletes the *oldest chunks* of a session that straddles the cutoff, leaving a replay that
starts partway through with no indication anything is missing. Grouping by `session_id` and
comparing the newest chunk means a session is either wholly present or wholly gone.

The cost is a `GROUP BY` over an already-indexed column
(`session_replay_website_id_created_at_idx`, `session_replay_session_id_chunk_index_idx`),
run once a day on a small table.

### Why saved replays expire too

`session_replay_saved` names a visit the owner deliberately kept, and holds no events of
its own. If chunks expire and the saved row does not, the dashboard lists a replay that
cannot play. If the saved row *exempted* its chunks, "kept 30 days" would be false for
exactly the recordings someone found interesting enough to keep — which is the population
most likely to contain something sensitive.

Durable saved replays are a policy decision first and a schema change second. Not a default.

---

## Where it runs

| Environment | Mechanism |
|---|---|
| Dev, Prod | Kubernetes `CronJob` in `infra/modules/app/analytics.tf`, daily, running `psql` against the in-cluster Postgres |
| local | The same statement, available under the `analytics` compose profile |

Same mechanism in all three, differing only in schedule and whether it is running —
constitution V. It executes as the **existing scoped `umami` role** (033 FR-025): no new
credential, and no reach into the application database, which that role is explicitly
revoked from.

The Job carries the standard constraints: `concurrencyPolicy: Forbid` (a slow run must not
overlap the next), `restartPolicy: OnFailure`, and bounded history so completed pods do not
accumulate.

---

## Verification (SC-006)

Trusting the schedule is not verification. Quickstart scenario 7 checks it in two steps:

1. **The statement is correct** — insert `session_replay` rows with `created_at` backdated
   past the cutoff, plus a session straddling it and a saved replay; run the job; confirm
   the expired session and its saved row are gone, the straddling session is **wholly**
   present, and nothing else was touched.
2. **The schedule fires** — confirm the CronJob ran and completed in the cluster, not just
   that it exists.

Step 1 is the one that catches the chunk-truncation bug, and it cannot be replaced by
waiting 30 days.

---

## What this does *not* cover

- **`heatmap_event`** — heatmaps are off (`heatmapEnabled: false`). If they are ever
  enabled, that table needs its own rule; it is not swept by this job.
- **Page-view data** — 033's indefinite retention is unchanged and still correct: those
  records contain no personal data about the viewer, which is the basis it was chosen on.
  This job touches recordings only.
- **On-request deletion** (FR-015) — a member asking for their recordings to be removed is
  the manual route the policy already documents. Distinct from expiry, and note FR-015a:
  the request may concern recordings made by *another* member's browser, which this job
  does not help locate.

---

## Failure behaviour

A failed retention run is a **loud** failure, unlike everything else in this feature.
Recording itself is fire-and-forget and fails silently by design (Principle VII); retention
is the opposite, because its silent failure is a legal statement quietly becoming false.

The job's failure must be visible in the cluster's Job status rather than swallowed —
`restartPolicy: OnFailure` with a non-zero exit on SQL error, and `failedJobsHistoryLimit`
kept high enough that a run failing overnight is still visible the next morning.
