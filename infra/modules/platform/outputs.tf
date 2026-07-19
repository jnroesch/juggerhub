output "ingress_class_name" {
  value = var.ingress_class_name
}

output "cluster_issuer_staging" {
  value = kubernetes_manifest.cluster_issuer_staging.manifest.metadata.name
}

output "cluster_issuer_prod" {
  value = kubernetes_manifest.cluster_issuer_prod.manifest.metadata.name
}
