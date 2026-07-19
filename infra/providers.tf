provider "azurerm" {
  features {}
  subscription_id = var.subscription_id # or ARM_SUBSCRIPTION_ID
  # Auth: az login (operator) or OIDC (CI, via ARM_USE_OIDC / azure-login action).
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
