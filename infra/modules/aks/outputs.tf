output "cluster_name" {
  value = azurerm_kubernetes_cluster.this.name
}

output "node_resource_group" {
  value = azurerm_kubernetes_cluster.this.node_resource_group
}

output "cluster_identity_principal_id" {
  description = "Control-plane managed identity; needs Network Contributor on the static-IP RG."
  value       = azurerm_kubernetes_cluster.this.identity[0].principal_id
}

output "kube_host" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].host
  sensitive = true
}

output "kube_client_certificate" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].client_certificate
  sensitive = true
}

output "kube_client_key" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].client_key
  sensitive = true
}

output "kube_cluster_ca_certificate" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].cluster_ca_certificate
  sensitive = true
}

# The pod network (#244). Every in-cluster proxy hop — the ingress controller, the frontend nginx —
# has an address in it, so it is the set of peers whose X-Forwarded-For headers are believed.
output "pod_cidr" {
  value = azurerm_kubernetes_cluster.this.network_profile[0].pod_cidr
}
