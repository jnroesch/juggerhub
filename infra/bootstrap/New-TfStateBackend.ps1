<#
.SYNOPSIS
    Bootstraps the Terraform remote-state backend for JuggerHub (feature 015).

.DESCRIPTION
    Creates, OUTSIDE Terraform, the single resource group + storage account +
    blob container that hold Terraform state for every environment. Terraform's
    azurerm backend (infra/backend.tf) points at these; workspaces keep one state
    blob per environment (env:/<env>/juggerhub.tfstate) inside the one container.

    These resources are NEVER declared in any .tf file and are never
    created/modified/destroyed by a `terraform apply` (feature 015, FR-013).

    Idempotent: safe to re-run. Requires an existing Azure subscription and
    `az login` with rights to create the resources.

.NOTES
    PowerShell only (constitution Principle VI). Uses the `az` CLI.
    Storage account names are globally unique, 3-24 chars, lowercase alphanumeric —
    override -StorageAccountName if the default is taken.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SubscriptionId,
    [string] $Location            = 'westeurope',
    [string] $ResourceGroup       = 'rg-juggerhub-tfstate',
    [string] $StorageAccountName  = 'stjuggerhubtfstate',
    [string] $ContainerName       = 'tfstate'
)

$ErrorActionPreference = 'Stop'

Write-Host "==> Selecting subscription $SubscriptionId" -ForegroundColor Cyan
az account set --subscription $SubscriptionId | Out-Null

Write-Host "==> Resource group '$ResourceGroup' in $Location" -ForegroundColor Cyan
az group create `
    --name $ResourceGroup `
    --location $Location `
    --tags managed-by=bootstrap feature=015-hosting purpose=tfstate | Out-Null

Write-Host "==> Storage account '$StorageAccountName' (hardened)" -ForegroundColor Cyan
# min TLS 1.2, no public blob access, HTTPS only. AAD (RBAC) auth is used by the
# backend, so shared-key access is not required for Terraform.
az storage account create `
    --name $StorageAccountName `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2 `
    --min-tls-version TLS1_2 `
    --https-only true `
    --allow-blob-public-access false `
    --tags managed-by=bootstrap feature=015-hosting purpose=tfstate | Out-Null

Write-Host "==> Enabling blob versioning + soft-delete" -ForegroundColor Cyan
az storage account blob-service-properties update `
    --account-name $StorageAccountName `
    --resource-group $ResourceGroup `
    --enable-versioning true `
    --enable-delete-retention true `
    --delete-retention-days 30 | Out-Null

Write-Host "==> Container '$ContainerName' (AAD auth)" -ForegroundColor Cyan
# --auth-mode login uses your az login identity (needs 'Storage Blob Data
# Contributor' on this account). Re-running is a no-op if the container exists.
az storage container create `
    --name $ContainerName `
    --account-name $StorageAccountName `
    --auth-mode login | Out-Null

Write-Host ""
Write-Host "State backend ready. Wire these into infra/backend.tf (or -backend-config):" -ForegroundColor Green
Write-Host "  resource_group_name  = `"$ResourceGroup`""
Write-Host "  storage_account_name = `"$StorageAccountName`""
Write-Host "  container_name       = `"$ContainerName`""
Write-Host "  key                  = `"juggerhub.tfstate`""
Write-Host ""
Write-Host "NOTE: grant every operator + the CI service principal the role" -ForegroundColor Yellow
Write-Host "      'Storage Blob Data Contributor' on this storage account." -ForegroundColor Yellow
