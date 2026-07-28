# Phase 1 Data Model: Self-Hosted Umami Analytics

**Feature**: `033-umami-analytics` | **Date**: 2026-07-28

This feature adds **no EF Core entities and no application schema**. Constitution Principle III (`BaseEntity`, UUIDv7, projections, pagination) governs application entities and does not apply here — Umami owns its own schema, created and migrated by Prisma inside its own database.

What this document defines is the **boundary**: what lives where, who may reach it, and the exact provisioning contract.

---

## 1. Database topology

One PostgreSQL instance — the existing StatefulSet in [infra/modules/app/main.tf](../../infra/modules/app/main.tf) — hosting two independent databases.

```text
postgres StatefulSet (existing, unchanged; 1 replica, ClusterIP/headless)
│
├── appdb                       owner: postgres (superuser, existing)
│   └── application schema, EF Core migrations
│         ▲
│         └── CONNECT explicitly REVOKED from PUBLIC   ← FR-025
│
└── umami        (NEW)          owner: umami (NEW, NOT a superuser)
      └── Umami schema, Prisma migrations, applied by Umami on start
```

No new PersistentVolumeClaim. Analytics shares the existing disk — the accepted trade-off recorded in the spec.

---

## 2. Roles and grants

| Principal | Type | May reach | Notes |
|---|---|---|---|
| `postgres` | superuser (existing) | everything | Unchanged. The application connects as this today. |
| `umami` | **new**, `LOGIN`, **not** superuser, **no** `CREATEDB`, **no** `CREATEROLE` | `umami` database only | Owns its database so Prisma can create and migrate its own tables. |

**FR-025 depends on an explicit revocation, not on a default.** PostgreSQL grants `CONNECT` to `PUBLIC` on every database by default, so a newly created role can connect to `appdb` unless told otherwise. The intuition that a fresh role "just can't see" other databases is wrong, and this is the single grant that makes the isolation claim true.

**It must be revoked from `PUBLIC`, not from `umami`.** Revoking from the role looks like the tighter, lower-blast-radius choice and is in fact a no-op: `REVOKE` only removes a grant actually made to that grantee, and `umami` is never granted `CONNECT` directly — it reaches `appdb` *through* the grant to `PUBLIC`, which a role-scoped revoke leaves untouched. The statement reports `REVOKE` and changes nothing, so the control appears to have been applied while the role still connects.

Confirmed by connecting as `umami` to `appdb` after running the role-scoped version: it succeeded. `\l appdb` shows the responsible grant as `=Tc/postgres`, where the **empty grantee before `=` is PUBLIC**.

Revoking from `PUBLIC` does not affect the application: it connects as the superuser that owns this database (`postgres` locally, `juggerhub` in Dev/Prod — both created by the postgres image as `POSTGRES_USER`). Superusers bypass privilege checks and the owner keeps its own grant. Only `CONNECT` is revoked from `PUBLIC`, not `ALL`, to keep the change to the application database as small as FR-025 allows.

---

## 3. Provisioning contract

Executed by an **initContainer** on the Umami Deployment (see [research.md](./research.md) §5 for why not `/docker-entrypoint-initdb.d/`, which would be a silent no-op on the already-initialised Dev and Prod volumes).

**Properties the script must have**:

| Property | Why |
|---|---|
| **Idempotent** | Runs on every pod start, restart, and rescheduling. |
| **Guarded `CREATE DATABASE`** | `CREATE DATABASE` cannot run inside a transaction or a `DO` block, so it needs the `\gexec` guard idiom rather than the `DO` block used for the role. |
| **Runs as superuser** | Creating roles and databases requires it. The superuser credential is read from the existing `postgres-secrets` Secret and is **never** given to the Umami container itself. |
| **Password from a Secret** | `TF_VAR_umami_db_password` → Kubernetes Secret → `psql` environment. Never in tfvars, never in the image, never in Terraform state as plaintext output. |
| **Ordered before Umami starts** | Prisma migrations fail against a missing database; an initContainer guarantees ordering without retry logic. |

**Operations, in order**:

1. Create role `umami` with `LOGIN`, if it does not exist — **without** the password, see below.
2. `ALTER ROLE umami … PASSWORD` unconditionally, so a rotated secret takes effect on the next roll rather than silently diverging.
3. Create database `umami` owned by `umami`, if it does not exist.
4. `REVOKE CONNECT ON DATABASE appdb FROM PUBLIC`  ← the statement the isolation claim rests on.
5. `REVOKE ALL ON DATABASE appdb FROM umami` — belt and braces, in case a direct grant is ever added by hand.
6. `REVOKE ALL ON DATABASE umami FROM PUBLIC` — the same protection in the other direction, so no future application-side role can read analytics data.

Step 2 is easy to omit and produces a confusing failure: rotating the password in GitHub Environments would change what Umami presents while the database still expects the old value.

Step 1 deliberately creates the role **without** a password. psql does not interpolate `:variables` inside a dollar-quoted `DO $$…$$` body — it passes the block to the server verbatim — so `CREATE ROLE umami LOGIN PASSWORD :umami_password` inside the existence guard fails with `syntax error at or near ":"`. Step 2 runs outside the block, where interpolation works, and has to run every time regardless; both statements share a session, so there is no window in which the role exists without a password.

---

## 4. Umami's own schema

Created and migrated by Prisma when the container starts. **Not modelled here and not managed by us** — treating a third-party application's internal schema as ours to define would make every upstream upgrade a merge conflict.

The tables that matter conceptually map to the spec's Key Entities:

| Spec entity | Umami concept | Notes |
|---|---|---|
| Tracked Site | `website` | One row per environment. Its `website_id` is what the tracker sends and what appears in page source — **not a secret**, so it lives in `envs/*.tfvars` rather than GitHub secrets. |
| Page View | `website_event` | Path, referrer, browser/OS/device, country, timestamp. |
| Visit | `session` | Derived from a rotating hash; no durable per-visitor identifier is stored, which is what makes FR-006 hold. |
| Dashboard Account | `user` | The seeded `admin` row, whose password hash is overwritten at deploy time (FR-022). |

### Fields that carry privacy weight

| Field | Requirement | Disposition |
|---|---|---|
| IP address | FR-005 (no full network address) | Umami hashes the IP into the session identifier and does not persist the address. Must be **confirmed by inspecting stored rows**, not assumed — quickstart scenario 6. |
| Page path (`url_path`) | FR-008 | **Recorded verbatim by owner decision**, including `/players/<handle>`. This is the one place the analytics store holds member-identifying data. |
| Account linkage | FR-005 | Umami's `identify()` / `data-tag` features are **not used**. Nothing links an event to a platform member. |
| Session replay | Release gate | Umami v3 introduced session replay. Must be **OFF** for every website. On an authenticated-only platform it would capture member data wholesale and invalidate every privacy claim in the spec. |

---

## 5. Retention

None configured. FR-003 sets a 12-month floor; the spec's assumption is indefinite retention, on the basis that no viewer-identifying data is stored.

**Caveat worth carrying forward**: that rationale was written before the FR-008 decision. The store *does* accumulate subject-side records of which member profiles were viewed and when. Indefinite retention of that is a larger commitment than indefinite retention of anonymous page counts. Not changed here — it is the owner's decision and it is recorded rather than quietly reinterpreted — but it belongs in the privacy-policy work tracked as **GH #92**.

Growth is not a capacity concern at this scale: page views in the hundreds to low thousands per day are megabytes per year.

---

## 6. What this feature does not touch

- No EF Core entity, migration, `DbContext`, or `appsettings` change.
- No change to the application's own Postgres connection, credentials, or StatefulSet spec beyond the initContainer's read of the existing secret.
- No new PVC, storage class, or volume.
- No application table read or written by Umami — enforced by §2 rather than by convention.
