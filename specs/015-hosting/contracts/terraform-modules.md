# Contract: Terraform Module Interfaces

Input/output contract for the root module and its four children. Types are indicative;
exact `variables.tf`/`outputs.tf` are written in implementation. **Contract, not code.**

---

## Root module (`infra/`)

**Backend**: `azurerm`, single account/container, workspace-keyed key (see
[state-backend.md](state-backend.md)).

**Workspace guard** (FR-014): a `precondition`/`check` asserting
`contains(["dev","prod","staging"], terraform.workspace)` — apply on `default` fails.

**Key inputs** (from `envs/<env>.tfvars` + secret `TF_VAR_*`): all variables in
[data-model.md](../data-model.md) (Environment configuration + secret-bearing).

**Key outputs**:

| Output | Meaning |
|--------|---------|
| `ingress_public_ip` | The env's **static** public IP — set this as the registrar A record for `app_hostname` (FR-016a) |
| `ingress_host` | HTTPS hostname the app is reachable at (`juggerhub.com` / `dev.juggerhub.com`) (SC-007) |
| `dns_a_records` | Map of hostname → IP the operator must create at the registrar (incl. `www` on prod) |
| `cluster_name` | AKS cluster name |
| `kubeconfig_command` | `az aks get-credentials …` for operators |
| `namespace` | app namespace |

---

## Module `network`

| Direction | Name | Type | Notes |
|-----------|------|------|-------|
| in | `name_prefix`, `location` | string | naming/region |
| in | `vnet_cidr`, `subnet_cidr` | string | small ranges (CNI Overlay) |
| out | `subnet_id` | string | consumed by `aks` |
| out | `resource_group_name` | string | per-env RG (≠ state RG) |
| out | `public_ip_address` | string | **static** Standard IP; feeds `platform` + root `ingress_public_ip` (FR-016a) |
| out | `public_ip_resource_group` | string | RG of the IP (for the ingress LB annotation) |

**Contract guarantee**: `public_ip_address` is `Static`/Standard SKU and stable across
applies and ingress-controller re-creation.

---

## Module `aks`

| Direction | Name | Type | Notes |
|-----------|------|------|-------|
| in | `subnet_id`, `resource_group_name`, `location` | string | from network |
| in | `node_vm_size` | string | per-env sizing (FR-006) |
| in | `system_node_count` | number | system pool |
| in | `user_node_min`, `user_node_max` | number | FR-005/007 |
| in | `enable_user_autoscale` | bool | FR-007 |
| in | `api_authorized_ip_ranges` | list(string) | API server allowlist |
| out | `kube_host`, `kube_client_certificate`, `kube_client_key`, `kube_cluster_ca` (all `sensitive`) | string | feed `kubernetes`/`helm` providers |
| out | `cluster_name`, `node_resource_group` | string | — |

**Contract guarantees**: system + user node pools separated; user pool autoscale iff
`enable_user_autoscale`; managed identity; Azure CNI Overlay.

---

## Module `platform` (Helm add-ons)

| Direction | Name | Type | Notes |
|-----------|------|------|-------|
| in | `ingress_nginx_version`, `cert_manager_version` | string | pinned charts |
| in | `public_ip_address`, `public_ip_resource_group` | string | bind ingress-nginx to the static IP (from `network`) |
| in | `acme_email` | string | Let's Encrypt registration contact |
| out | `ingress_class_name` | string | consumed by `app` Ingress |
| out | `cluster_issuer_staging`, `cluster_issuer_prod` | string | issuer names for the `app` Ingress annotation |

**Contract guarantees**: ingress-nginx binds to the **provided static IP**
(`controller.service.loadBalancerIP` + LB-resource-group annotation), not an
auto-assigned one; cert-manager is installed with **both** Let's Encrypt
`ClusterIssuer`s (staging + prod) using the **HTTP-01** solver on the ingress class.

---

## Module `app` (Kubernetes workloads)

| Direction | Name | Type | Notes |
|-----------|------|------|-------|
| in | `namespace`, `ingress_class_name` | string | placement/routing |
| in | `app_hostname` | string | per-env host: `juggerhub.com` / `dev.juggerhub.com` (FR-016) |
| in | `enable_www_redirect` | bool | prod `www.juggerhub.com` → apex |
| in | `enable_tls` | bool | HTTPS + cert (default true, FR-017) |
| in | `cluster_issuer` | string | which ClusterIssuer to annotate (`letsencrypt-staging`/`-prod`) |
| in | `image_repo_backend`, `image_repo_frontend`, `image_tag` | string | GHCR + SHA (FR-021) |
| in | `backend_replicas`, `frontend_replicas` | number | FR-005 |
| in | `enable_backend_hpa` | bool | Prod HPA |
| in | `postgres_storage_gb`, `postgres_storage_class` | number/string | FR-006 |
| in | `aspnetcore_environment` | string | app env |
| in (sensitive) | `postgres_password`, `jwt_signing_key`, `resend_api_key`, `email_from_address`, `email_frontend_base_url`, `admin_emails`, `ghcr_pull_token` | string | FR-018/019 |
| out | `service_urls` | map | for smoke tests |

**Contract guarantees**: see [k8s-workloads.md](k8s-workloads.md). Secrets are created
as K8s `Secret`s, never rendered to plaintext outputs; all secret vars `sensitive`.
