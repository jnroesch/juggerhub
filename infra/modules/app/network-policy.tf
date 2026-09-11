# Network isolation for the app namespace (#253).
#
# Default-deny INGRESS, then one allow per real connection. Written as the list of who may talk to
# whom, so the file reads as the traffic diagram:
#
#   ingress controller ─▶ frontend:8080        umami:3000 (dashboard host)      acme solver:8089
#   frontend ──────────▶ backend:8080          umami:3000 (same-origin analytics proxy)
#   umami-post-deploy ─▶ umami:3000            (waits for Umami's heartbeat before provisioning)
#   backend, umami, umami-post-deploy, umami-replay-retention ─▶ postgres:5432
#
# Why it matters beyond hygiene: two services here trust the network. Postgres ENABLES TLS but does
# not refuse plaintext clients (047 residual — see the StatefulSet comment in main.tf), and Redis
# (#219) will have no authentication. Without these policies any compromised pod could reach both.
#
# ENFORCEMENT NEEDS AN ENGINE. The AKS module sets network_policy = "cilium"; without it every
# object below is accepted by the API server and silently ignored.
#
# Deliberately NOT restricted:
#   - Egress. The backend calls Resend and Azure Blob by hostname, which a plain NetworkPolicy
#     cannot express; FQDN egress rules are a Cilium-specific follow-up.
#   - Kubelet health probes. They originate from the node, which Cilium admits to local pods
#     regardless of policy.
#
# ADDING A WORKLOAD? It receives nothing until a policy here admits it. A new caller of Postgres
# must be added to the postgres policy's list, or it fails to connect with a timeout that looks
# like a networking fault rather than a missing line in this file. Redis (#219) needs its own
# policy admitting the backend on 6379.

locals {
  ingress_ns_selector = { "kubernetes.io/metadata.name" = var.ingress_namespace }
}

resource "kubernetes_network_policy_v1" "default_deny_ingress" {
  metadata {
    name      = "default-deny-ingress"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    pod_selector {}
    policy_types = ["Ingress"]
  }
}

resource "kubernetes_network_policy_v1" "frontend" {
  metadata {
    name      = "allow-frontend-from-ingress"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    pod_selector {
      match_labels = { app = "frontend" }
    }
    ingress {
      from {
        namespace_selector {
          match_labels = local.ingress_ns_selector
        }
      }
      ports {
        port     = "8080"
        protocol = "TCP"
      }
    }
    policy_types = ["Ingress"]
  }
}

resource "kubernetes_network_policy_v1" "backend" {
  metadata {
    name      = "allow-backend-from-frontend"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    pod_selector {
      match_labels = { app = "backend" }
    }
    # The frontend nginx is the backend's ONLY client: /api/ and /hubs/ are both proxied through it.
    # If #249 is fixed by routing /hubs from the ingress straight to the backend, the ingress
    # namespace must be admitted here too — otherwise every realtime connection is refused.
    ingress {
      from {
        pod_selector {
          match_labels = { app = "frontend" }
        }
      }
      ports {
        port     = "8080"
        protocol = "TCP"
      }
    }
    policy_types = ["Ingress"]
  }
}

resource "kubernetes_network_policy_v1" "umami" {
  metadata {
    name      = "allow-umami"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    pod_selector {
      match_labels = { app = "umami" }
    }
    ingress {
      # The dashboard Ingress (analytics host), the frontend's same-origin tracker/recorder proxy,
      # and the post-deploy Job, which polls /api/heartbeat before it provisions anything.
      from {
        namespace_selector {
          match_labels = local.ingress_ns_selector
        }
      }
      from {
        pod_selector {
          match_labels = { app = "frontend" }
        }
      }
      from {
        pod_selector {
          match_labels = { app = "umami-post-deploy" }
        }
      }
      ports {
        port     = "3000"
        protocol = "TCP"
      }
    }
    policy_types = ["Ingress"]
  }
}

resource "kubernetes_network_policy_v1" "postgres" {
  metadata {
    name      = "allow-postgres-from-clients"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    pod_selector {
      match_labels = { app = "postgres" }
    }
    ingress {
      from {
        pod_selector {
          match_expressions {
            key      = "app"
            operator = "In"
            # Every workload that opens a database connection. Umami's db-init initContainer runs
            # inside the umami pod and is covered by its label.
            values = ["backend", "umami", "umami-post-deploy", "umami-replay-retention"]
          }
        }
      }
      ports {
        port     = "5432"
        protocol = "TCP"
      }
    }
    policy_types = ["Ingress"]
  }
}

# cert-manager answers Let's Encrypt HTTP-01 challenges from short-lived solver pods it creates in
# THIS namespace (the namespace of the Ingress being certified), reached through the ingress
# controller. Blocking them breaks nothing today and fails every certificate RENEWAL weeks from
# now — silently, until TLS expires. The label and port are cert-manager's own.
resource "kubernetes_network_policy_v1" "acme_solver" {
  metadata {
    name      = "allow-acme-http01-solver"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    pod_selector {
      match_labels = { "acme.cert-manager.io/http01-solver" = "true" }
    }
    ingress {
      from {
        namespace_selector {
          match_labels = local.ingress_ns_selector
        }
      }
      ports {
        port     = "8089"
        protocol = "TCP"
      }
    }
    policy_types = ["Ingress"]
  }
}
