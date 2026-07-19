# Contract: Remote State Backend & Bootstrap

Governs FR-012/013/014 and SC-005. The state backend is created **once, outside
Terraform**, then referenced by every environment.

---

## Bootstrap contract (`infra/bootstrap/New-TfStateBackend.ps1`)

**Run**: once per subscription, by an operator/CI with Contributor rights, **before**
any `terraform init`. PowerShell only (Principle VI).

**Creates** (idempotent — safe to re-run):

| Resource | Name (default) | Hardening |
|----------|----------------|-----------|
| Resource group | `rg-juggerhub-tfstate` | tagged `managed-by=bootstrap` |
| Storage account | `stjuggerhubtfstate` | `min_tls=TLS1_2`, public blob access off, versioning + blob soft-delete on, `allow_shared_key`/RBAC per org policy |
| Blob container | `tfstate` | private |

**Outputs** (printed for the operator to wire into CI vars): resource group name,
storage account name, container name.

**Invariants**:
- These resources are **never** declared in any `.tf` file (FR-013). No Terraform
  apply may create, modify, or destroy them.
- The script is safe to re-run and does not touch environment resources.

---

## Backend block contract (`infra/backend.tf`)

```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "rg-juggerhub-tfstate"   # bootstrap RG (outside TF)
    storage_account_name = "stjuggerhubtfstate"
    container_name       = "tfstate"
    key                  = "juggerhub.tfstate"       # workspaces ⇒ env:/<env>/juggerhub.tfstate
    use_oidc             = true                      # GitHub Actions federated identity
  }
}
```

**Isolation guarantee**: with workspaces, Azure stores each environment's state as a
distinct blob `env:/<workspace>/juggerhub.tfstate` in the one `tfstate` container.
Selecting workspace `dev` can neither read nor write `prod`'s blob (SC-005, FR-012).

---

## Workspace lifecycle contract

| Action | Command | Guarantee |
|--------|---------|-----------|
| First-time init | `terraform init` | binds to shared backend; creates no env state |
| Create env | `terraform workspace new <env>` | new isolated state blob |
| Select env | `terraform workspace select <env>` | subsequent plan/apply scoped to that env |
| Guard | apply on `default` | **fails** the workspace precondition (FR-014) — no resources created |

**Auth**: backend + `azurerm` provider authenticate via GitHub Actions **OIDC**
federated credentials (no stored client secret). Operators use `az login` locally.
