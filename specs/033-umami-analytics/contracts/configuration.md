# Contract: Configuration & Secrets

**Feature**: `033-umami-analytics`

Every new variable, where it comes from in each environment, and whether it is a secret. Per constitution "Secret & Configuration Management": local values come from `.env`, deployed values come from **GitHub Environments** via `TF_VAR_*`. **No Azure Key Vault.**

---

## Umami container

| Variable | Value | Secret? | Notes |
|---|---|---|---|
| `DATABASE_URL` | `postgresql://umami:<pw>@postgres:5432/umami` | **Yes** | Composed from the DB password secret. The only variable Umami strictly requires. |
| `APP_SECRET` | random ≥32 chars | **Yes** | Signs session tokens. A default or leaked value means forgeable dashboard sessions. |
| `COLLECT_API_ENDPOINT` | `/jh-insights/e` | No | **Required.** Must equal the public collection path in [nginx-routes.md](./nginx-routes.md). Rewrites the tracker script's embedded URL at container start. Without it the beacon posts to `/api/send` on our origin — already the .NET backend. |
| ~~`TRACKER_SCRIPT_NAME`~~ | — | — | **Deliberately not set — it has no effect in the Docker image.** Verified: Umami always serves `/script.js`. nginx does the renaming instead ([research.md](../research.md) §2). |
| `DISABLE_TELEMETRY` | `1` | No | FR-009 — no outbound call-home. |
| `DISABLE_UPDATES` | `1` | No | No update checks from a server-side app; version is pinned by us. |

**`BASE_PATH` is deliberately absent** — it is build-time only and would require forking Umami. See [research.md](../research.md) §1.

---

## Frontend container (new)

Consumed by the nginx template at container start.

| Variable | Value | Secret? |
|---|---|---|
| `JH_ANALYTICS_HEAD` | The snippet from [tracker-snippet.md](./tracker-snippet.md), or **empty to disable**. **No `'` may appear in it** — see [tracker-snippet.md](./tracker-snippet.md) | No |
| `JH_ANALYTICS_UPSTREAM` | `http://umami:3000` locally; **`http://umami.<namespace>.svc.cluster.local:3000` in Kubernetes** — the short name yields NXDOMAIN and a 502 on every tracker request, see [nginx-routes.md](./nginx-routes.md) | No |
| `JH_ANALYTICS_RESOLVER` | DNS server nginx resolves the upstream with: `127.0.0.11` locally (Docker's embedded DNS), the cluster DNS ClusterIP in Kubernetes | No |

`JH_ANALYTICS_RESOLVER` **has no safe empty default** — an unset value renders `resolver ;`, which is a config error — so every environment must set it. It exists because the analytics routes resolve their upstream per request rather than at startup; see [nginx-routes.md](./nginx-routes.md) for why that is mandatory.

All frontend placeholders are `JH_`-prefixed so `envsubst` cannot collide with nginx runtime variables — see [nginx-routes.md](./nginx-routes.md).

---

## Terraform variables

| Variable | Where it lives | Secret? | Dev | Prod |
|---|---|---|---|---|
| `umami_image` | `variables.tf` default | No | `docker.umami.is/umami-software/umami` | same |
| `umami_image_tag` | `variables.tf` default | No | pinned **`3.2.0`** — v3 dropped the `postgresql-`/`v` prefixes, so `postgresql-v3.2.0` does not exist | same |
| `umami_replicas` | `envs/*.tfvars` | No | `1` | `2` |
| `analytics_hostname` | `envs/*.tfvars` | No | `analytics-dev.juggerhub.com` | `analytics.juggerhub.com` |
| `umami_website_id` | `envs/*.tfvars` | **No** — ships in page source | per-env | per-env |
| `umami_app_secret` | GitHub Environments | **Yes** | | |
| `umami_db_password` | GitHub Environments | **Yes** | | |
| `umami_admin_password_hash` | GitHub Environments | **Yes** | | |

Only **sizing and hostnames** differ between environments — the resource set is identical, per constitution Principle V.

`umami_website_id` being non-secret matters practically: it is committed in tfvars, so it is reviewable and diffable, and a rotation is an ordinary PR rather than a secret update.

---

## GitHub Environments → deploy

Added to **both** the dev and prod jobs in [.github/workflows/deploy.yml](../../../.github/workflows/deploy.yml), alongside the existing `TF_VAR_postgres_password` / `TF_VAR_jwt_signing_key` / `TF_VAR_resend_api_key`:

```yaml
TF_VAR_umami_app_secret:          ${{ secrets.UMAMI_APP_SECRET }}
TF_VAR_umami_db_password:         ${{ secrets.UMAMI_DB_PASSWORD }}
TF_VAR_umami_admin_password_hash: ${{ secrets.UMAMI_ADMIN_PASSWORD_HASH }}
```

Both jobs, or the environment that was missed fails at apply with an unset-variable error.

---

## Local `.env`

Documented in `.env.sample`, defaulting to **off**:

| Variable | Default | Notes |
|---|---|---|
| `JH_ANALYTICS_HEAD` | *(empty)* | Off unless the developer opts in. |
| `UMAMI_APP_SECRET` | dev-only placeholder | Local only; never a real value. |
| `UMAMI_DB_PASSWORD` | `umami` | Local only. |
| `UMAMI_WEBSITE_ID` | *(empty)* | Filled after creating the local website. |

Umami itself only starts under the `analytics` compose profile — `docker compose --profile analytics up` — mirroring the existing `spam` profile for `spamd`. A plain `docker compose up` is unaffected (FR-019, SC-009).

---

## The admin password hash

`UMAMI_ADMIN_PASSWORD_HASH` is a **bcrypt hash**, generated once by the owner and stored in GitHub Environments. The plaintext never enters the repository, Terraform state, or the cluster.

Umami provides no environment variable for the admin password ([research.md](../research.md) §4), so a post-deploy Job writes this hash over the seeded `admin` account. The exact bcrypt cost and format Umami expects must be confirmed by inspecting a locally-seeded row — [research.md](../research.md) open item 4 — rather than guessed.

---

## Bootstrap ordering

**There is no bootstrap phase. A single apply produces a measuring environment.**

This contract previously specified a two-phase deployment — apply with analytics off, sign in, create the website, copy its generated ID into tfvars, apply again — on the premise that "website IDs do not exist until the website is created in the dashboard". That premise is wrong: `website.website_id` is a plain `uuid` primary key **with no default**, so the ID is ours to choose. Verified by inserting a row with a chosen UUID and confirming it accepts beacons and appears in the dashboard.

Choosing it turns measurement configuration from *discovered state* into ordinary declarative config:

| | Two-phase (rejected) | Provisioned ID |
|---|---|---|
| First apply | measures nothing | measures immediately |
| Manual steps | log in, create, copy, re-apply | none |
| Volume lost | ID changes; deployed snippet posts to a dead ID | same ID re-provisioned; snippet keeps working |
| Failure mode | silently measuring nothing until noticed | none of consequence |

The last row is the reason this matters beyond convenience. A two-phase bootstrap leaves every new environment one forgotten step away from silently recording nothing, and analytics is precisely the kind of system where nobody notices for weeks.

**How it is provisioned**: `scripts/umami-seed-website.sql`, run by the same post-deploy Job that writes the admin password hash (T028). It runs there rather than in the `db-init` initContainer because the `website` and `user` tables do not exist until Prisma has migrated and seeded — the initContainer runs before Umami has ever started. `user_id` cannot be a constant (Umami randomises the admin account's ID at seed time) so it is looked up by username, which also makes the insert self-guarding: if the admin account is not seeded yet it inserts nothing and the next deploy picks it up.

`umami_website_id` therefore has a **real value in `envs/*.tfvars` from the first apply**, and Terraform composes `JH_ANALYTICS_HEAD` from it — so the snippet's quoting is handled by Terraform rather than pasted by hand, which removes the single-quote hazard described in [tracker-snippet.md](./tracker-snippet.md) entirely.

Locally the same SQL runs as a one-shot compose service, so `docker compose --profile analytics up` self-provisions on a fresh clone. Verified from a dropped database and role.

Analytics-off is still available and still harmless: blank `JH_ANALYTICS_HEAD` and no tracker is served, regardless of what is provisioned.
