# Implementation Plan: Azure AKS Hosting & Infrastructure-as-Code

**Branch**: `015-hosting` | **Date**: 2026-07-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-hosting/spec.md`

## Summary

Stand up JuggerHub's deployed hosting as **one Terraform definition applied to many
environments** (Dev, Prod; Staging later) on **Azure Kubernetes Service (AKS)**.
Environments are selected with **Terraform workspaces + one `<env>.tfvars` each** and
differ **only** in values (node/VM size, backend replica count, Postgres disk,
autoscaling) — never in the set or shape of resources. Remote state lives in a
**single storage account/container inside a bootstrap resource group managed outside
Terraform**; workspaces keep one state per environment (`env:/dev/…`, `env:/prod/…`).

The cluster runs the same three workloads as local compose (minus Mailpit):
**backend** (.NET 10 API), **frontend** (Angular SPA on nginx, proxying `/api` and
the SignalR `/hubs` WebSocket), and **PostgreSQL 18 in-cluster** (StatefulSet + PVC).
Images come from **GHCR** via an `imagePullSecret`. Traffic enters through an
**ingress-nginx** controller bound to a **pre-allocated static Azure public IP** (one
per environment). The domain **`juggerhub.com`** is live: **Prod = `juggerhub.com`**
(+ `www` redirect), **Dev = `dev.juggerhub.com`**, Staging = `staging.juggerhub.com`.
DNS stays at the registrar (manual A records → each env's static IP); **cert-manager
issues per-host Let's Encrypt certificates via HTTP-01** and HTTP redirects to HTTPS.
Deployed **secrets/config come from GitHub Environments** into Kubernetes
Secrets/ConfigMaps — no Key Vault. This required a constitution amendment
(App Services → AKS), completed as **v1.2.0**.

## Technical Context

**Language/Version**: HCL (Terraform ≥ 1.9), Kubernetes manifests (YAML). App images
are the existing .NET 10 backend + Angular/Nx-on-nginx frontend + `postgres:18` — no
application-code changes in this feature.

**Primary Dependencies**: Terraform providers `azurerm` (~> 4.x), `kubernetes`
(~> 2.x), `helm` (~> 2.x); Azure resources — Resource Group, Virtual Network/Subnet,
AKS (managed cluster + system/user node pools), Public IP; in-cluster via Helm —
`ingress-nginx`, `cert-manager`; in-cluster via Kubernetes provider — Namespace,
Deployments (backend, frontend), StatefulSet (postgres), Services, Ingress, Secrets,
ConfigMaps, PVC/StorageClass, HPA (prod). Registry: GHCR.

**Storage**: PostgreSQL 18 **in-cluster** — StatefulSet with a `volumeClaimTemplate`
bound to an Azure Disk (`managed-csi` / `managed-csi-premium`) PVC; size is a per-env
variable. Data survives pod restart/reschedule via the retained PVC. (Backup/DR is a
separate future feature — out of scope here.)

**Testing**: `terraform validate` + `terraform fmt -check`; `terraform plan` per
workspace as a review gate; `tflint`/`checkov` static scan in CI; post-apply smoke:
frontend loads over the public IP, `/api/v1/health` returns 200, and a `/hubs`
WebSocket upgrades. See [quickstart.md](quickstart.md).

**Target Platform**: Azure Kubernetes Service (Standard tier, Linux node pools),
Azure Disk-backed persistent storage, Azure Load Balancer (public IP) via
ingress-nginx.

**Project Type**: Infrastructure-as-code for a web application (existing `backend/` +
`frontend/`); new top-level `infra/` tree.

**Performance Goals**: Low-traffic hobby-scale app. Dev = 1 node, 1 backend replica,
no autoscale. Prod = autoscaling user node pool (min 2), ≥ 2 backend replicas,
optional HPA. Rollouts are zero-downtime (RollingUpdate; failed pods never displace
the healthy ReplicaSet).

**Constraints**: Architecture identical across environments — differences are
variable values only (FR-002). No plaintext secrets in code/state (FR-018). State
backend RG is never touched by an apply (FR-013). Applying an unselected/`default`
workspace must not create resources (FR-014). Ingress must sustain WebSocket upgrades
for `/hubs` over `wss` (FR-009). Each env has a static public IP (FR-016a) and serves
HTTPS with auto-issued/renewed per-host certs, HTTP→HTTPS redirect (FR-017); HTTP-01
issuance self-heals once the registrar A record resolves (edge case: DNS not yet
pointed).

**Scale/Scope**: 1 new `infra/` root module + child modules (network, aks,
platform [ingress+cert-manager], app [k8s workloads]); 2 tfvars (`dev`, `prod`) +
`staging` later; 1 bootstrap script (state backend) run outside Terraform; CI wiring
in existing `deploy.yml`. No app source changes; one nginx note (trust proxy headers)
verified, not rewritten.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance |
|---|-----------|------------|
| I | Security-first, never trust the client | No new app surface. Secrets never enter Terraform code or state in plaintext — they are created as K8s Secrets from GitHub Environments at deploy (FR-018); Postgres password, JWT signing key, Resend key flow only as env-injected Secret refs. AKS API server access is IP-restricted (authorized ranges); Postgres is `ClusterIP`-only (never exposed via ingress/LB). Ingress terminates a single origin so httpOnly/SameSite cookies stay first-party. |
| II | Thin controllers, service-centric | N/A (no application code). Terraform mirrors the "thin composition over child modules" analogue: the root wires modules; each module owns one concern. |
| III | Disciplined data access | N/A (no EF changes). Postgres image/version and connection string are config; the app's data layer is unchanged. |
| IV | Secure auth & sessions | Unchanged app-side, and **strengthened by HTTPS from day one**: same-origin ingress + `X-Forwarded-Proto=https` let the backend issue `Secure` httpOnly cookies; HTTP→HTTPS redirect enforced at ingress; `/hubs` upgrades over `wss` end-to-end. |
| V | Environment parity & reproducible containerized deployments | **Core of this feature.** One definition → many environments via workspaces + per-env tfvars (FR-001..004); per-service Dockerfiles + compose (local) unchanged; GHCR retained; single state account with one state per env, RG outside Terraform (FR-012/013); no Key Vault (FR-018). Amended constitution v1.2.0 records App Services → AKS. |
| VI | Conventions & tooling | Scripts are **PowerShell only** — the state-backend bootstrap and any helpers are `.ps1` (no `.sh`). Frontend `.html`/`.css`/`.ts` separation untouched. |
| — | Quality gate 7 (UI review) | Not applicable — this feature ships **no UI**. |

**Result**: PASS. The one material governance item (App Services → AKS) is resolved
by amending the constitution (v1.2.0, done in this plan), as required by FR-023 — not
a violation to track. Complexity Tracking not needed.

## Project Structure

### Documentation (this feature)

```text
specs/015-hosting/
├── plan.md              # This file
├── research.md          # Phase 0 output — decisions & rationale
├── data-model.md        # Phase 1 output — environment/config & resource-topology model
├── quickstart.md        # Phase 1 output — bootstrap → provision → verify
├── contracts/
│   ├── terraform-modules.md   # Phase 1 — module input/output contracts
│   ├── state-backend.md       # Phase 1 — remote-state + bootstrap contract
│   └── k8s-workloads.md       # Phase 1 — in-cluster resource contract (ports, probes, secrets)
├── checklists/
│   └── requirements.md  # spec quality (done)
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
infra/                                   # NEW — all Terraform + K8s IaC
├── bootstrap/
│   └── New-TfStateBackend.ps1           # one-time, run OUTSIDE Terraform: RG + storage + container (FR-013)
├── envs/                                 # per-environment values only (FR-004)
│   ├── dev.tfvars                        # 1 node, 1 backend replica, small disk, no autoscale
│   ├── prod.tfvars                       # autoscaling pool, 2+ backend replicas, larger disk, HA
│   └── staging.tfvars.example            # template proving "add an env = add a file" (FR-003)
├── backend.tf                            # azurerm remote state (single account/container; workspace-keyed)
├── providers.tf                          # azurerm + kubernetes + helm providers (kube creds from aks module)
├── versions.tf                           # required_version + provider version pins
├── variables.tf                          # every per-env knob (sizes, counts, toggles, names)
├── main.tf                               # root: guards default workspace; wires child modules
├── locals.tf                             # env = terraform.workspace; naming; per-env app_hostname
├── outputs.tf                            # public IP, cluster name, ingress host, kubeconfig cmd
└── modules/
    ├── network/                          # VNet + subnet(s) + static Public IP(s) per env (FR-016a)
    ├── aks/                              # managed cluster + system/user node pools; outputs kubeconfig
    ├── platform/                         # Helm: ingress-nginx (bound to static IP) + cert-manager + LE ClusterIssuers
    └── app/                             # K8s: namespace, backend/ frontend Deploys, postgres StatefulSet+PVC,
                                          #      Services, Ingress (TLS + HTTP-01 cert, /, /api, /hubs), Secrets/ConfigMap, HPA(prod)

.github/workflows/
└── deploy.yml                            # EXTEND — build/push GHCR → terraform apply (per env) → rollout

# Unchanged, referenced only:
backend/Dockerfile, frontend/Dockerfile, frontend/nginx.conf, docker-compose.yml
```

**Structure Decision**: A single Terraform **root module** at `infra/` composed of
four child modules (network → aks → platform → app), parameterized entirely by
`variables.tf` and selected per environment with `terraform workspace` +
`envs/<env>.tfvars`. This is the minimal structure that satisfies "define once, run
many" (FR-001/003) while keeping per-env divergence to values. The `app` module holds
the Kubernetes workloads (mirroring `docker-compose.yml` minus Mailpit); `platform`
holds cluster-wide add-ons (ingress, cert-manager). Bootstrap of the state backend is
a **PowerShell script outside Terraform** so Terraform never manages its own backend
(FR-013, Principle VI).

## Complexity Tracking

Not applicable — Constitution Check passes with no violations (the App Services → AKS
change is handled by the v1.2.0 amendment, not carried as debt).
