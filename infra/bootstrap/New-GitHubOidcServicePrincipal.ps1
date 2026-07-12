<#
.SYNOPSIS
    Creates the GitHub Actions CI service principal for JuggerHub (feature 015),
    using federated OIDC credentials (no client secret).

.DESCRIPTION
    Creates, OUTSIDE Terraform, a Microsoft Entra ID app registration + service
    principal that GitHub Actions uses to authenticate to Azure via OIDC. Adds one
    federated credential per GitHub Environment (Dev, Prod, ...) plus optional
    branch/PR credentials, and assigns the roles Terraform needs.

    Because Terraform makes role assignments (e.g. granting the AKS identity
    Network Contributor on the static-IP resource group so ingress can bind it),
    the SP needs 'User Access Administrator' in addition to 'Contributor'. It also
    needs 'Storage Blob Data Contributor' on the Terraform-state storage account
    (keyless AAD backend auth). Grant 'Owner' instead only if you accept the broader
    scope.

    OUTPUT: prints AZURE_CLIENT_ID / AZURE_TENANT_ID / AZURE_SUBSCRIPTION_ID to set
    as GitHub *variables* (not secrets — harmless with OIDC). Nothing sensitive is
    produced; there is no client secret.

    Idempotent-ish: re-running reuses an existing app by display name and re-adds
    only missing federated credentials/role assignments.

.NOTES
    PowerShell only (constitution Principle VI). Requires `az login` as a user who
    can create app registrations and assign subscription roles (Owner or
    User Access Administrator + a privileged directory role).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]   $SubscriptionId,
    [Parameter(Mandatory)] [string]   $RepoOwner,      # e.g. jnroesch
    [Parameter(Mandatory)] [string]   $RepoName,       # e.g. juggerhub
    [string]   $AppName            = 'sp-juggerhub-github-ci',
    # MUST match the GitHub Environment names used in deploy.yml (development/production).
    [string[]] $Environments       = @('development', 'production'),
    [string]   $StateResourceGroup = 'rg-juggerhub-tfstate',
    [string]   $StateStorageAccount= 'stjuggerhubtfstate',
    [switch]   $IncludeMainBranch,                      # add ref:refs/heads/main credential
    [switch]   $IncludePullRequest                      # add pull_request credential (plan on PRs)
)

$ErrorActionPreference = 'Stop'
$issuer   = 'https://token.actions.githubusercontent.com'
$audience = 'api://AzureADTokenExchange'

az account set --subscription $SubscriptionId | Out-Null
$tenantId = az account show --query tenantId -o tsv

Write-Host "==> Ensuring app registration '$AppName'" -ForegroundColor Cyan
$appId = az ad app list --display-name $AppName --query "[0].appId" -o tsv
if (-not $appId) {
    $appId = az ad app create --display-name $AppName --query appId -o tsv
    Write-Host "    created app $appId"
} else {
    Write-Host "    reusing app $appId"
}

Write-Host "==> Ensuring service principal" -ForegroundColor Cyan
$spId = az ad sp list --filter "appId eq '$appId'" --query "[0].id" -o tsv
if (-not $spId) {
    $spId = az ad sp create --id $appId --query id -o tsv
    Write-Host "    created SP $spId"
} else {
    Write-Host "    reusing SP $spId"
}

# --- Federated credentials --------------------------------------------------
function Add-FederatedCredential {
    param([string] $Name, [string] $Subject)
    $exists = az ad app federated-credential list --id $appId --query "[?name=='$Name'] | [0].name" -o tsv
    if ($exists) { Write-Host "    fed-cred '$Name' exists"; return }
    $params = @{ name = $Name; issuer = $issuer; subject = $Subject; audiences = @($audience) } | ConvertTo-Json -Compress
    az ad app federated-credential create --id $appId --parameters $params | Out-Null
    Write-Host "    added fed-cred '$Name' -> $Subject"
}

Write-Host "==> Federated credentials for $RepoOwner/$RepoName" -ForegroundColor Cyan
foreach ($env in $Environments) {
    Add-FederatedCredential -Name "gh-env-$($env.ToLower())" -Subject "repo:${RepoOwner}/${RepoName}:environment:$env"
}
if ($IncludeMainBranch) { Add-FederatedCredential -Name 'gh-branch-main' -Subject "repo:${RepoOwner}/${RepoName}:ref:refs/heads/main" }
if ($IncludePullRequest) { Add-FederatedCredential -Name 'gh-pull-request' -Subject "repo:${RepoOwner}/${RepoName}:pull_request" }

# --- Role assignments -------------------------------------------------------
$subScope   = "/subscriptions/$SubscriptionId"
$stateScope = "$subScope/resourceGroups/$StateResourceGroup/providers/Microsoft.Storage/storageAccounts/$StateStorageAccount"

function Add-Role {
    param([string] $Role, [string] $Scope)
    $have = az role assignment list --assignee $appId --role $Role --scope $Scope --query "[0].id" -o tsv 2>$null
    if ($have) { Write-Host "    role '$Role' already assigned"; return }
    az role assignment create --assignee $appId --role $Role --scope $Scope | Out-Null
    Write-Host "    assigned '$Role' at $Scope"
}

Write-Host "==> Role assignments" -ForegroundColor Cyan
Add-Role -Role 'Contributor'                  -Scope $subScope
Add-Role -Role 'User Access Administrator'    -Scope $subScope   # needed for TF role assignments (AKS/ingress)
Add-Role -Role 'Storage Blob Data Contributor'-Scope $stateScope # keyless state backend

Write-Host ""
Write-Host "Set these as GitHub repo/environment VARIABLES (not secrets):" -ForegroundColor Green
Write-Host "  AZURE_CLIENT_ID       = $appId"
Write-Host "  AZURE_TENANT_ID       = $tenantId"
Write-Host "  AZURE_SUBSCRIPTION_ID = $SubscriptionId"
Write-Host ""
Write-Host "The deploy workflow uses azure/login with these + 'permissions: id-token: write'." -ForegroundColor Green
Write-Host "No client secret is created or needed." -ForegroundColor Green
