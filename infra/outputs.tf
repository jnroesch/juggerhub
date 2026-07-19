output "ingress_public_ip" {
  description = "Static public IP. Create a registrar A record: app_hostname -> this IP (FR-016a)."
  value       = module.network.public_ip_address
}

output "ingress_host" {
  description = "HTTPS hostname the app is reachable at."
  value       = var.app_hostname
}

output "dns_a_records" {
  description = "A records the operator must create at the registrar."
  value = merge(
    { (var.app_hostname) = module.network.public_ip_address },
    var.enable_www_redirect ? { "www.${var.app_hostname}" = module.network.public_ip_address } : {},
  )
}

output "cluster_name" {
  value = module.aks.cluster_name
}

output "kubeconfig_command" {
  description = "Fetch kubeconfig for this environment."
  value       = "az aks get-credentials --resource-group ${module.network.resource_group_name} --name ${module.aks.cluster_name}"
}

output "namespace" {
  value = local.namespace
}
