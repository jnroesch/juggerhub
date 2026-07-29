# The JuggerHub workloads: namespace, config/secrets, in-cluster Postgres, backend
# and frontend Deployments, the single-origin Ingress (TLS via cert-manager), and an
# optional backend HPA. Mirrors docker-compose.yml minus Mailpit.

locals {
  backend_image  = "${var.image_repo_backend}:${var.image_tag}"
  frontend_image = "${var.image_repo_frontend}:${var.image_tag}"

  # ghcr_pull_token is sensitive; count/for_each can't derive from a sensitive value
  # (older Terraform rejects it). Declassify only the "is a token set?" boolean.
  ghcr_enabled = nonsensitive(var.ghcr_pull_token != "")

  tls_hosts = var.enable_www_redirect ? [var.app_hostname, "www.${var.app_hostname}"] : [var.app_hostname]

  ingress_annotations = merge(
    {
      "nginx.ingress.kubernetes.io/ssl-redirect"         = tostring(var.enable_tls)
      "nginx.ingress.kubernetes.io/from-to-www-redirect" = tostring(var.enable_www_redirect)
      # SignalR /hubs are long-lived WebSockets — keep the upstream sockets open.
      "nginx.ingress.kubernetes.io/proxy-read-timeout" = "3600"
      "nginx.ingress.kubernetes.io/proxy-send-timeout" = "3600"
      # Cookie affinity pins a client to one backend pod for the life of the session (feature 019).
      # The SignalR backplane (Redis) fans messages ACROSS pods, but it does not fix the negotiate
      # handshake: /hubs/negotiate returns a connection token bound to the pod that answered, and a
      # follow-up request round-robined to a different pod is rejected. With backend_replicas > 1
      # (prod runs 2, HPA to 6) both ChatHub (019) and NotificationHub (010) need this to connect at
      # all. Deliberately cookie affinity rather than skipNegotiation: skipNegotiation forfeits
      # SignalR's transport fallback on restrictive networks. See specs/019-chat/research.md §10.
      "nginx.ingress.kubernetes.io/affinity"               = "cookie"
      "nginx.ingress.kubernetes.io/affinity-mode"          = "persistent"
      "nginx.ingress.kubernetes.io/session-cookie-name"    = "jugger-affinity"
      "nginx.ingress.kubernetes.io/session-cookie-expires" = "3600"
      "nginx.ingress.kubernetes.io/session-cookie-max-age" = "3600"
    },
    var.enable_tls ? { "cert-manager.io/cluster-issuer" = var.cluster_issuer } : {},
  )
}

resource "kubernetes_namespace_v1" "app" {
  metadata {
    name   = var.namespace
    labels = { app = "juggerhub" }
  }
}

# --- Config & secrets -------------------------------------------------------
resource "kubernetes_config_map_v1" "app" {
  metadata {
    name      = "app-config"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    "ASPNETCORE_ENVIRONMENT" = var.aspnetcore_environment
    "Jwt__Issuer"            = var.jwt_issuer
    "Jwt__Audience"          = var.jwt_audience
    "Email__Provider"        = "Resend"
    "Email__FromAddress"     = var.email_from_address
    "Email__FrontendBaseUrl" = var.email_frontend_base_url
  }
}

resource "kubernetes_secret_v1" "app" {
  metadata {
    name      = "app-secrets"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    "ConnectionStrings__DefaultConnection" = var.connection_string
    "Jwt__SigningKey"                      = var.jwt_signing_key
    "Email__Resend__ApiKey"                = var.resend_api_key
    "Admin__Emails"                        = var.admin_emails
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "postgres" {
  metadata {
    name      = "postgres-secrets"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    "POSTGRES_USER"     = var.postgres_user
    "POSTGRES_PASSWORD" = var.postgres_password
    "POSTGRES_DB"       = var.postgres_db
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "ghcr" {
  # Optional: only needed for PRIVATE GHCR packages. With public packages, leave
  # ghcr_pull_token empty and pods pull anonymously (no imagePullSecret).
  count = local.ghcr_enabled ? 1 : 0

  metadata {
    name      = "ghcr-pull"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  type = "kubernetes.io/dockerconfigjson"
  data = {
    ".dockerconfigjson" = jsonencode({
      auths = {
        "ghcr.io" = {
          username = var.ghcr_username
          password = var.ghcr_pull_token
          auth     = base64encode("${var.ghcr_username}:${var.ghcr_pull_token}")
        }
      }
    })
  }
}

# --- PostgreSQL (in-cluster, never exposed beyond ClusterIP) ----------------
resource "kubernetes_service_v1" "postgres" {
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    cluster_ip = "None" # headless: stable DNS for the StatefulSet pod
    selector   = { app = "postgres" }
    port {
      port        = 5432
      target_port = 5432
    }
  }
}

resource "kubernetes_stateful_set_v1" "postgres" {
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
    labels    = { app = "postgres" }
  }
  spec {
    service_name = kubernetes_service_v1.postgres.metadata[0].name
    replicas     = 1
    selector {
      match_labels = { app = "postgres" }
    }
    template {
      metadata {
        labels = { app = "postgres" }
      }
      spec {
        container {
          name  = "postgres"
          image = "postgres:18.3-alpine"
          port {
            container_port = 5432
          }
          env_from {
            secret_ref {
              name = kubernetes_secret_v1.postgres.metadata[0].name
            }
          }
          # Postgres 18 stores data under a version subdir; mount at the parent
          # (matches docker-compose.yml).
          volume_mount {
            name       = "data"
            mount_path = "/var/lib/postgresql"
          }
          readiness_probe {
            exec {
              command = ["pg_isready", "-U", var.postgres_user, "-d", var.postgres_db]
            }
            initial_delay_seconds = 10
            period_seconds        = 10
          }
        }
      }
    }
    volume_claim_template {
      metadata {
        name = "data"
      }
      spec {
        access_modes       = ["ReadWriteOnce"]
        storage_class_name = var.postgres_storage_class
        resources {
          requests = {
            storage = "${var.postgres_storage_gb}Gi"
          }
        }
      }
    }
  }
}

# --- Backend ----------------------------------------------------------------
resource "kubernetes_deployment_v1" "backend" {
  metadata {
    name      = "backend"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
    labels    = { app = "backend" }
  }
  spec {
    replicas = var.backend_replicas
    selector {
      match_labels = { app = "backend" }
    }
    strategy {
      type = "RollingUpdate"
      rolling_update {
        max_unavailable = "0" # a bad image never displaces the healthy ReplicaSet
        max_surge       = "1"
      }
    }
    template {
      metadata {
        labels = { app = "backend" }
      }
      spec {
        dynamic "image_pull_secrets" {
          for_each = local.ghcr_enabled ? [1] : []
          content {
            name = kubernetes_secret_v1.ghcr[0].metadata[0].name
          }
        }
        container {
          name  = "backend"
          image = local.backend_image
          port {
            container_port = 8080
          }
          env_from {
            config_map_ref {
              name = kubernetes_config_map_v1.app.metadata[0].name
            }
          }
          env_from {
            secret_ref {
              name = kubernetes_secret_v1.app.metadata[0].name
            }
          }
          readiness_probe {
            http_get {
              path = "/api/v1/health"
              port = 8080
            }
            initial_delay_seconds = 15
            period_seconds        = 10
          }
          liveness_probe {
            http_get {
              path = "/api/v1/health"
              port = 8080
            }
            initial_delay_seconds = 30
            period_seconds        = 15
          }
        }
      }
    }
  }
}

resource "kubernetes_service_v1" "backend" {
  metadata {
    name      = "backend" # MUST be "backend": the frontend nginx upstream is hardcoded
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    selector = { app = "backend" }
    port {
      port        = 8080
      target_port = 8080
    }
  }
}

# --- Frontend ---------------------------------------------------------------
resource "kubernetes_deployment_v1" "frontend" {
  metadata {
    name      = "frontend"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
    labels    = { app = "frontend" }
  }
  spec {
    replicas = var.frontend_replicas
    selector {
      match_labels = { app = "frontend" }
    }
    template {
      metadata {
        labels = { app = "frontend" }
      }
      spec {
        dynamic "image_pull_secrets" {
          for_each = local.ghcr_enabled ? [1] : []
          content {
            name = kubernetes_secret_v1.ghcr[0].metadata[0].name
          }
        }
        container {
          name  = "frontend"
          image = local.frontend_image
          port {
            container_port = 80
          }

          # Analytics config reaches the ALREADY-BUILT image at container start: the nginx image's
          # own envsubst entrypoint renders /etc/nginx/templates into the live config. So the same
          # released image serves Dev and Prod with different analytics settings and no rebuild
          # (FR-020), and no Angular source is involved.
          env {
            name = "JH_ANALYTICS_HEAD"
            # Composed by Terraform rather than pasted anywhere. The snippet is carried into
            # nginx's SINGLE-quoted sub_filter argument, so one apostrophe in it stops nginx from
            # starting — every string below therefore uses double quotes, and building it here
            # means that cannot be got wrong by hand.
            #
            # Empty website ID renders an empty value, which makes the substitution a no-op and
            # ships no tracker at all. That is what lets an environment run with analytics off
            # without any conditional configuration.
            value = local.analytics_head
          }
          env {
            name  = "JH_ANALYTICS_UPSTREAM"
            value = "http://${kubernetes_service_v1.umami.metadata[0].name}:3000"
          }
          env {
            name = "JH_ANALYTICS_RESOLVER"
            # nginx proxies the analytics routes through a VARIABLE so it resolves them per
            # request. With a literal upstream it resolves at startup and refuses to start when the
            # name is missing — which would let an absent Umami take down the entire frontend.
            # Runtime resolution needs an explicit resolver, and there is no safe empty default.
            #
            # Read from the cluster rather than hardcoded: the kube-dns ClusterIP is assigned per
            # cluster, so a literal would be right in one environment and silently wrong in the next.
            value = data.kubernetes_service_v1.kube_dns.spec[0].cluster_ip
          }

          readiness_probe {
            http_get {
              path = "/"
              port = 80
            }
            initial_delay_seconds = 5
            period_seconds        = 10
          }
        }
      }
    }
  }
}

resource "kubernetes_service_v1" "frontend" {
  metadata {
    name      = "frontend"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    selector = { app = "frontend" }
    port {
      port        = 80
      target_port = 80
    }
  }
}

# --- Ingress (single origin; frontend nginx proxies /api and /hubs) ---------
resource "kubernetes_ingress_v1" "app" {
  metadata {
    name        = "juggerhub"
    namespace   = kubernetes_namespace_v1.app.metadata[0].name
    annotations = local.ingress_annotations
  }
  spec {
    ingress_class_name = var.ingress_class_name
    rule {
      host = var.app_hostname
      http {
        path {
          path      = "/"
          path_type = "Prefix"
          backend {
            service {
              name = kubernetes_service_v1.frontend.metadata[0].name
              port {
                number = 80
              }
            }
          }
        }
      }
    }
    dynamic "tls" {
      for_each = var.enable_tls ? [1] : []
      content {
        hosts       = local.tls_hosts
        secret_name = "${replace(var.app_hostname, ".", "-")}-tls"
      }
    }
  }
}

# --- Backend HPA (prod) -----------------------------------------------------
resource "kubernetes_horizontal_pod_autoscaler_v2" "backend" {
  count = var.enable_backend_hpa ? 1 : 0

  metadata {
    name      = "backend"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    min_replicas = var.backend_replicas
    max_replicas = var.backend_hpa_max_replicas
    scale_target_ref {
      api_version = "apps/v1"
      kind        = "Deployment"
      name        = kubernetes_deployment_v1.backend.metadata[0].name
    }
    metric {
      type = "Resource"
      resource {
        name = "cpu"
        target {
          type                = "Utilization"
          average_utilization = var.backend_hpa_cpu_target
        }
      }
    }
  }
}
