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
