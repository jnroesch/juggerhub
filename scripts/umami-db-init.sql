-- Feature 033 — provision the `umami` database and its scoped role on the EXISTING Postgres
-- instance. Applied by the db-init initContainer on the Umami Deployment (Dev/Prod) and by hand
-- locally; see specs/033-umami-analytics/data-model.md §3.
--
-- WHY NOT /docker-entrypoint-initdb.d/ : the stock postgres image runs those scripts ONLY when
-- initialising an EMPTY data directory. Dev and Prod already have populated volumes, so a script
-- placed there would be a silent no-op — the deploy would look fine and Umami would fail to
-- connect, with nothing pointing at the cause. An initContainer runs on every pod start instead.
--
-- Must be IDEMPOTENT: it re-runs on every pod start, restart, and reschedule.
-- Run as the Postgres superuser. The superuser credential is NEVER given to the Umami container.
--
-- Usage:  psql -v umami_password=<password> -v app_db=appdb -f umami-db-init.sql

\set ON_ERROR_STOP on

-- 1. Role. CREATE ROLE is not idempotent, so guard it; then always re-assert the password so a
--    rotated secret takes effect on the next roll. Omitting the ALTER is an easy mistake with a
--    confusing symptom: the secret changes, Umami starts presenting the new password, and the
--    database still expects the old one.
--
--    The password is deliberately NOT set here. psql does NOT interpolate :variables inside a
--    dollar-quoted $$...$$ body — it passes the block through verbatim — so `PASSWORD
--    :umami_password` would reach the server literally and fail with `syntax error at or near
--    ":"`. The ALTER below runs outside the block, where interpolation does work, and it has to
--    run every time regardless. So the role is created without a password and immediately given
--    one; there is no window in between, as both statements are in the same session.
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'umami') THEN
    CREATE ROLE umami LOGIN;
  END IF;
END
$$;

-- :'...' makes psql quote the value as a string literal, so the password never has to arrive
-- pre-quoted and an awkward character cannot break out of the statement.
ALTER ROLE umami WITH LOGIN PASSWORD :'umami_password';

-- Belt and braces: never a superuser, cannot create databases or roles (FR-025).
ALTER ROLE umami WITH NOSUPERUSER NOCREATEDB NOCREATEROLE;

-- 2. Database. CREATE DATABASE cannot run inside a transaction or a DO block, so it needs the
--    \gexec guard idiom rather than the DO block used above.
SELECT 'CREATE DATABASE umami OWNER umami'
 WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'umami')\gexec

-- 3. Isolation from application data (FR-025).
--
--    This is the statement the isolation claim actually rests on. PostgreSQL grants CONNECT on
--    every database to PUBLIC by default, so a freshly-created role CAN connect to the application
--    database until told otherwise. The intuition that a new role "just can't see" other databases
--    is wrong, and quickstart Scenario 9 exists to prove this took effect.
--
--    IT MUST BE REVOKED FROM **PUBLIC**, NOT FROM `umami`.
--
--    Revoking from `umami` looks equivalent and is not: REVOKE only removes a grant that was
--    actually made to that grantee, and `umami` was never granted CONNECT directly — it reaches
--    the database through the implicit grant to PUBLIC, which a role-specific REVOKE leaves fully
--    intact. The role-scoped version therefore reports `REVOKE` and changes nothing, which is the
--    worst possible failure mode: the isolation control appears to have been applied.
--    Verified by connecting as `umami` to the application database; `\l` shows the PUBLIC grant as
--    the `=Tc/postgres` entry, where the empty grantee before `=` IS PUBLIC.
--
--    Safe for the application: it connects as the superuser that owns this database (`postgres`
--    locally, `juggerhub` in Dev/Prod, both created by the postgres image as POSTGRES_USER).
--    Superusers bypass privilege checks and the owner keeps its own grant, so neither is affected.
--    Only CONNECT is revoked here — not ALL — to keep the blast radius on the application database
--    as small as the requirement allows.
REVOKE CONNECT ON DATABASE :"app_db" FROM PUBLIC;

--    Belt and braces, in case a direct grant is ever added by hand.
REVOKE ALL ON DATABASE :"app_db" FROM umami;

--    Same protection in the other direction: no future application-side role should be able to
--    read analytics data either. `umami` owns this database, so it is unaffected.
REVOKE ALL ON DATABASE umami FROM PUBLIC;
