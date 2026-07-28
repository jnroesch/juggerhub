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

-- Keep the descriptive fields in step with configuration on later deploys. Deliberately does NOT
-- touch recorder_enabled: session replay stays off (FR-038 release gate), and an operator who
-- turns something on in the UI should not have it silently reverted by the next deploy.
UPDATE website
   SET name = :'website_name', domain = :'website_domain', updated_at = now()
 WHERE website_id = :'website_id'::uuid
   AND (name IS DISTINCT FROM :'website_name' OR domain IS DISTINCT FROM :'website_domain');

-- Undo a soft delete, so a website removed in the UI comes back on the next deploy rather than
-- leaving the deployed snippet posting to an id Umami will reject.
UPDATE website SET deleted_at = NULL
 WHERE website_id = :'website_id'::uuid AND deleted_at IS NOT NULL;
