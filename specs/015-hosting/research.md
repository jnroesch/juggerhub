# Phase 0 Research: Azure AKS Hosting & IaC

All items below were decided with the user during intake (see spec Assumptions) or
resolved here against Azure/Terraform/Kubernetes best practice. No open
`NEEDS CLARIFICATION` remain.

---

## 1. Multi-environment strategy — workspaces + per-env tfvars

**Decision**: One Terraform root module; select environment with
`terraform workspace select <env>` and apply with `-var-file=envs/<env>.tfvars`.
`local.env = terraform.workspace` drives naming and per-env values.

**Rationale**: Satisfies "define once, run many" (FR-001/003) with the least
duplication — adding Staging is one new `.tfvars` + `terraform workspace new staging`,
zero changes to resource definitions (FR-003, SC-002). Matches the constitution's
existing "one state file per environment" model: the `azurerm` backend stores
workspace state under `env:/<workspace>/<key>` in the **same** container, so all
environments share one account/container yet stay isolated (FR-012, SC-005).

**Guard**: `default` workspace must not provision. `main.tf` includes a
`terraform_data`/`precondition` (or `check`) asserting
`contains(["dev","prod","staging"], terraform.workspace)` so a forgotten
`workspace select` fails fast instead of creating an unnamed environment (FR-014).

**Alternatives considered**:
- *Directory-per-env (thin roots calling shared modules)* — explicit and allows
  divergence, but the user chose workspaces; more duplication and drift risk.
- *Terragrunt* — DRY but adds a tool/learning curve the user declined.

---

## 2. Remote state backend — static, bootstrapped outside Terraform

**Decision**: A one-time **PowerShell** script (`infra/bootstrap/New-TfStateBackend.ps1`)
creates a Resource Group + Storage Account + one blob **container** via `az`/`Az` CLI.
Terraform's `backend "azurerm"` points at that container with a single `key`
(e.g. `juggerhub.tfstate`); workspaces prefix it automatically.

**Rationale**: FR-013 requires the state's hosting RG to be managed **outside** the
definition and never touched by an apply — a bootstrap script is the standard
chicken-and-egg resolution (Terraform can't host its own backend before it exists).
PowerShell-only per Principle VI. Storage hardened: versioning + soft-delete on,
public blob access off, `min_tls_version = TLS1_2`.

**Auth**: Backend + provider authenticate via the CI's Azure identity
(OIDC federated credential from GitHub Actions → an Azure AD app / user-assigned
managed identity). No stored client secrets; nothing in tfvars.

**Alternatives considered**: Terraform-managed backend (rejected — violates FR-013);
Terraform Cloud/remote backend (rejected — the constitution mandates an Azure storage
account).

---

## 3. Cluster — AKS Standard tier, two node pools

**Decision**: AKS **Standard** tier managed cluster. **System** node pool (small,
`CriticalAddonsOnly` taint optional, min 1) runs cluster/system pods; **user** node
pool runs app workloads. VM size and counts are per-env variables; `agents_count`
fixed on Dev, `enable_auto_scaling` + min/max on Prod (FR-005/007).

**Rationale**: Separating system and user pools is the AKS reliability baseline and
lets Prod autoscale app capacity without churning system pods (SC-004). Standard tier
(vs. Automatic) keeps cost/control appropriate for a small app and gives explicit
node-pool sizing — the exact "smaller machines on dev" lever the user asked for.

**Networking**: **Azure CNI Overlay** with `cilium`/`azure` — pods get overlay IPs,
no VNet IP exhaustion, minimal address planning; a single small VNet/subnet suffices.
Outbound via managed load balancer. API server: **authorized IP ranges** enabled
(CI egress + operator IPs) rather than a fully private cluster, to keep CI/kubectl
simple without standing up a bastion/private endpoint.

**Identity**: cluster uses a **system-assigned managed identity**; `kubelet` identity
used only for any future ACR — GHCR uses an imagePullSecret instead (§6).

**Alternatives considered**: AKS Automatic (rejected — less sizing control, higher
floor cost); kubenet (legacy); fully private API server (deferred — adds bastion/CI
complexity beyond this feature's needs).

---

## 4. Ingress — ingress-nginx on a pre-allocated static public IP

**Decision**: Install **ingress-nginx** via Helm in the `platform` module, bound to a
**static Azure Public IP** (Standard SKU) that Terraform pre-allocates in the
`network` module — one per environment (FR-016a). The IP is handed to the controller
via `controller.service.loadBalancerIP` + the
`service.beta.kubernetes.io/azure-load-balancer-resource-group` annotation when the IP
lives outside the AKS-managed node RG. One `Ingress` routes `/` → **frontend** Service;
the app's existing `nginx.conf` proxies `/api` and `/hubs` to the backend — preserving
the exact same-origin topology as local compose (one origin, first-party cookies, no
CORS) with the fewest moving parts.

**Why a pre-allocated static IP**: DNS is registrar-managed with manual A records
(user decision), so the IP must be stable across deploys/controller re-creation — an
auto-assigned LB IP would break DNS and certs on any churn.

**WebSockets**: ingress-nginx supports WS upgrades natively; the frontend nginx block
for `/hubs` already sets `Upgrade`/`Connection` + `proxy_read_timeout 3600s`
([frontend/nginx.conf](../../frontend/nginx.conf)). Annotation
`nginx.ingress.kubernetes.io/proxy-read-timeout` set high for long-lived hubs; over
HTTPS these become `wss` (FR-009, SC-007).

**Hosts (domain live)**: per-env hostname is a variable —
`prod → juggerhub.com` (+ `www.juggerhub.com`), `dev → dev.juggerhub.com`,
`staging → staging.juggerhub.com`. Prod redirects `www` → apex via
`nginx.ingress.kubernetes.io/from-to-www-redirect: "true"`.

**Alternatives considered**: AGIC/Application Gateway (heavier, pricier); Service
`type=LoadBalancer` per app (loses single-origin + path routing); auto-assigned LB IP
(rejected — incompatible with manual DNS).

---

## 5. TLS — cert-manager, per-host HTTP-01, active from day one

**Decision**: Install **cert-manager** (Helm) in `platform` with two
`ClusterIssuer`s — **Let's Encrypt staging** (for smoke/validation) and **production**
— both using the **HTTP-01** solver via the ingress-nginx class. The app `Ingress`
carries a `tls:` block and the `cert-manager.io/cluster-issuer` annotation for its
hostname(s); ingress-nginx enforces **HTTP → HTTPS redirect** (`ssl-redirect: "true"`,
default once TLS is present). `enable_tls` defaults **true**; a per-host Certificate is
issued (Prod cert SANs cover `juggerhub.com` + `www.juggerhub.com`).

**Rationale**: FR-017/SC-007/SC-008. HTTP-01 needs no DNS credentials and works with
registrar-managed DNS — the tradeoff is no wildcard (fine: hostnames are known per
env). Certs auto-renew ~30 days before expiry with no downtime (edge case: renewal).

**Ordering / self-heal (registrar DNS + HTTP-01)**: HTTP-01 validation requires the
hostname to already resolve to the env's static IP. Flow: `apply` → read
`ingress_public_ip` output → create the A record at the registrar → cert-manager's
`Order`/`Challenge` retries until DNS propagates, then issues — **no re-apply**
(edge case: DNS not yet pointed). Start against the **staging** issuer to avoid
Let's Encrypt rate limits while validating, then switch to **production** via the
`letsencrypt_issuer` variable.

**Alternatives considered**: DNS-01/wildcard (needs Azure DNS delegation — user kept
DNS at registrar); Azure-managed certs (needs App Gateway/Front Door); self-signed
(browser warnings).

---

## 6. Registry & image pull — GHCR via imagePullSecret

**Decision**: Keep **GHCR** (constitution). The `app` module creates a
`kubernetes_secret` of type `kubernetes.io/dockerconfigjson` from a
GitHub-Environments-provided GHCR token (a PAT or `GITHUB_TOKEN` with `read:packages`),
referenced by each Deployment's `imagePullSecrets`. Images are tagged by commit SHA.

**Rationale**: No Azure registry cost, no constitution change, and CI already builds
in GitHub Actions. FR-015. Pull failures surface as `ImagePullBackOff` — the smoke
step treats non-Ready pods as a hard failure (edge case: registry auth).

**Alternatives considered**: ACR + AKS managed-identity attach (cleaner pull, but adds
cost + a constitution change the user declined).

---

## 7. PostgreSQL — in-cluster StatefulSet + PVC

**Decision**: `postgres:18` (matching compose) as a **StatefulSet** (replicas: 1) with
a `volumeClaimTemplate` → Azure Disk PVC (`managed-csi`; `managed-csi-premium` on
Prod). A headless `Service` gives a stable DNS name; a `ClusterIP` Service is the
connection target. Credentials come from a K8s Secret (from GitHub Environments).
Storage size + StorageClass are per-env variables (FR-006).

**Rationale**: User chose in-cluster over managed. StatefulSet + retained PVC keeps
data across pod restart/reschedule (FR-010, edge case: DB persistence). Postgres is
**never** exposed beyond `ClusterIP` (security — no ingress/LB path to the DB).

**Explicitly deferred**: backups, PITR, HA/replication, major-version upgrades — their
own future feature (spec Out of Scope). A resource comment + a README note flag the
gap so it isn't mistaken for done.

**Alternatives considered**: Azure DB for PostgreSQL Flexible Server (managed; user
declined); a Postgres operator (CloudNativePG/Zalando — powerful HA/backups but scope
creep for a single-replica start).

---

## 8. Secrets & configuration — GitHub Environments → K8s Secrets/ConfigMaps

**Decision**: Non-secret settings → `ConfigMap`; secrets → `Secret`, both created by
the `app` module from Terraform **input variables** whose values are supplied at
`apply` time from **GitHub Environments** (via `TF_VAR_*` or `-var`). Backend env
mirrors compose: `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey`,
`Jwt__Issuer/Audience`, `Email__Provider=Resend`, `Email__Resend__ApiKey`,
`Email__FromAddress`, `Email__FrontendBaseUrl`, `Admin__Emails`,
`ASPNETCORE_ENVIRONMENT` (Development on Dev / Production on Prod). (FR-018/019.)

**No plaintext in state**: secret-bearing variables are `sensitive = true`; values
live only in GitHub Environments and the (access-controlled, encrypted) remote state.
Rotation = update the GitHub Environment secret + re-run deploy; no code change
(FR-020, SC-006). **No Key Vault** (constitution).

**Alternatives considered**: Azure Key Vault + CSI driver (constitution forbids);
sealed-secrets/SOPS in-repo (adds tooling; GitHub Environments already the mandated
store).

---

## 9. Deployment flow — extend `deploy.yml`

**Decision**: Extend the existing `.github/workflows/deploy.yml`: (1) build + push
`ghcr.io/<owner>/juggerhub-backend|frontend:<sha>`; (2) `az login` via OIDC;
(3) `terraform init` (shared backend) → `workspace select <env>` →
`apply -var-file=envs/<env>.tfvars -var image_tag=<sha>` with secret `TF_VAR_*` from
the GitHub Environment; (4) rollout is the apply itself (updated image tag triggers a
RollingUpdate); (5) smoke: wait for rollout, curl `/api/v1/health`, probe `/hubs`.
Dev auto-deploys on merge to `main`; Prod gates on a GitHub Environment approval.

**Rationale**: FR-021/022, SC-009. RollingUpdate + readiness probes mean a bad image
never displaces the healthy ReplicaSet (edge case: failed rollout). Prod approval uses
GitHub Environments' protection rules (no extra infra).

**Alternatives considered**: Argo CD / Flux GitOps (great long-term, but heavier than
this feature needs and not in the constitution's GitHub-Actions+Terraform mandate).

---

## 10. Tooling & quality gates

**Decision**: `terraform fmt`/`validate` + `tflint` + `checkov` (or `trivy config`)
in CI; `terraform plan` posted on PRs as the human review gate. Provider versions
pinned in `versions.tf` (major pinned, per constitution dependency rule). All helper
scripts `.ps1` (Principle VI).

**Rationale**: Static analysis catches misconfig (open storage, missing TLS min
version) before apply; pinning keeps environments reproducible.
