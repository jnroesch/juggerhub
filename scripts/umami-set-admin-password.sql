-- Feature 033 — overwrite Umami's seeded `admin` account password (FR-022).
--
-- WHY THIS EXISTS: Umami seeds a fixed, publicly documented admin/umami credential on first start
-- and offers NO environment variable to set the password (research.md §4). The dashboard is
-- reachable on a public hostname, and cert-manager publishes that hostname to Certificate
-- Transparency logs the moment it issues — which scanners watch precisely to find newly
-- provisioned hosts. So the credential is closed by the DEPLOY, in the same Job that provisions
-- the website, rather than by someone remembering to log in afterwards and racing automation.
--
-- The PLAINTEXT never reaches here. A bcrypt hash is generated once by hand and stored in the
-- GitHub Environment; this only writes that hash. Format confirmed against a locally seeded row
-- (T004): bcrypt `$2b$`, cost 10, `public."user"."password"` is varchar(60).
--
-- `user` is a RESERVED WORD in PostgreSQL and must stay double-quoted — unquoted it parses as the
-- current-user function and fails with a syntax error rather than a missing-table error, which
-- sends you looking in the wrong place.
--
-- Must be IDEMPOTENT: it re-runs on every deploy.
-- Run as the `umami` role, which owns this table.
--
-- Usage:  psql -v password_hash='<bcrypt hash>' -f umami-set-admin-password.sql

\set ON_ERROR_STOP on

-- The WHERE guard makes a re-run a genuine no-op (UPDATE 0) rather than a write that merely lands
-- on the same value, so `updated_at` stays meaningful as "when the credential last changed".
--
-- Deliberately NOT guarded on the account existing: if Umami has not seeded yet this updates
-- nothing and the next deploy applies it. That is safe because until the account exists there is
-- no default credential to be exposed either.
UPDATE "user"
   SET password = :'password_hash', updated_at = now()
 WHERE username = 'admin'
   AND password IS DISTINCT FROM :'password_hash';
