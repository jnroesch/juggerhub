provider "azurerm" {
  features {}
  subscription_id = var.subscription_id # or ARM_SUBSCRIPTION_ID
  # Auth: az login (operator) or OIDC (CI, via ARM_USE_OIDC / azure-login action).

  # azurerm 5.0 changed the default from auto-registering ~60 Resource Providers to
  # registering none. Existing subscriptions are unaffected (theirs are long since
  # registered), so this is invisible until someone bootstraps a NEW subscription or
  # environment — at which point the first apply fails on an unregistered provider.
  # "legacy" keeps the 4.x behaviour that this repo's bootstrap path assumes.
  resource_provider_registrations = "legacy"
}

# The kubernetes/helm providers are configured from the AKS cluster's admin
# kubeconfig outputs. On a brand-new cluster this creates a provider-depends-on-
# resource ordering; use the two-phase apply noted in README on first run.
provider "kubernetes" {
  host                   = module.aks.kube_host
  client_certificate     = base64decode(module.aks.kube_client_certificate)
  client_key             = base64decode(module.aks.kube_client_key)
  cluster_ca_certificate = base64decode(module.aks.kube_cluster_ca_certificate)
}

provider "helm" {
  kubernetes = {
    host                   = module.aks.kube_host
    client_certificate     = base64decode(module.aks.kube_client_certificate)
    client_key             = base64decode(module.aks.kube_client_key)
    cluster_ca_certificate = base64decode(module.aks.kube_cluster_ca_certificate)
  }
}
