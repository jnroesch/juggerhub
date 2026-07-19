# Phase 1 Data Model: Environment Configuration & Resource Topology

For infrastructure, the "data model" is (a) the **per-environment configuration
variables** that parameterize the single definition and (b) the **resource topology**
those variables produce. Entities map to spec §Key Entities.

---

## Entity: Environment

A named, isolated deployment target selected by `terraform.workspace`.

| Field | Source | Dev | Prod | Notes |
|-------|--------|-----|------|-------|
| `env` (name) | `terraform.workspace` | `dev` | `prod` | Guarded: must be one of dev/prod/staging (FR-014) |
| state key | backend + workspace | `env:/dev/juggerhub.tfstate` | `env:/prod/juggerhub.tfstate` | One container, isolated per env (FR-012) |
| secret set | GitHub Environment | `Dev` | `Prod` | Injected as `TF_VAR_*` at apply (FR-018) |

**Uniqueness / relationships**: exactly one state entry and one GitHub Environment per
Environment; all Environments share the same module graph (FR-002).

---

## Entity: Environment configuration (`envs/<env>.tfvars`)

The complete set of per-env knobs. **These are the only differences between
environments** (FR-002/SC-003). Types/defaults are declared in `infra/variables.tf`.

| Variable | Type | `dev.tfvars` | `prod.tfvars` | Requirement |
|----------|------|--------------|---------------|-------------|
| `location` | string | `westeurope` | `westeurope` | — |
| `node_vm_size` | string | `Standard_B2s` | `Standard_D2s_v5` | FR-006 (smaller on dev) |
| `system_node_count` | number | `1` | `1` | system pool |
| `user_node_min` | number | `1` | `2` | FR-005/007 |
| `user_node_max` | number | `1` | `4` | dev min==max ⇒ no autoscale (FR-007) |
| `enable_user_autoscale` | bool | `false` | `true` | FR-007 |
| `backend_replicas` | number | `1` | `2` | FR-005/SC-004 |
| `frontend_replicas` | number | `1` | `2` | parity |
| `enable_backend_hpa` | bool | `false` | `true` | optional Prod HPA |
| `postgres_storage_gb` | number | `8` | `32` | FR-006 |
| `postgres_storage_class` | string | `managed-csi` | `managed-csi-premium` | FR-006 |
| `aspnetcore_environment` | string | `Development` | `Production` | app env |
| `api_authorized_ip_ranges` | list(string) | CI+operator CIDRs | CI+operator CIDRs | §3 research |
| `app_hostname` | string | `dev.juggerhub.com` | `juggerhub.com` | FR-016 (per-env host) |
| `enable_www_redirect` | bool | `false` | `true` | Prod `www.juggerhub.com` → apex |
| `enable_tls` | bool | `true` | `true` | FR-017 (HTTPS from day one) |
| `letsencrypt_issuer` | string | `letsencrypt-staging` → `letsencrypt-prod` | `letsencrypt-prod` | §5 research (rate limits) |
| `image_tag` | string | `-var` at deploy (commit SHA) | same | FR-021 |

**Static public IP** (FR-016a): one `azurerm_public_ip` (Standard, `Static`) per env,
created in the `network` module and bound to ingress-nginx; its address is output as
`ingress_public_ip` for the manual registrar A record. Not a tunable — always present.

### Secret-bearing variables (never in tfvars; `sensitive = true`; from GitHub Env)

| Variable | Maps to app config | Requirement |
|----------|--------------------|-------------|
| `postgres_password` | DB + `ConnectionStrings__DefaultConnection` | FR-019 |
| `jwt_signing_key` | `Jwt__SigningKey` | FR-019 |
| `resend_api_key` | `Email__Resend__ApiKey` | FR-019 |
| `email_from_address` | `Email__FromAddress` | FR-019 |
| `email_frontend_base_url` | `Email__FrontendBaseUrl` | FR-019 |
| `admin_emails` | `Admin__Emails` | FR-019 |
| `ghcr_pull_token` | `imagePullSecret` dockerconfigjson | FR-015 |

**Validation rules**: `env` ∈ {dev,prod,staging}; `backend_replicas ≥ 1`;
`user_node_max ≥ user_node_min`; `enable_user_autoscale ⇒ user_node_max > user_node_min`;
secret vars non-empty (`validation` blocks) so a missing GitHub Environment secret
fails at plan, not at runtime.

---

## Entity: Remote state (shared, workspace-keyed)

| Attribute | Value |
|-----------|-------|
| Resource group | `rg-juggerhub-tfstate` (bootstrap; **outside** Terraform — FR-013) |
| Storage account | `stjuggerhubtfstate` (globally unique; hardened, §2 research) |
| Container | `tfstate` (single, shared) |
| Key | `juggerhub.tfstate` (workspaces prefix `env:/<env>/`) |
| Isolation | one state per Environment; apply to one can't mutate another (SC-005) |

---

## Entity: Workload (per environment, in the `app` module)

| Workload | Kind | Replicas | Port | Storage | Exposure |
|----------|------|----------|------|---------|----------|
| backend | Deployment | `backend_replicas` | 8080 | none | ClusterIP (via frontend proxy) |
| frontend | Deployment | `frontend_replicas` | 80 | none | ClusterIP ← Ingress `/` |
| postgres | StatefulSet | 1 | 5432 | PVC `postgres_storage_gb` | ClusterIP only (never ingress) |

**State transitions (rollout)**: `image_tag` change → new ReplicaSet → RollingUpdate
gated by readiness probes → old ReplicaSet retired only after new pods Ready; a
never-Ready new pod leaves the old version serving (FR-022, edge case: failed rollout).

---

## Resource topology (produced graph)

```text
Resource Group (rg-juggerhub-<env>)                 [network module]
├─ VNet + Subnet
├─ Static Public IP (Standard)  ──► registrar A record: app_hostname → this IP (FR-016a)
└─ AKS managed cluster (Standard)                [aks module]
      ├─ system node pool (system_node_count)
      ├─ user node pool (min/max, autoscale?)
      └─ managed identity → outputs kubeconfig
         │
         ├─ platform namespace                       [platform module, Helm]
         │  ├─ ingress-nginx  → bound to the static Public IP (loadBalancerIP)
         │  └─ cert-manager   + letsencrypt-staging/-prod ClusterIssuers (HTTP-01)
         │
         └─ juggerhub namespace                       [app module]
            ├─ Secret (app secrets)  ← GitHub Environment TF_VARs
            ├─ Secret (ghcr pull)    ← ghcr_pull_token
            ├─ ConfigMap (non-secret settings)
            ├─ Deployment backend  (imagePullSecrets, envFrom Secret+ConfigMap)
            ├─ Deployment frontend (imagePullSecrets)
            ├─ StatefulSet postgres + PVC (StorageClass per env)
            ├─ Services: backend(ClusterIP), frontend(ClusterIP),
            │            postgres(headless + ClusterIP)
            ├─ Ingress: app_hostname / → frontend (TLS via cert-manager HTTP-01;
            │           HTTP→HTTPS redirect; www→apex on prod; WS timeout for /hubs)
            ├─ Certificate (per-host, auto-renew)      [cert-manager]
            └─ HPA backend (prod, if enable_backend_hpa)
```

Everything above is identical across environments **except** the values in the
Environment-configuration table — this is the structural guarantee behind FR-002 /
SC-003.
