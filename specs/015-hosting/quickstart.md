# Quickstart: Provision & Validate a JuggerHub Environment

A runnable validation guide proving the feature end-to-end. Details live in
[contracts/](contracts/) and [data-model.md](data-model.md) — this is the run/verify
path, not implementation.

## Prerequisites

- Azure subscription + `az` CLI logged in (`az login`), Contributor on the subscription.
- Terraform ≥ 1.9, `kubectl`, `helm`.
- GitHub Environments `Dev` and `Prod` populated with the secret set
  ([data-model.md](data-model.md) secret table) and a GHCR token with `read:packages`.
- Container images pushed to `ghcr.io/<owner>/juggerhub-{backend,frontend}:<sha>`.

## One-time bootstrap (state backend — outside Terraform, FR-013)

```powershell
# Run ONCE per subscription. Creates the state RG + storage account + container.
./infra/bootstrap/New-TfStateBackend.ps1 -Location westeurope
# Note the printed storage account / container; they match infra/backend.tf.
```

**Expected**: `rg-juggerhub-tfstate` with a hardened storage account + `tfstate`
container. Re-running is a no-op (idempotent). These are never in any `.tf` file.

## Provision Dev (User Story 1, SC-001/007)

```powershell
cd infra
terraform init
terraform workspace new dev            # first time (else: workspace select dev)
# Secret TF vars come from the GitHub Environment in CI; locally export TF_VAR_* first.
terraform plan  -var-file=envs/dev.tfvars -var="image_tag=<sha>"
terraform apply -var-file=envs/dev.tfvars -var="image_tag=<sha>"
```

**Expected**: RG + VNet + **static public IP** + AKS (1 system, 1 user node) +
ingress-nginx (bound to that IP) + cert-manager (+ Let's Encrypt staging/prod issuers)
+ `juggerhub` namespace with backend(1)/frontend(1)/postgres. Outputs print
`ingress_public_ip`, `ingress_host`, and `dns_a_records`.

### Point DNS (one-time per env, at the registrar — FR-016a)

```powershell
terraform output -raw ingress_public_ip     # e.g. 20.61.x.x
# At the domain registrar for juggerhub.com, create:
#   dev :  A  dev            -> <ingress_public_ip>
#   prod:  A  @ (apex)       -> <ingress_public_ip>   and  A/CNAME  www -> apex/IP
```

cert-manager's HTTP-01 `Challenge` retries until the record resolves, then issues the
cert automatically — **no re-apply** needed.

### Verify (smoke — [k8s-workloads.md](contracts/k8s-workloads.md))

```powershell
az aks get-credentials -g rg-juggerhub-dev -n <cluster_name>
kubectl -n juggerhub rollout status deploy/backend deploy/frontend
kubectl -n juggerhub rollout status statefulset/postgres
kubectl -n juggerhub get certificate            # READY=True once DNS resolves (SC-008)
$H = terraform output -raw ingress_host         # dev.juggerhub.com
curl -I "http://$H/"                  # 308 -> https             (SC-008)
curl "https://$H/"                    # 200, SPA, valid cert     (SC-007)
curl "https://$H/api/v1/health"       # 200  (backend+DB, US1 AC2)
# wss upgrade to /hubs → 101          (FR-009)  e.g. via a browser session or wscat over wss
kubectl -n juggerhub get svc postgres -o jsonpath='{.spec.type}'   # ClusterIP (never public)
```

**Pass** when the cert is `READY=True`, HTTPS serves the SPA over a trusted cert,
health is 200, a user can register/log in, and `/hubs` upgrades over `wss`. **Fail** on
any `ImagePullBackOff` (GHCR pull secret) or a `Challenge` stuck pending (check the A
record / propagation).

## Provision Prod (User Story 3 — independent sizing, SC-004)

```powershell
terraform workspace new prod
terraform apply -var-file=envs/prod.tfvars -var="image_tag=<sha>"
kubectl -n juggerhub get deploy backend -o jsonpath='{.spec.replicas}'   # ≥ 2  (SC-004)
kubectl get nodes                                                        # autoscaling user pool
```

**Expected**: same resource set as Dev, larger — ≥ 2 backend replicas, autoscaling
user pool, premium/larger Postgres disk — driven **only** by `prod.tfvars`.

## Prove "add an environment = add a file" (User Story 2, SC-002/003)

```powershell
Copy-Item envs/staging.tfvars.example envs/staging.tfvars   # edit sizes only
terraform workspace new staging
terraform plan -var-file=envs/staging.tfvars -var="image_tag=<sha>"
git diff --stat   # ONLY envs/staging.tfvars added; zero changes to modules/ or *.tf
```

**Pass** when a new environment is fully planned with no edits to shared definitions.

## Prove state isolation (User Story 4, SC-005)

```powershell
az storage blob list --account-name stjuggerhubtfstate -c tfstate -o table
# Expect: env:/dev/juggerhub.tfstate AND env:/prod/juggerhub.tfstate (isolated, one container)
terraform workspace select default; terraform apply -var-file=envs/dev.tfvars  # MUST refuse (FR-014)
```

## Prove state RG is untouched (FR-013)

```powershell
# A plan/apply of any env must show NO changes to rg-juggerhub-tfstate or its storage.
terraform plan -var-file=envs/dev.tfvars | Select-String "tfstate"   # no matches
```

## CI path (User Story 5, SC-009)

Merge to `main` → `deploy.yml` builds/pushes images to GHCR, `az login` (OIDC),
`terraform apply` for the target env with `image_tag=<sha>` and `TF_VAR_*` secrets from
the GitHub Environment, then waits for rollout + runs the smoke curls. Prod requires a
GitHub Environment approval. A never-Ready new pod leaves the prior version serving
(FR-022).

## Teardown (non-prod)

```powershell
terraform workspace select dev
terraform destroy -var-file=envs/dev.tfvars   # leaves rg-juggerhub-tfstate intact (FR-013)
```
