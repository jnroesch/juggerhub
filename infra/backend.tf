# Remote state — single storage account/container created by
# bootstrap/New-TfStateBackend.ps1 (managed OUTSIDE Terraform, FR-013).
# Workspaces yield one state blob per environment: env:/<env>/juggerhub.tfstate.
#
# Backend blocks cannot use variables. If you changed the storage account name in
# the bootstrap script (globally unique!), either edit it here or override at init:
#   terraform init -backend-config="storage_account_name=<name>"
#
# Auth is keyless via Entra ID (AAD): your operator account and the CI service
# principal each need "Storage Blob Data Contributor" on this storage account.
terraform {
  backend "azurerm" {
    resource_group_name  = "rg-juggerhub-tfstate"
    storage_account_name = "stjuggerhubtfstate"
    container_name       = "tfstate"
    key                  = "juggerhub.tfstate"
    use_azuread_auth     = true
    use_oidc             = true # honored in GitHub Actions; ignored for local az-login
  }
}
