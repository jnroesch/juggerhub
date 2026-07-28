# Self-hosted Umami analytics (feature 033).
#
# In its own file rather than main.tf purely for reviewability — main.tf is already 391 lines and
# this adds a workload, its provisioning, and a second Ingress. Terraform composes the module from
# every .tf in the directory, so placement carries no meaning.
#
# Shape of the thing: Umami runs beside the app, its data lives in a SEPARATE database on the
# EXISTING Postgres StatefulSet under a scoped non-superuser role, measurement is proxied
# same-origin through the frontend's nginx (so no blocklist rule matches it), and only the
# DASHBOARD gets its own hostname — because Umami's BASE_PATH is baked at build time and `/api/`
# on the app origin already belongs to the .NET backend.

# The cluster's DNS service, so the frontend's nginx resolver is read from the cluster instead of
# hardcoded. kube-dns's ClusterIP is assigned per cluster, so a literal would be correct in one
# environment and silently wrong in the next — and "silently" is the problem: measurement would
# simply stop, with the application unaffected and nothing to notice.
data "kubernetes_service_v1" "kube_dns" {
  metadata {
    name      = "kube-dns"
    namespace = "kube-system"
  }
}

locals {
  umami_image = "${var.umami_image}:${var.umami_image_tag}"

  # The <head> snippet injected into index.html by the frontend's nginx.
  #
  # Assembled here so the quoting is settled once in code. It ends up inside nginx's SINGLE-quoted
  # sub_filter argument, so a single apostrophe anywhere in it stops nginx from starting — every
  # string literal below is therefore double-quoted, which JavaScript treats identically and nginx
  # does not.
  #
  # An empty website ID yields an empty value, which makes the nginx substitution a no-op and ships
  # no tracker. Analytics-off needs no conditional configuration anywhere.
  analytics_head = var.umami_website_id == "" ? "" : join("", [
    "<script>(function(){",
    # Global Privacy Control and Do Not Track are checked BEFORE the tracker is injected, so a
    # visitor who has signalled either generates no request at all — not merely an ignored one.
    # Umami's own data-do-not-track covers DNT only; GPC it does not implement (FR-007).
    "var n=navigator;",
    "if(n.globalPrivacyControl||n.doNotTrack===\"1\"||window.doNotTrack===\"1\")return;",
    "var s=document.createElement(\"script\");",
    # async + defer: measurement must never delay first render (FR-012), and an appended async
    # script cannot block. A failed or slow load is invisible (FR-011).
    "s.async=true;s.defer=true;",
    "s.src=\"/jh-insights.js\";",
    "s.setAttribute(\"data-website-id\",\"${var.umami_website_id}\");",
    "s.setAttribute(\"data-do-not-track\",\"true\");",
    # Query strings are not recorded (FR-008a). Without this Umami stores url_query beside
    # url_path, which recorded /sign-in carrying its returnUrl — and returnUrl holds deep links
    # such as /players/<handle>. FR-008 made page PATHS verbatim; it did not cover queries.
    "s.setAttribute(\"data-exclude-search\",\"true\");",
    # No data-host-url: Umami defaults to sending where the script was served from, which is our
    # own origin. Setting it would duplicate a value that can then drift.
    # No identify() and no data-tag: nothing may link an event to a member (FR-005).
    "document.head.appendChild(s);})();</script>",
  ])

  # Assembled here rather than passed in, so the password stays a single sensitive variable and
  # never appears in tfvars as part of a longer string. `postgres` is the headless Service.
  umami_database_url = "postgresql://umami:${var.umami_db_password}@postgres:5432/umami"

  # The post-deploy Job is named after a digest of everything that decides what it DOES. A
  # Kubernetes Job's pod spec is immutable, so a Job that already exists is never re-run and a
  # changed password hash or website ID would otherwise be silently ignored until someone deleted
  # the Job by hand. Rotating a secret must take effect on the next deploy, so the name has to
  # change with the inputs.
  #
  # nonsensitive() because a resource name cannot be derived from a sensitive value; a truncated
  # SHA-256 of the hash discloses nothing (and the input is itself already a hash).
  umami_job_digest = nonsensitive(substr(sha256(join("|", [
    file("${path.module}/../../../scripts/umami-set-admin-password.sql"),
    file("${path.module}/../../../scripts/umami-seed-website.sql"),
    var.umami_admin_password_hash,
    var.umami_website_id,
  ])), 0, 10))
}

# --- Provisioning SQL -------------------------------------------------------
# Mounted from the repo rather than inlined as heredocs, so scripts/*.sql stays the single source
# of truth and the EXACT text exercised locally is what runs in the cluster. Inlining would let
# the two drift, and the drift would only surface as a failed deploy.
resource "kubernetes_config_map_v1" "umami_sql" {
  metadata {
    name      = "umami-sql"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    "umami-db-init.sql"            = file("${path.module}/../../../scripts/umami-db-init.sql")
    "umami-seed-website.sql"       = file("${path.module}/../../../scripts/umami-seed-website.sql")
    "umami-set-admin-password.sql" = file("${path.module}/../../../scripts/umami-set-admin-password.sql")
  }
}

# --- Config & secrets -------------------------------------------------------
resource "kubernetes_config_map_v1" "umami" {
  metadata {
    name      = "umami-config"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    # Moves the beacon off /api/send. REQUIRED, not cosmetic: the tracker posts to this path on the
    # APP's origin, where /api/ is already proxied to the .NET backend. This rewrites the URL baked
    # into script.js at container start.
    #
    # It does NOT create a server route — Umami still accepts beacons at /api/send only, and the
    # frontend nginx maps /jh-insights/e onto it. Verified empirically; the documentation implies
    # otherwise. TRACKER_SCRIPT_NAME is deliberately absent: it has no effect in this image
    # (standalone Next.js build, rewrites baked at build time), so nginx renames the script too.
    "COLLECT_API_ENDPOINT" = "/jh-insights/e"

    # No outbound call-home and no update checks: the version is pinned by us (FR-009).
    "DISABLE_TELEMETRY" = "1"
    "DISABLE_UPDATES"   = "1"
  }
}

resource "kubernetes_secret_v1" "umami" {
  metadata {
    name      = "umami-secrets"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    "DATABASE_URL" = local.umami_database_url

    # Signs dashboard session tokens. A shared or default value makes sessions forgeable, which is
    # why this is a per-environment secret and not a variable with a default.
    "APP_SECRET" = var.umami_app_secret
  }
  type = "Opaque"
}

# --- Umami ------------------------------------------------------------------
resource "kubernetes_deployment_v1" "umami" {
  metadata {
    name      = "umami"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
    labels    = { app = "umami" }
  }
  spec {
    replicas = var.umami_replicas
    selector {
      match_labels = { app = "umami" }
    }
    template {
      metadata {
        labels = { app = "umami" }
      }
      spec {
        # Creates the `umami` role and database and revokes its access to the application database,
        # before Umami starts — Prisma's migrations fail against a database that does not exist.
        #
        # An initContainer rather than /docker-entrypoint-initdb.d/, which only runs when
        # initialising an EMPTY data directory. Dev and Prod volumes are already initialised, so a
        # script placed there would be a SILENT no-op: the deploy would look healthy and Umami
        # would fail to connect, with nothing pointing at the cause.
        init_container {
          name  = "db-init"
          image = "postgres:18.3-alpine" # same image as the StatefulSet

          # Inline shell in a container command, not a .sh file (constitution VI) — the same shape
          # as the pg_isready exec probes already in this module. The wait matters on a cold
          # environment, where Postgres and Umami are scheduled together and psql would otherwise
          # fail before the database is accepting connections.
          command = [
            "sh", "-c",
            join(" ", [
              "until pg_isready -h postgres -U \"$POSTGRES_USER\" >/dev/null 2>&1;",
              "do echo 'waiting for postgres'; sleep 2; done;",
              "PGPASSWORD=\"$POSTGRES_PASSWORD\" psql -h postgres -U \"$POSTGRES_USER\" -d postgres",
              "-v ON_ERROR_STOP=1",
              "-v umami_password=\"$UMAMI_DB_PASSWORD\"",
              "-v app_db=\"$POSTGRES_DB\"",
              "-f /sql/umami-db-init.sql",
            ])
          ]

          # The superuser credential is read here and NEVER given to the Umami container itself.
          env_from {
            secret_ref {
              name = kubernetes_secret_v1.postgres.metadata[0].name
            }
          }
          env {
            name = "UMAMI_DB_PASSWORD"
            value_from {
              secret_key_ref {
                name = kubernetes_secret_v1.umami_db.metadata[0].name
                key  = "password"
              }
            }
          }
          volume_mount {
            name       = "sql"
            mount_path = "/sql"
            read_only  = true
          }
        }

        container {
          name  = "umami"
          image = local.umami_image
          port {
            container_port = 3000
          }
          env_from {
            config_map_ref {
              name = kubernetes_config_map_v1.umami.metadata[0].name
            }
          }
          env_from {
            secret_ref {
              name = kubernetes_secret_v1.umami.metadata[0].name
            }
          }

          # /api/heartbeat, established by probing the running image (T003): /api/health and
          # /heartbeat both 404, so a guessed path would have failed every pod indefinitely.
          readiness_probe {
            http_get {
              path = "/api/heartbeat"
              port = 3000
            }
            initial_delay_seconds = 15
            period_seconds        = 10
          }
          liveness_probe {
            http_get {
              path = "/api/heartbeat"
              port = 3000
            }
            # Generous: the first start runs 20 Prisma migrations, and a liveness kill mid-migration
            # would restart the pod into the same work.
            initial_delay_seconds = 90
            period_seconds        = 15
          }

          # Bounded on purpose, unlike the first-party workloads in main.tf. Analytics is a
          # third-party workload on a shared node, and it must not be able to starve the
          # application it is measuring (constitution VII).
          resources {
            requests = {
              cpu    = "50m"
              memory = "256Mi"
            }
            limits = {
              memory = "512Mi"
            }
          }
        }

        volume {
          name = "sql"
          config_map {
            name = kubernetes_config_map_v1.umami_sql.metadata[0].name
          }
        }
      }
    }
  }

  depends_on = [kubernetes_stateful_set_v1.postgres]
}

# Held separately from umami-secrets because the db-init initContainer needs the password on its
# own, and env_from on umami-secrets would hand that container DATABASE_URL and APP_SECRET too.
resource "kubernetes_secret_v1" "umami_db" {
  metadata {
    name      = "umami-db"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    "password" = var.umami_db_password
  }
  type = "Opaque"
}

resource "kubernetes_service_v1" "umami" {
  metadata {
    name      = "umami" # the frontend nginx resolves this name for the same-origin proxy routes
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    selector = { app = "umami" }
    port {
      port        = 3000
      target_port = 3000
    }
  }
}

# --- Post-deploy provisioning ----------------------------------------------
# Closes the default credential and creates the tracked website, in one Job because both need the
# same thing: Umami must have MIGRATED and SEEDED first. Neither can go in the db-init
# initContainer, which runs before Umami has ever started and therefore before `user` and
# `website` exist.
resource "kubernetes_job_v1" "umami_post_deploy" {
  metadata {
    # Name carries an input digest so a rotated password hash or a changed website ID produces a
    # NEW Job. See local.umami_job_digest.
    name      = "umami-post-deploy-${local.umami_job_digest}"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  spec {
    backoff_limit = 6
    template {
      metadata {
        labels = { app = "umami-post-deploy" }
      }
      spec {
        restart_policy = "OnFailure"
        container {
          name  = "provision"
          image = "postgres:18.3-alpine"
          command = [
            "sh", "-c",
            join(" ", [
              # Wait for Umami itself, not just Postgres: the tables these statements touch are
              # created by Prisma when Umami first starts, and the admin row is seeded after that.
              "until wget -q --spider http://umami:3000/api/heartbeat;",
              "do echo 'waiting for umami'; sleep 3; done;",
              "PGPASSWORD=\"$UMAMI_DB_PASSWORD\" psql -h postgres -U umami -d umami",
              "-v ON_ERROR_STOP=1",
              "-v password_hash=\"$UMAMI_ADMIN_PASSWORD_HASH\"",
              "-f /sql/umami-set-admin-password.sql &&",
              "PGPASSWORD=\"$UMAMI_DB_PASSWORD\" psql -h postgres -U umami -d umami",
              "-v ON_ERROR_STOP=1",
              "-v website_id=\"$UMAMI_WEBSITE_ID\"",
              "-v website_name=\"$UMAMI_WEBSITE_NAME\"",
              "-v website_domain=\"$UMAMI_WEBSITE_DOMAIN\"",
              "-f /sql/umami-seed-website.sql",
            ])
          ]
          env {
            name = "UMAMI_DB_PASSWORD"
            value_from {
              secret_key_ref {
                name = kubernetes_secret_v1.umami_db.metadata[0].name
                key  = "password"
              }
            }
          }
          env {
            name = "UMAMI_ADMIN_PASSWORD_HASH"
            value_from {
              secret_key_ref {
                name = kubernetes_secret_v1.umami_admin.metadata[0].name
                key  = "password_hash"
              }
            }
          }
          env {
            name  = "UMAMI_WEBSITE_ID"
            value = var.umami_website_id
          }
          env {
            name  = "UMAMI_WEBSITE_NAME"
            value = "JuggerHub"
          }
          env {
            name  = "UMAMI_WEBSITE_DOMAIN"
            value = var.app_hostname
          }
          volume_mount {
            name       = "sql"
            mount_path = "/sql"
            read_only  = true
          }
        }
        volume {
          name = "sql"
          config_map {
            name = kubernetes_config_map_v1.umami_sql.metadata[0].name
          }
        }
      }
    }
  }

  # Terraform blocks until the Job succeeds, so a failure to close the default credential fails the
  # DEPLOY rather than leaving a publicly reachable dashboard on admin/umami.
  wait_for_completion = true
  timeouts {
    create = "10m"
    update = "10m"
  }

  depends_on = [kubernetes_deployment_v1.umami]
}

# The bcrypt hash of the dashboard password. Umami offers no environment variable for it
# (research.md §4) and seeds a documented admin/umami credential, so it is overwritten by the
# deploy. Only ever the HASH: the plaintext exists in the GitHub Environment and nowhere else.
resource "kubernetes_secret_v1" "umami_admin" {
  metadata {
    name      = "umami-admin"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
  }
  data = {
    "password_hash" = var.umami_admin_password_hash
  }
  type = "Opaque"
}

# --- Dashboard Ingress ------------------------------------------------------
# A hostname of its own, which is the one place this feature spends a DNS record. Umami's BASE_PATH
# is BUILD-time, so serving the dashboard under a path on the app origin would mean forking and
# rebuilding a third-party Next.js app — and /api/ there is already the .NET backend.
#
# Measurement does NOT depend on this: the tracker and collection endpoint are same-origin on the
# app hostname, so a blocklist entry for this host would only inconvenience the owner reading their
# own dashboard (FR-015 as amended).
#
# REQUIRES A MANUALLY CREATED DNS A RECORD pointing at the existing static public IP, per
# environment, BEFORE the first apply — the certificate is automatic, the DNS record is not, and an
# HTTP-01 challenge against a hostname that does not resolve fails.
resource "kubernetes_ingress_v1" "umami" {
  metadata {
    name      = "umami"
    namespace = kubernetes_namespace_v1.app.metadata[0].name
    annotations = merge(
      { "nginx.ingress.kubernetes.io/ssl-redirect" = tostring(var.enable_tls) },
      var.enable_tls ? { "cert-manager.io/cluster-issuer" = var.cluster_issuer } : {},
    )
  }
  spec {
    ingress_class_name = var.ingress_class_name
    rule {
      host = var.analytics_hostname
      http {
        path {
          path      = "/"
          path_type = "Prefix"
          backend {
            service {
              name = kubernetes_service_v1.umami.metadata[0].name
              port {
                number = 3000
              }
            }
          }
        }
      }
    }
    dynamic "tls" {
      for_each = var.enable_tls ? [1] : []
      content {
        hosts       = [var.analytics_hostname]
        secret_name = "${replace(var.analytics_hostname, ".", "-")}-tls"
      }
    }
  }
}
