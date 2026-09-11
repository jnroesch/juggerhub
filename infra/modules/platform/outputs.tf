output "ingress_class_name" {
  value = var.ingress_class_name
}

# The app's NetworkPolicies (#253) admit traffic from this namespace only.
output "ingress_namespace" {
  value = helm_release.ingress_nginx.namespace
}

output "cluster_issuer_staging" {
  value = kubernetes_manifest.cluster_issuer_staging.manifest.metadata.name
}

output "cluster_issuer_prod" {
  value = kubernetes_manifest.cluster_issuer_prod.manifest.metadata.name
}
