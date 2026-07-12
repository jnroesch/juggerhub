output "namespace" {
  value = kubernetes_namespace_v1.app.metadata[0].name
}

output "service_urls" {
  description = "In-cluster service endpoints (for smoke checks / debugging)."
  value = {
    backend  = "http://backend.${var.namespace}.svc.cluster.local:8080"
    frontend = "http://frontend.${var.namespace}.svc.cluster.local:80"
    postgres = "postgres.${var.namespace}.svc.cluster.local:5432"
  }
}
