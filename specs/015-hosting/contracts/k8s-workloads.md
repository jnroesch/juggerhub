# Contract: In-Cluster Kubernetes Workloads

The resources the `app` module renders per environment. Mirrors `docker-compose.yml`
minus Mailpit. **Contract, not manifests.**

---

## Namespace

`juggerhub` (one per environment; environments are separate clusters/RGs, so the name
is constant). Add-ons (`ingress-nginx`, `cert-manager`) live in their own namespaces
(`platform` module).

---

## Backend — Deployment

| Aspect | Contract |
|--------|----------|
| Image | `ghcr.io/<owner>/juggerhub-backend:<image_tag>` + `imagePullSecrets: [ghcr]` |
| Replicas | `backend_replicas` (dev 1, prod ≥ 2) |
| Container port | 8080 |
| Env | `envFrom` app `Secret` + `ConfigMap`; keys per [data-model.md](../data-model.md) secret table (`ConnectionStrings__DefaultConnection`, `Jwt__*`, `Email__*`, `Admin__Emails`, `ASPNETCORE_ENVIRONMENT`) |
| Readiness/liveness | HTTP `GET /api/v1/health` on 8080 (matches compose healthcheck) |
| Rollout | `RollingUpdate`, `maxUnavailable: 0` ⇒ bad image never displaces healthy pods (FR-022) |
| HPA | only if `enable_backend_hpa` (prod): CPU target, min=`backend_replicas` |
| Exposure | `Service` type `ClusterIP` (name `backend`, port 8080) — reached via frontend proxy, never ingress-direct |

---

## Frontend — Deployment

| Aspect | Contract |
|--------|----------|
| Image | `ghcr.io/<owner>/juggerhub-frontend:<image_tag>` + `imagePullSecrets: [ghcr]` |
| Replicas | `frontend_replicas` |
| Container port | 80 (nginx) |
| Config | uses the image's baked `nginx.conf` which proxies `/api/` and `/hubs/` to `http://backend:8080` — **the Service must be named `backend`** so the existing upstream resolves |
| Readiness/liveness | HTTP `GET /` on 80 |
| Exposure | `Service` type `ClusterIP` (name `frontend`, port 80) ← Ingress target |

> **Integration note**: [frontend/nginx.conf](../../frontend/nginx.conf) hardcodes
> upstream `backend:8080`. The backend Service name **must** be `backend` in the same
> namespace, or the frontend proxy 502s. Verified, not rewritten.

---

## PostgreSQL — StatefulSet

| Aspect | Contract |
|--------|----------|
| Image | `postgres:18` (matches compose; pinned) |
| Replicas | 1 |
| Env | `POSTGRES_USER`, `POSTGRES_PASSWORD` (Secret), `POSTGRES_DB` |
| Storage | `volumeClaimTemplate` → PVC, `storageClassName = postgres_storage_class`, `resources.requests.storage = postgres_storage_gb`Gi; mounted at `/var/lib/postgresql` (Postgres 18 subdir convention, per compose comment) |
| Services | headless `postgres` (stable DNS) + `ClusterIP` for the connection string; **no ingress/LB — never publicly reachable** |
| Readiness | `pg_isready -U <user> -d <db>` |
| Persistence | PVC retained across pod restart/reschedule (FR-010); PVC reclaim policy `Retain` |

---

## Ingress (single origin)

| Aspect | Contract |
|--------|----------|
| Class | `ingress_class_name` (from platform) |
| Host | `app_hostname` — `juggerhub.com` (prod) / `dev.juggerhub.com` (dev) |
| Rule | `/` → `frontend:80` (frontend nginx proxies `/api`, `/hubs` onward) |
| WebSockets | annotations for `proxy-read-timeout`/`proxy-send-timeout` (≥ 3600s) so `/hubs` SignalR upgrades persist over `wss` (FR-009, SC-007) |
| TLS | `tls:` block with `secretName: <host>-tls` + annotation `cert-manager.io/cluster-issuer: <cluster_issuer>`; cert issued per-host via HTTP-01 (FR-017/SC-008) |
| HTTP→HTTPS | `nginx.ingress.kubernetes.io/ssl-redirect: "true"` — plain HTTP 308-redirects to HTTPS |
| www redirect (prod) | `nginx.ingress.kubernetes.io/from-to-www-redirect: "true"`; cert SANs include `www.juggerhub.com` |
| Issuance ordering | HTTP-01 needs `app_hostname` → static IP resolving first; cert-manager retries the `Challenge` until the registrar A record propagates, then issues — no re-apply (edge case: DNS not yet pointed) |

---

## Secrets & ConfigMap

| Object | Contents | Source |
|--------|----------|--------|
| `Secret` (app) | `postgres_password`, `jwt_signing_key`, `resend_api_key`, `email_from_address`, `email_frontend_base_url`, `admin_emails`, assembled `ConnectionStrings__DefaultConnection` | `sensitive` TF vars ← GitHub Environment (FR-018/019) |
| `Secret` (ghcr, `dockerconfigjson`) | GHCR pull creds | `ghcr_pull_token` ← GitHub Environment (FR-015) |
| `ConfigMap` | `Jwt__Issuer`, `Jwt__Audience`, `Email__Provider=Resend`, `ASPNETCORE_ENVIRONMENT`, non-secret settings | tfvars |

**Invariant**: no secret value appears in Terraform outputs, logs, or plaintext state
(SC-006). Rotation = update GitHub Environment secret + redeploy; no code change
(FR-020).

---

## Smoke-test contract (post-apply, FR/SC verification)

1. `kubectl rollout status` for backend, frontend, postgres → all Ready.
2. `kubectl -n juggerhub get certificate` → `READY=True` (cert issued, SC-008).
3. `GET http://<app_hostname>/` → 308 redirect to `https://` (SC-008).
4. `GET https://<app_hostname>/` → 200 over a valid/trusted cert, serves SPA (SC-007).
5. `GET https://<app_hostname>/api/v1/health` → 200 (backend+DB reachable).
6. WebSocket handshake to `wss://<app_hostname>/hubs/...` → 101 Switching Protocols
   (FR-009).
7. Postgres Service has **no** external IP / ingress rule (security invariant).
