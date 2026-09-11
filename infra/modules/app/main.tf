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
    name = var.namespace
    labels = {
      app = "juggerhub"
      # Pod Security Admission (#252) makes the hardening structural: a future workload that runs
      # privileged or mounts the host is REJECTED at admission instead of relying on review.
      #
      # ENFORCE is `baseline`, not `restricted`, deliberately. Every workload in this module now
      # meets `restricted`, but cert-manager also creates short-lived HTTP-01 solver pods in this
      # namespace to renew the public certificates. If one of those were rejected, renewal would
      # fail silently and TLS would expire weeks later. `restricted` is warned + audited here, so
      # any violation is loud; promote enforce to `restricted` once a renewal has been observed
      # passing under it (a server-side dry run of the label shows violations without applying).
      "pod-security.kubernetes.io/enforce" = "baseline"
      "pod-security.kubernetes.io/warn"    = "restricted"
      "pod-security.kubernetes.io/audit"   = "restricted"
    }
  }
}

# Defaults for any container in the namespace that declares no resources of its own (#254) — the
# short-lived psql Jobs and initContainers today, and whatever is added next. Without it such a
# container is unbounded and invisible to the scheduler. Workloads that matter set their own.
resource "kubernetes_limit_range_v1" "app" {
  metadata {
    name      = "defaults"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    limit {
      type = "Container"
      default_request = {
        cpu    = "10m"
        memory = "32Mi"
      }
      default = {
        memory = "256Mi"
      }
    }
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
    # Feature 035 — media object storage. Only the container name is non-sensitive; the
    # connection string carries the account key and lives in the Secret below.
    "MediaStorage__ContainerName" = var.media_storage_container_name
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
    # Feature 047 — chat message encryption. The Secret, never the ConfigMap: this IS the thing
    # that keeps a database copy unreadable, so putting it beside the database would defeat it.
    "Chat__Encryption__Keys" = var.chat_encryption_keys
    "Email__Resend__ApiKey"  = var.resend_api_key
    "Admin__Emails"          = var.admin_emails
    # Feature 035 — carries the storage account key, so it belongs here and not in the ConfigMap.
    "MediaStorage__ConnectionString" = var.media_storage_connection_string
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

# --- PostgreSQL TLS (feature 047 / #223) ------------------------------------
# The backend connects with `SSL Mode=VerifyFull`, so the database needs a certificate and the
# backend needs the authority that signed it. cert-manager is already installed by the platform
# module and already mints the public ingress certificate; an internal CA is the right trust anchor
# here because the database is not publicly resolvable and ACME could never validate it.
#
# Three objects: a self-signed issuer to bootstrap, a CA certificate it signs, and a CA issuer that
# signs the server certificate.
#
# ⚠ BOTH CERTIFICATES SET AN EXPLICIT LONG `duration`, AND THAT IS DELIBERATE.
#
# cert-manager's default is 90 days, renewed when 30 days remain — correct for the public ingress
# certificate, and a scheduled outage here. The reason is downstream: **PostgreSQL does not re-read
# `ssl_cert_file` by itself.** It is a SIGHUP-context setting, so a reload would pick up a renewed
# certificate, but nothing in this deployment sends one. cert-manager would rotate the Secret, the
# kubelet would update the file in the pod, and Postgres would carry on serving the OLD certificate
# until the pod happened to restart. Inside the 30-day overlap that self-heals invisibly; past it
# the served certificate expires, `VerifyFull` starts refusing, and the backend loses the database
# roughly three months after an apply with nothing in the diff to point at.
#
# Short-lived certificates buy a smaller compromise window, which is worth real operational cost
# for a certificate the public presents to. This is a private CA on a hop only the backend can
# reach, so the trade runs the other way: rotation becomes a deliberate operator action (see
# infra/README.md) instead of a timer nobody set.
#
# Revisiting this properly — automatic rotation with something that reloads Postgres when the
# Secret changes — is tracked as GH #239. Until that lands, do NOT shorten these durations: doing
# so re-arms exactly the failure above.
#
# NOTE the same two-phase-apply constraint the ClusterIssuers carry: kubernetes_manifest resolves
# the CRD schema at PLAN time, so on a fresh cluster cert-manager must be applied first. See
# infra/README.md.
resource "kubernetes_manifest" "postgres_selfsigned_issuer" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "Issuer"
    metadata = {
      name      = "postgres-selfsigned"
      namespace = kubernetes_namespace_v1.app.metadata[0].name
    }
    spec = { selfSigned = {} }
  }
}

resource "kubernetes_manifest" "postgres_ca_certificate" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "Certificate"
    metadata = {
      name      = "postgres-ca"
      namespace = kubernetes_namespace_v1.app.metadata[0].name
    }
    spec = {
      isCA       = true
      commonName = "juggerhub-postgres-ca"
      secretName = "postgres-ca"
      # 10 years, renewed a year out — it must comfortably outlive every certificate it signs, or
      # leaves would be chaining to an authority that expires before they do.
      duration    = "87600h"
      renewBefore = "8760h"
      privateKey  = { algorithm = "RSA", size = 4096 }
      issuerRef = {
        name  = kubernetes_manifest.postgres_selfsigned_issuer.manifest.metadata.name
        kind  = "Issuer"
        group = "cert-manager.io"
      }
    }
  }
}

resource "kubernetes_manifest" "postgres_ca_issuer" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "Issuer"
    metadata = {
      name      = "postgres-ca-issuer"
      namespace = kubernetes_namespace_v1.app.metadata[0].name
    }
    spec = { ca = { secretName = "postgres-ca" } }
  }
  depends_on = [kubernetes_manifest.postgres_ca_certificate]
}

resource "kubernetes_manifest" "postgres_server_certificate" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "Certificate"
    metadata = {
      name      = "postgres-tls"
      namespace = kubernetes_namespace_v1.app.metadata[0].name
    }
    spec = {
      secretName = "postgres-tls"
      # 5 years, matching what scripts/dev-postgres-certs.ps1 issues locally so the two environments
      # age the same way. renewBefore is set SMALL and explicitly: left unset it defaults to a third
      # of the duration, which would schedule the first automatic renewal — and the silent
      # stale-certificate problem above — about twenty months from now rather than in five years.
      duration    = "43800h"
      renewBefore = "720h"
      # `postgres` — the bare service name — is LOAD-BEARING and must stay first: the connection
      # string says Host=postgres, and VerifyFull matches the host string as written, not the name
      # it resolves to. Dropping it here breaks the backend with a hostname-mismatch error that
      # reads nothing like a missing SAN.
      dnsNames = [
        "postgres",
        "postgres.${kubernetes_namespace_v1.app.metadata[0].name}.svc",
        "postgres.${kubernetes_namespace_v1.app.metadata[0].name}.svc.cluster.local",
      ]
      usages = ["server auth"]
      issuerRef = {
        name  = kubernetes_manifest.postgres_ca_issuer.manifest.metadata.name
        kind  = "Issuer"
        group = "cert-manager.io"
      }
    }
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
        # fs_group 70 is the `postgres` uid/gid inside postgres:18.3-alpine (verified against the
        # image, not assumed). Together with default_mode 0640 on the certificate volume below it
        # satisfies PostgreSQL's key-file rule — which is NOT simply "0600": a key owned by the
        # database user may have no group or world bits at all, while a root-owned key may be
        # group-readable. A Kubernetes secret volume produces root-owned files, so 0640 + this
        # group is accepted. 0644 is not, and fails as a crash loop whose message says nothing
        # about TLS.
        #
        # run_as_user 70 (#252): the image's entrypoint normally starts as root and drops to
        # `postgres` itself; started as 70 it skips that step and needs no capabilities at all. Safe
        # on Dev's existing volume (PGDATA is already owned by 70) and on a fresh one (fs_group
        # makes the mount group-writable, so initdb can create PGDATA as 70). Both proven locally
        # against postgres:18.3-alpine before this went in.
        security_context {
          fs_group        = 70
          run_as_user     = 70
          run_as_group    = 70
          run_as_non_root = true
          seccomp_profile {
            type = "RuntimeDefault"
          }
        }
        automount_service_account_token = false
        container {
          name  = "postgres"
          image = "postgres:18.3-alpine"
          # run_as_non_root is repeated on EVERY container block in this module, and that is not
          # redundancy: the kubernetes provider sends `runAsNonRoot: false` for a container
          # security_context that omits it, and a container-level value OVERRIDES the pod-level
          # `true`. Drop it and the kubelet stops enforcing non-root and Pod Security `restricted`
          # fails — observed on Dev after #252 shipped without it.
          security_context {
            run_as_non_root            = true
            allow_privilege_escalation = false
            read_only_root_filesystem  = true
            capabilities {
              drop = ["ALL"]
            }
          }
          # TLS on (feature 047). This ENABLES encryption; it does not refuse plaintext clients —
          # forcing that means replacing pg_hba.conf on an already-initialised volume, which is a
          # plausible way to break the database for a gain only against our own misconfiguration.
          # Every client we ship asks for TLS and the backend additionally verifies. Recorded as a
          # residual in specs/047-chat-message-encryption/research.md §8.
          args = [
            "-c", "ssl=on",
            "-c", "ssl_cert_file=/etc/postgres-certs/tls.crt",
            "-c", "ssl_key_file=/etc/postgres-certs/tls.key",
          ]
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
          volume_mount {
            name       = "tls"
            mount_path = "/etc/postgres-certs"
            read_only  = true
          }
          # The only paths Postgres writes outside PGDATA, with the root filesystem read-only: the
          # Unix socket + lock file (pg_isready below connects through it) and scratch space.
          volume_mount {
            name       = "run"
            mount_path = "/var/run/postgresql"
          }
          volume_mount {
            name       = "tmp"
            mount_path = "/tmp"
          }
          readiness_probe {
            exec {
              command = ["pg_isready", "-U", var.postgres_user, "-d", var.postgres_db]
            }
            initial_delay_seconds = 10
            period_seconds        = 10
          }
          # #254. The memory limit is set well clear of normal use on purpose: hitting it OOM-kills
          # the only database replica, which is an outage, not a slowdown. No CPU limit — throttling
          # a database stalls every query behind it.
          resources {
            requests = {
              cpu    = var.postgres_cpu_request
              memory = var.postgres_memory_request
            }
            limits = {
              memory = var.postgres_memory_limit
            }
          }
        }
        volume {
          name = "tls"
          secret {
            secret_name  = "postgres-tls" # written by cert-manager
            default_mode = "0640"
          }
        }
        volume {
          name = "run"
          empty_dir {
            size_limit = "16Mi"
          }
        }
        volume {
          name = "tmp"
          empty_dir {
            size_limit = "256Mi"
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
        # #252. 1654 is the aspnet base image's `app` user, which the Dockerfile now runs as; stated
        # numerically so runAsNonRoot can verify it. The backend never talks to the Kubernetes API,
        # so it gets no service-account token to steal.
        security_context {
          run_as_user     = 1654
          run_as_group    = 1654
          run_as_non_root = true
          seccomp_profile {
            type = "RuntimeDefault"
          }
        }
        automount_service_account_token = false
        container {
          name  = "backend"
          image = local.backend_image
          security_context {
            run_as_non_root            = true
            allow_privilege_escalation = false
            read_only_root_filesystem  = true
            capabilities {
              drop = ["ALL"]
            }
          }
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
          # The authority that signed the database's certificate (feature 047). Mounted at the same
          # path docker-compose uses, so the connection string is character-identical in every
          # environment (Principle V).
          volume_mount {
            name       = "postgres-ca"
            mount_path = "/etc/juggerhub/certs"
            read_only  = true
          }
          # The only writable path under the read-only root (mirrored as tmpfs in
          # docker-compose.yml). The Data Protection key ring is in Postgres (#250), so nothing
          # else here writes to disk.
          volume_mount {
            name       = "tmp"
            mount_path = "/tmp"
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
          # #254. The CPU REQUEST is load-bearing beyond scheduling: the HPA below measures
          # utilisation as a percentage of it, and with no request it reports <unknown> and never
          # scales. No CPU limit — throttling hurts request latency more than it protects the node.
          # The memory limit also sizes the .NET GC, which reads the container limit for its heap.
          resources {
            requests = {
              cpu    = var.backend_cpu_request
              memory = var.backend_memory_request
            }
            limits = {
              memory = var.backend_memory_limit
            }
          }
        }
        volume {
          name = "postgres-ca"
          secret {
            secret_name = "postgres-ca" # written by cert-manager; only ca.crt is projected
            items {
              key  = "ca.crt"
              path = "ca.crt"
            }
          }
        }
        volume {
          name = "tmp"
          empty_dir {
            size_limit = "256Mi"
          }
        }
      }
    }
  }
  depends_on = [kubernetes_manifest.postgres_server_certificate]
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
        # #252. 101 is the `nginx` user of nginx-unprivileged, which the image runs as.
        security_context {
          run_as_user     = 101
          run_as_group    = 101
          run_as_non_root = true
          seccomp_profile {
            type = "RuntimeDefault"
          }
        }
        automount_service_account_token = false
        container {
          name  = "frontend"
          image = local.frontend_image
          security_context {
            run_as_non_root            = true
            allow_privilege_escalation = false
            read_only_root_filesystem  = true
            capabilities {
              drop = ["ALL"]
            }
          }
          # 8080: unprivileged nginx cannot bind 80. The Service below still listens on 80, so the
          # ingress is untouched.
          port {
            container_port = 8080
          }
          # The image's envsubst entrypoint renders the config TEMPLATE into conf.d at start, so it
          # must be writable; the pid file and every nginx temp path are under /tmp. Mirrored as
          # tmpfs in docker-compose.yml.
          volume_mount {
            name       = "conf"
            mount_path = "/etc/nginx/conf.d"
          }
          volume_mount {
            name       = "tmp"
            mount_path = "/tmp"
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
            # Feature 038. The website id on its own, because the nginx location for the
            # recorder's configuration endpoint is an EXACT match and must contain it. A prefix
            # location would have avoided this variable at the cost of proxying Umami's entire
            # API — including /api/auth/login — from the application's own origin.
            #
            # Empty when analytics is off, which renders a valid location nothing ever requests.
            name  = "JH_ANALYTICS_WEBSITE_ID"
            value = var.umami_website_id
          }
          env {
            name = "JH_ANALYTICS_UPSTREAM"
            # FULLY QUALIFIED, and that is mandatory here even though `umami` resolves fine from a
            # shell in this very pod. Those are two different resolvers: a shell goes through libc,
            # which appends the search domains in /etc/resolv.conf (juggerhub.svc.cluster.local,
            # ...). nginx's `resolver` directive talks to kube-dns directly and applies NO search
            # domains, so it asks for the literal name `umami` and gets NXDOMAIN —
            #   [error] umami could not be resolved (3: Host not found)
            # which surfaces as a 502 on every tracker request.
            #
            # Local compose keeps the short name because Docker's embedded DNS resolves bare
            # service names natively. That difference is why this passed every local test and
            # failed on the first deploy.
            value = "http://${kubernetes_service_v1.umami.metadata[0].name}.${kubernetes_service_v1.umami.metadata[0].namespace}.svc.cluster.local:3000"
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
              port = 8080
            }
            initial_delay_seconds = 5
            period_seconds        = 10
          }
          # #254. nginx serving static files and proxying: a few MiB at idle on Dev.
          resources {
            requests = {
              cpu    = "10m"
              memory = "32Mi"
            }
            limits = {
              memory = "128Mi"
            }
          }
        }
        volume {
          name = "conf"
          empty_dir {
            size_limit = "1Mi"
          }
        }
        volume {
          name = "tmp"
          empty_dir {
            # Generous on purpose: nginx spills proxied responses (media) and request bodies (the
            # 2m recorder payload) to /tmp, and exceeding an emptyDir limit EVICTS the pod.
            size_limit = "256Mi"
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
      target_port = 8080
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
# Utilization is measured against the backend container's CPU REQUEST (backend_cpu_request). Remove
# that request and this HPA silently stops scaling — it shows <unknown> and holds min_replicas.
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
