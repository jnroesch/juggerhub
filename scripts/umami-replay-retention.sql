-- Feature 038 — delete session recordings older than the retention period.
--
-- WHY THIS EXISTS: Umami v3.2.0 has no expiry column, no retention setting and no cleanup job of
-- its own, and this platform has no automated deletion anywhere else either (GH #106). The privacy
-- policy states recordings are kept 30 days. Without this file that sentence is simply untrue, and
-- an unenforced retention promise in a published legal document is a worse position than making no
-- promise at all. Feature 038 FR-012a therefore makes recording contingent on this working.
--
-- Recordings are personal data about the viewer: a recording depicts the screen, and on an
-- authenticated-only platform the screen carries the member's name. That is why 033's indefinite
-- retention does NOT extend here — 033 chose indefinite explicitly on the basis that page views
-- contain no personal data, and that basis does not survive session replay.
--
-- Must be IDEMPOTENT: it runs daily and must be safe to run twice, or never, or after a gap.
-- Run as the `umami` role, which owns these tables. The superuser is not needed, and the role is
-- explicitly revoked from the application database, so this cannot touch application data.
--
-- Usage:  psql -v retention_days=30 -f umami-replay-retention.sql

\set ON_ERROR_STOP on

-- DELETE BY SESSION, NOT BY ROW. This is the whole subtlety of the file.
--
-- session_replay is CHUNKED: one recording is many rows sharing a session_id, ordered by
-- chunk_index. The obvious statement —
--
--     DELETE FROM session_replay WHERE created_at < now() - interval '30 days';   -- WRONG
--
-- deletes the OLDEST CHUNKS of a session that straddles the cutoff, leaving a recording that
-- starts partway through with nothing to indicate anything is missing. Someone watching it would
-- reasonably conclude the member arrived mid-flow. Grouping by session_id and comparing the NEWEST
-- chunk means a session is either wholly present or wholly gone.
--
-- The cost is a GROUP BY over session_replay_website_id_created_at_idx, once a day, on a table
-- bounded by this very job.
--
-- SAVED REPLAYS EXPIRE TOO. session_replay_saved names a visit the owner deliberately kept and
-- holds no events itself. Leaving a saved row behind would list a replay that cannot play; letting
-- it EXEMPT its chunks would make "kept 30 days" false for exactly the recordings someone found
-- interesting enough to keep — the population most likely to contain something sensitive. Durable
-- saved replays are a policy decision first and a schema change second, not a default.
WITH expired AS (
    SELECT session_id
      FROM session_replay
     GROUP BY session_id
    HAVING MAX(created_at) < now() - make_interval(days => :'retention_days'::int)
),
-- Collected before the chunks are deleted: session_replay_saved is keyed by visit_id, and after
-- the DELETE below there would be nothing left to resolve a session_id to its visits.
expired_visits AS (
    SELECT DISTINCT r.visit_id
      FROM session_replay r
      JOIN expired e ON e.session_id = r.session_id
),
deleted_saved AS (
    DELETE FROM session_replay_saved s
     USING expired_visits v
     WHERE s.visit_id = v.visit_id
    RETURNING 1
)
DELETE FROM session_replay r
 USING expired e
 WHERE r.session_id = e.session_id;

-- Heatmaps are deliberately NOT swept here. heatmapEnabled is false, so heatmap_event stays empty;
-- if heatmaps are ever enabled they need their own retention rule and their own disclosure, and
-- inheriting this one silently would be the wrong kind of convenient.
