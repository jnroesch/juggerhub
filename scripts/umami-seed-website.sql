-- Feature 033 — provision the tracked website row with a CHOSEN id, so measurement configures
-- itself and no one ever has to copy a UUID out of the dashboard.
--
-- WHY THIS EXISTS: Umami mints a random website id when you create a website in its UI, and the
-- tracker snippet has to embed that id. Taking the id FROM the dashboard makes it discovered
-- state, which forces a two-phase deploy — apply once with analytics off, log in, create the
-- website, copy the id into tfvars, apply again — and leaves every environment one manual step
-- away from silently measuring nothing.
--
-- Nothing requires the id to be Umami's choice. `website.website_id` is a plain uuid primary key
-- with no default, so WE pick it, per environment, and it becomes ordinary declarative config
-- like any other. First apply measures immediately, and if the volume is ever lost the row is
-- recreated with the SAME id, so the already-deployed snippet keeps working.
--
-- ORDERING — this is NOT part of umami-db-init.sql and must not be merged into it. That script
-- runs in an initContainer BEFORE Umami starts, when neither `website` nor `user` exists yet
-- (Prisma creates them on first run). This one runs AFTER Umami has migrated and seeded, from the
-- same post-deploy Job that writes the admin password hash, which already has that dependency.
--
-- Must be IDEMPOTENT: it re-runs on every deploy.
-- Run as the `umami` role, which owns these tables. The superuser is not needed here.
--
-- Usage:  psql -v website_id=<uuid> -v website_name=<name> -v website_domain=<host> \
--              -f umami-seed-website.sql

\set ON_ERROR_STOP on

-- `user_id` cannot be a constant the way `website_id` can: Umami generates the admin account's id
-- randomly when it seeds, so it differs per environment and must be looked up. The INSERT ... SELECT
-- also makes this self-guarding — if the admin account does not exist yet, it inserts nothing
-- rather than failing on a not-null violation, and the next deploy picks it up.
INSERT INTO website (website_id, name, domain, user_id, created_by, created_at)
SELECT :'website_id'::uuid, :'website_name', :'website_domain', u.user_id, u.user_id, now()
  FROM "user" u
 WHERE u.username = 'admin'
ON CONFLICT (website_id) DO NOTHING;

-- Keep the descriptive fields in step with configuration on later deploys.
UPDATE website
   SET name = :'website_name', domain = :'website_domain', updated_at = now()
 WHERE website_id = :'website_id'::uuid
   AND (name IS DISTINCT FROM :'website_name' OR domain IS DISTINCT FROM :'website_domain');

-- --- Session recording (feature 038) ----------------------------------------
-- A NOTE THAT USED TO SIT HERE said session replay stays off as a release gate. Feature 038 turns
-- it on as an owner decision (specs/038-umami-session-recording/spec.md); 033's quickstart
-- scenario 7 was amended to match rather than left to fail against a system working as designed.
--
-- The other half of that old note still holds, and it decides the shape of what follows: an
-- operator who changes something in the dashboard must not have it silently reverted by the next
-- deploy. THE DASHBOARD TOGGLE IS THE RUNTIME KILL SWITCH for recording. If it is switched off
-- during an incident, a deploy an hour later must not turn recording back on.
--
-- THE DASHBOARD OWNS THESE SETTINGS. Every one of them — the replay switch, the sample rate, the
-- mask level, the duration, the block selector — has a control in Umami's website settings, and an
-- operator has to be able to reach for any of them during an incident without waiting for a
-- deploy. So this file SEEDS A NEW WEBSITE and then gets out of the way.
--
-- That is why both statements below are shaped the way they are, and why an earlier version of
-- this block was wrong. It re-asserted the whole config on every deploy, as "drift protection".
-- Two things were wrong with that:
--
--   - It would silently undo the kill switch. Replay turned off at 02:00 would come back on with
--     the next deploy.
--   - For maskLevel it protected against a threat that cannot exist. The field has exactly two
--     values, 'strict' and 'moderate', and 'moderate' is the WEAKER one — so an operator can only
--     ever tighten it, and re-asserting 'moderate' could only ever undo that tightening. The same
--     goes for the sample rate and the block selector: every dashboard edit to those narrows what
--     is captured, or is a deliberate widening someone chose on purpose.
--
-- The rule that survives: A DEPLOY MAY NARROW WHAT IS CAPTURED, OR LEAVE IT ALONE. NEVER WIDEN IT.
-- Exactly one setting is re-asserted on every deploy, because it is the only one whose drift would
-- widen capture — see statement 2.
--
-- What none of this can affect: input masking happens in the browser and is on at BOTH mask
-- levels, so no value in this file, and no dashboard setting, can cause a password or a
-- half-written message to be captured.

-- 1. Seed a NEW website, so a fresh environment records from its first apply instead of waiting
--    for someone to notice. `replay_config IS NULL` is what "never configured" looks like; once
--    anything has been written — by this statement or by a human in the dashboard — it is a no-op
--    forever.
--
--    NO sampleRate here, deliberately. It is a dashboard slider like the rest, so a fresh website
--    starts on Umami's own default of 0.15 and whoever sets the environment up chooses the real
--    rate there. The alternative — seeding it from Terraform — meant carrying a variable that
--    could only ever apply once and would read like live configuration while being ignored.
UPDATE website
   SET recorder_enabled = true,
       replay_config    = jsonb_build_object(
           'replayEnabled', true,
           'maskLevel',     'moderate',
           'maxDuration',   300000,
           'blockSelector', ''
       ),
       updated_at = now()
 WHERE website_id = :'website_id'::uuid
   AND replay_config IS NULL;

-- 2. Heatmaps stay off, on every deploy. The ONE exception to "the dashboard owns it", because it
--    is the one setting whose drift collects MORE rather than less: heatmaps are a separate
--    mechanism writing to heatmap_event, they are disclosed nowhere in the privacy policy, and the
--    retention job does not sweep that table — so a stray click would start accumulating
--    undisclosed data that never expires. Turning them on is a decision that needs a policy change
--    first, not a toggle.
UPDATE website
   SET replay_config = coalesce(replay_config, '{}'::jsonb) || '{"heatmapEnabled": false}'::jsonb,
       updated_at    = now()
 WHERE website_id = :'website_id'::uuid
   AND replay_config->>'heatmapEnabled' IS DISTINCT FROM 'false';

-- Undo a soft delete, so a website removed in the UI comes back on the next deploy rather than
-- leaving the deployed snippet posting to an id Umami will reject.
UPDATE website SET deleted_at = NULL
 WHERE website_id = :'website_id'::uuid AND deleted_at IS NOT NULL;
