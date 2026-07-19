---
description: "Task list for feature 015 — Azure AKS Hosting & Infrastructure-as-Code"
---

# Tasks: Azure AKS Hosting & Infrastructure-as-Code

**Input**: Design documents from `/specs/015-hosting/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No automated test suite is requested for this infrastructure feature.
Verification is `terraform validate`/`plan` + the post-apply smoke checks from
[quickstart.md](quickstart.md) and [contracts/k8s-workloads.md](contracts/k8s-workloads.md).
Those verification tasks are included per story.

**Organization**: Grouped by user story. Story order follows dependency: **US4
(remote state)** must exist before any `apply`, so it precedes **US1 (provision)**.
MVP = **US4 + US1** (a complete, reachable Dev environment).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1–US5 from spec.md
- All paths are repo-relative; the new tree lives under `infra/`.

## Path Conventions

New IaC tree at repo root: `infra/` (root module + `modules/` + `envs/` + `bootstrap/`).
CI lives in `.github/workflows/`. App source (`backend/`, `frontend/`) is referenced,
not modified.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the `infra/` tree and Terraform tooling.

- [ ] T001 Create the `infra/` tree skeleton — `infra/bootstrap/`, `infra/envs/`, `infra/modules/{network,aks,platform,app}/` each with placeholder `main.tf`/`variables.tf`/`outputs.tf`
- [ ] T002 [P] Add `infra/versions.tf` — `required_version >= 1.9` and pinned providers `azurerm ~> 4`, `kubernetes ~> 2`, `helm ~> 2`
- [ ] T003 [P] Add `infra/.tflint.hcl` and a Checkov/Trivy config for Terraform static analysis
- [ ] T004 [P] Add `.github/workflows/terraform-ci.yml` — PR gate running `terraform fmt -check`, `validate`, `tflint`, and `checkov` (no apply)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The root-module contract (variables, locals, providers, wiring skeleton)
every module and story depends on.

**⚠️ CRITICAL**: No story work can begin until this phase is complete.

- [ ] T005 Add `infra/variables.tf` — declare every per-env variable from [data-model.md](data-model.md) (sizing, counts, toggles, `app_hostname`, `letsencrypt_issuer`, `enable_*`) plus `sensitive = true` secret vars; add `validation` blocks (env ∈ {dev,prod,staging}, `backend_replicas ≥ 1`, `user_node_max ≥ user_node_min`, autoscale ⇒ max>min, non-empty secrets)
- [ ] T006 Add `infra/locals.tf` — `env = terraform.workspace`, `name_prefix`, per-env `app_hostname` resolution, `dns_a_records` map (incl. `www` on prod), common tags
- [ ] T007 Add `infra/providers.tf` — `azurerm` (`use_oidc`), and `kubernetes` + `helm` providers fed from the `aks` module's kube-credential outputs
- [ ] T008 Add `infra/main.tf` + `infra/outputs.tf` skeleton — module-call stubs (network→aks→platform→app) and root outputs (`ingress_public_ip`, `ingress_host`, `dns_a_records`, `cluster_name`, `kubeconfig_command`, `namespace`)

**Checkpoint**: `terraform init`/`validate` succeed against the skeleton.

---

## Phase 3: User Story 4 - Isolated, shared remote state (Priority: P1)

**Goal**: One shared state container with per-environment isolation, hosted in a RG
managed outside Terraform; unselected workspace cannot provision.

**Independent Test**: Bootstrap the backend, `init`, create `dev`+`prod` workspaces,
confirm two isolated `env:/…` state blobs in the one container, and that an apply on
the `default` workspace refuses.

- [ ] T009 [US4] Write `infra/bootstrap/New-TfStateBackend.ps1` — idempotent PowerShell that creates `rg-juggerhub-tfstate` + hardened storage account (`min_tls=TLS1_2`, no public blob, versioning + soft-delete) + `tfstate` container, and prints the names ([contracts/state-backend.md](contracts/state-backend.md)); it MUST never be referenced by any `.tf`
- [ ] T038 [US4] Write `infra/bootstrap/New-GitHubOidcServicePrincipal.ps1` — Entra app registration + service principal + **per-GitHub-Environment OIDC federated credentials** (no client secret) + role assignments (Contributor, User Access Administrator, Storage Blob Data Contributor on the state account); prints `AZURE_CLIENT_ID`/`_TENANT_ID`/`_SUBSCRIPTION_ID` for GitHub variables. Bootstrap step, runs alongside T009 (feature 015 §"Why a service principal")
- [ ] T010 [US4] Add `infra/backend.tf` — `backend "azurerm"` pointing at the bootstrap RG/account/container, `key = "juggerhub.tfstate"`, `use_oidc = true`
- [ ] T011 [US4] Add the workspace guard in `infra/main.tf` — a `check`/`precondition` asserting `contains(["dev","prod","staging"], terraform.workspace)` so `default` apply fails (FR-014)
- [ ] T012 [US4] Add `infra/README.md` — bootstrap + workspace lifecycle (`init`, `workspace new/select`, state isolation, OIDC auth)

**Checkpoint**: Isolated per-env state proven; MVP prerequisite ready.

---

## Phase 4: User Story 1 - Provision a complete environment (Priority: P1) 🎯 MVP

**Goal**: One `apply` turns an empty subscription (state backend present) into a
reachable, HTTPS Dev environment running backend + frontend + Postgres.

**Independent Test**: `workspace select dev` → apply → set the Dev A record → cert goes
`READY` → `https://dev.juggerhub.com` serves the SPA, `/api/v1/health` = 200, `/hubs`
upgrades over `wss`, and Postgres is `ClusterIP`-only.

- [ ] T013 [P] [US1] `infra/modules/network/` — RG, VNet, subnet, **static Standard Public IP**; outputs `subnet_id`, `resource_group_name`, `public_ip_address`, `public_ip_resource_group` (FR-016a)
- [ ] T014 [P] [US1] `infra/modules/aks/` — managed cluster (Standard, Azure CNI Overlay, system-assigned MI), separate **system** + **user** node pools, API `authorized_ip_ranges`; outputs kube host/certs + `cluster_name`, `node_resource_group`
- [ ] T015 [US1] `infra/modules/platform/` — Helm `ingress-nginx` bound to the static IP via `controller.service.loadBalancerIP` + `azure-load-balancer-resource-group` annotation; output `ingress_class_name` (depends on T013, T014)
- [ ] T016 [US1] `infra/modules/platform/` — Helm `cert-manager` + `letsencrypt-staging` and `letsencrypt-prod` `ClusterIssuer`s using the **HTTP-01** solver on the ingress class; output issuer names
- [ ] T017 [P] [US1] `infra/modules/app/` — `Namespace`, non-secret `ConfigMap`, app `Secret` (from `sensitive` vars incl. assembled `ConnectionStrings__DefaultConnection`), and GHCR `dockerconfigjson` pull `Secret` (FR-015/018/019)
- [ ] T018 [P] [US1] `infra/modules/app/` — Postgres `StatefulSet` with `volumeClaimTemplate` (`postgres_storage_class`/`_gb`), mount `/var/lib/postgresql`, `pg_isready` probe, headless + `ClusterIP` Services (never exposed) (FR-010)
- [ ] T019 [US1] `infra/modules/app/` — backend `Deployment` (image+`image_tag`, `imagePullSecrets`, `envFrom` Secret+ConfigMap, `/api/v1/health` readiness/liveness, `RollingUpdate maxUnavailable:0`) + `ClusterIP` Service **named `backend`** (nginx upstream requirement)
- [ ] T020 [US1] `infra/modules/app/` — frontend `Deployment` (image+`image_tag`, `imagePullSecrets`, `/` probe) + `ClusterIP` Service `frontend`
- [ ] T021 [US1] `infra/modules/app/` — `Ingress`: `app_hostname` `/` → `frontend`, `tls:` + `cert-manager.io/cluster-issuer` annotation, `ssl-redirect`, `from-to-www-redirect` (prod), `proxy-read/send-timeout ≥ 3600s` for `/hubs`; per-host `Certificate` (FR-009/016/017)
- [ ] T022 [US1] Wire all four modules in `infra/main.tf` (network→aks→platform→app), pass outputs through, and finalize `infra/outputs.tf`
- [ ] T023 [US1] Add `infra/envs/dev.tfvars` — minimal Dev values to provision (1 node, 1 backend/frontend replica, small `managed-csi` disk, `app_hostname=dev.juggerhub.com`, `letsencrypt_issuer=letsencrypt-staging`)
- [ ] T024 [US1] Validate Dev end-to-end per [quickstart.md](quickstart.md): `workspace new dev`, `apply`, set the A record, cert `READY=True`, HTTPS smoke (`/`, `/api/v1/health`, `wss /hubs`), Postgres `ClusterIP`-only; then flip `letsencrypt_issuer` to `letsencrypt-prod` and re-apply

**Checkpoint**: MVP — Dev is fully provisioned and reachable over HTTPS.

---

## Phase 5: User Story 2 - Add an environment without changing architecture (Priority: P1)

**Goal**: A second (Prod) and future (Staging) environment come up from the same
definition with only a new tfvars file.

**Independent Test**: Provision Prod from `prod.tfvars`; add `staging.tfvars` and plan
Staging with zero edits to `modules/` or root `*.tf`; `git diff --stat` shows only the
new tfvars.

- [ ] T025 [US2] Add `infra/envs/prod.tfvars` — Prod values (`app_hostname=juggerhub.com`, `enable_www_redirect=true`, `letsencrypt_issuer=letsencrypt-prod`, autoscaling pool, ≥2 replicas, `managed-csi-premium` disk)
- [ ] T026 [US2] Add `infra/envs/staging.tfvars.example` — template proving "add an env = add a file" (`staging.juggerhub.com`)
- [ ] T027 [US2] Provision Prod (`workspace new prod`, apply), set the apex + `www` A records; then verify adding Staging needs **no** module/root changes (`git diff --stat` shows only `envs/…`) and a `default`-workspace apply still refuses (SC-002/003, FR-014)

**Checkpoint**: Two live environments; adding a third is a one-file change.

---

## Phase 6: User Story 3 - Size each environment independently (Priority: P2)

**Goal**: Per-env sizing — Prod runs ≥2 backend replicas + autoscaling nodes + larger
disk; Dev runs one, no autoscale — driven solely by tfvars.

**Independent Test**: Compare live Dev vs Prod: replica counts, node autoscaling, and
Postgres disk differ, each matching its tfvars.

- [ ] T028 [US3] `infra/modules/aks/` — gate user-pool autoscaling on `enable_user_autoscale` with `user_node_min`/`_max` (Dev min==max ⇒ no autoscale; Prod scales) (FR-007)
- [ ] T029 [US3] `infra/modules/app/` — backend `HorizontalPodAutoscaler` gated by `enable_backend_hpa` (Prod), `minReplicas=backend_replicas`, CPU target
- [ ] T030 [US3] Verify sizing comes only from tfvars: `kubectl get deploy backend -o jsonpath` ≥2 on Prod / 1 on Dev; Prod user pool autoscales; Prod Postgres PVC is premium/larger (SC-004)

**Checkpoint**: Environments differ only in values, never in shape.

---

## Phase 7: User Story 5 - Continuous deployment through the pipeline (Priority: P2)

**Goal**: A merge builds/publishes images, applies the target env, and rolls out the
new images with secrets injected from GitHub Environments; a bad rollout never takes
down the serving version.

**Independent Test**: Merge a trivial change; watch `deploy.yml` build/push to GHCR,
apply the env with the new `image_tag`, and serve the new build; confirm a failing pod
leaves the prior version up.

- [ ] T031 [US5] Extend `.github/workflows/deploy.yml` — build + push `ghcr.io/<owner>/juggerhub-{backend,frontend}:<sha>`
- [ ] T032 [US5] `deploy.yml` — `az login` via OIDC, then `terraform init` → `workspace select <env>` → `apply -var-file=envs/<env>.tfvars -var image_tag=<sha>`, with secret `TF_VAR_*` mapped from the GitHub Environment (Dev/Prod)
- [ ] T033 [US5] `deploy.yml` — post-apply `kubectl rollout status` + smoke (cert `READY`, HTTPS `/api/v1/health`, `wss /hubs`); Dev auto-deploys on `main`, Prod gated by a GitHub Environment approval; confirm `RollingUpdate maxUnavailable:0` keeps the old version serving on a failed rollout (FR-021/022)

**Checkpoint**: Merges deliver to environments automatically and safely.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T034 [P] Finalize `infra/README.md` — architecture diagram, per-env hostnames, registrar A-record steps, and a note that DB backup/DR is a **deferred future feature**
- [ ] T035 [P] Document the required GitHub Environment secrets (Dev/Prod) and their mapping to the `sensitive` TF vars ([data-model.md](data-model.md) secret table)
- [ ] T036 Security hardening pass — Checkov/Trivy clean; assert no plaintext secrets in state/outputs; Postgres never exposed beyond `ClusterIP`; API `authorized_ip_ranges` set; storage account hardened
- [ ] T037 Run the full [quickstart.md](quickstart.md) validation end-to-end (Dev, and Prod if provisioning) — all SC-001…SC-009 checks pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)** → no dependencies.
- **Foundational (P2)** → after Setup; **blocks all stories**.
- **US4 (Phase 3)** → after Foundational; **prerequisite for every `apply`** (state backend).
- **US1 (Phase 4)** → after US4; the MVP provision.
- **US2 (Phase 5)** → after US1 (reuses the modules; adds envs).
- **US3 (Phase 6)** → after US1 (extends aks/app with autoscale/HPA); independently testable.
- **US5 (Phase 7)** → after US1 (needs a working apply path); can overlap US2/US3.
- **Polish (Phase 8)** → after the stories you intend to ship.

### Within Each User Story

- Module files in **different directories** (`network`, `aks`, `app`) can be authored
  in parallel; **root `main.tf` wiring (T022) is sequential** after them.
- `platform` (T015/T016) depends on `network` (IP) + `aks` (cluster).
- The `apply`/verify task closes each story (T024, T027, T030, T033, T037).

### Parallel Opportunities

- Setup: **T002, T003, T004** in parallel.
- US1 module authoring: **T013 (network), T014 (aks), T017 (app secrets), T018 (app postgres)** in parallel; then T015/T016 (platform), then T019/T020/T021 (app deploys/ingress), then T022 (wire).
- Polish: **T034, T035** in parallel.

---

## Parallel Example: User Story 1

```text
# Author independent modules together:
Task: T013 network module (RG/VNet/subnet/static IP)     -> infra/modules/network/
Task: T014 aks module (cluster + node pools)             -> infra/modules/aks/
Task: T017 app secrets/configmap/pull-secret             -> infra/modules/app/
Task: T018 app postgres StatefulSet + PVC + services     -> infra/modules/app/
# Then sequentially: T015/T016 platform -> T019/T020/T021 app deploys+ingress -> T022 wire -> T023 dev.tfvars -> T024 apply+smoke
```

---

## Implementation Strategy

### MVP (US4 + US1)

1. Phase 1 Setup → Phase 2 Foundational.
2. Phase 3 **US4** (state backend) — the hard prerequisite.
3. Phase 4 **US1** (provision Dev end-to-end).
4. **STOP & VALIDATE**: `https://dev.juggerhub.com` serves the app over a valid cert;
   `/hubs` works; Postgres is private. Demo.

### Incremental Delivery

- Add **US2** → Prod live + staging-in-one-file proof.
- Add **US3** → per-env sizing (Prod scales, Dev stays small).
- Add **US5** → merges auto-deploy with GitHub-Environment secrets.
- **Polish** → docs, secret catalog, security scan, full quickstart run.

---

## Notes

- No app source changes; the backend K8s Service **must** be named `backend` so the
  frontend image's baked `nginx.conf` upstream resolves.
- HTTP-01 first issuance needs DNS pointing first — cert-manager self-heals once the
  registrar A record resolves; no re-apply (see [research.md](research.md) §5).
- Start Dev on the Let's Encrypt **staging** issuer, then switch to **prod** to avoid
  rate limits during validation.
- Database backup/restore/DR is intentionally **out of scope** (its own future feature).
- Scripts are PowerShell only (Principle VI). Secrets flow from GitHub Environments;
  no Key Vault.
