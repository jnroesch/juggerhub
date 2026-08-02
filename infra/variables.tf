# All per-environment knobs. Values are supplied by envs/<env>.tfvars; secrets by
# GitHub Environments as TF_VAR_* at apply time. See specs/015-hosting/data-model.md.

# --- Azure / subscription ---------------------------------------------------
variable "subscription_id" {
  type        = string
  description = "Target Azure subscription id (or set ARM_SUBSCRIPTION_ID)."
  default     = null
}

variable "location" {
  type        = string
  description = "Azure region for the environment."
  default     = "westeurope"
}

# --- Networking -------------------------------------------------------------
variable "vnet_cidr" {
  type    = string
  default = "10.60.0.0/22"
}

variable "subnet_cidr" {
  type    = string
  default = "10.60.0.0/24"
}

variable "api_authorized_ip_ranges" {
  type        = list(string)
  description = "CIDRs allowed to reach the AKS API server (CI egress + operators). Empty = open."
  default     = []
}

# --- Cluster sizing (per env) ----------------------------------------------
variable "kubernetes_version" {
  type        = string
  description = "AKS Kubernetes version; null = AKS default."
  default     = null
}

variable "node_vm_size" {
  type    = string
  default = "Standard_D2s_v3"
}

variable "system_node_count" {
  type    = number
  default = 1
}

variable "user_node_min" {
  type    = number
  default = 1
}

variable "user_node_max" {
  type    = number
  default = 1

  validation {
    condition     = var.user_node_max >= var.user_node_min
    error_message = "user_node_max must be >= user_node_min."
  }
  validation {
    # autoscaling needs a real range; a fixed pool must have min == max.
    condition     = var.enable_user_autoscale ? var.user_node_max > var.user_node_min : true
    error_message = "When enable_user_autoscale is true, user_node_max must be > user_node_min."
  }
}

variable "enable_user_autoscale" {
  type    = bool
  default = false
}

# --- Workload sizing (per env) ---------------------------------------------
variable "backend_replicas" {
  type    = number
  default = 1

  validation {
    condition     = var.backend_replicas >= 1
    error_message = "backend_replicas must be >= 1."
  }
}

variable "frontend_replicas" {
  type    = number
  default = 1
}

variable "enable_backend_hpa" {
  type    = bool
  default = false
}

variable "backend_hpa_max_replicas" {
  type    = number
  default = 5
}

variable "backend_hpa_cpu_target" {
  type    = number
  default = 70
}

# --- Postgres (in-cluster) --------------------------------------------------
variable "postgres_storage_gb" {
  type    = number
  default = 8
}

variable "postgres_storage_class" {
  type    = string
  default = "managed-csi"
}

variable "postgres_user" {
  type    = string
  default = "juggerhub"
}

variable "postgres_db" {
  type    = string
  default = "juggerhub"
}

# --- Images (GHCR) ----------------------------------------------------------
# NOTE: build.yml publishes to ghcr.io/<owner>/<repo>/backend and /frontend.
# The deploy workflow passes these explicitly as -var; defaults are for manual applies.
variable "image_repo_backend" {
  type    = string
  default = "ghcr.io/jnroesch/juggerhub/backend"
}

variable "image_repo_frontend" {
  type    = string
  default = "ghcr.io/jnroesch/juggerhub/frontend"
}

variable "image_tag" {
  type        = string
  description = "Image tag to deploy (commit SHA), supplied at deploy time."
}

# --- Ingress / domain / TLS -------------------------------------------------
variable "app_hostname" {
  type        = string
  description = "Environment hostname, e.g. juggerhub.com (prod) or dev.juggerhub.com."
}

variable "enable_www_redirect" {
  type        = bool
  description = "Redirect www.<host> to the apex (prod)."
  default     = false
}

variable "enable_tls" {
  type    = bool
  default = true
}

variable "letsencrypt_issuer" {
  type        = string
  description = "cert-manager ClusterIssuer to use: letsencrypt-staging | letsencrypt-prod."
  default     = "letsencrypt-staging"

  validation {
    condition     = contains(["letsencrypt-staging", "letsencrypt-prod"], var.letsencrypt_issuer)
    error_message = "letsencrypt_issuer must be letsencrypt-staging or letsencrypt-prod."
  }
}

variable "acme_email" {
  type        = string
  description = "Contact email for Let's Encrypt registration."
}

# --- App config -------------------------------------------------------------
variable "aspnetcore_environment" {
  type    = string
  default = "Production"
}

variable "jwt_issuer" {
  type    = string
  default = "juggerhub"
}

variable "jwt_audience" {
  type    = string
  default = "juggerhub"
}

variable "email_from_address" {
  description = "From header on outgoing mail; may carry a display name (\"Name <addr>\"). Must be a Resend-verified sender."
  type        = string
  default     = "JuggerHub <hello@juggerhub.com>"
}

variable "email_frontend_base_url" {
  type        = string
  description = "SPA base URL used in emails; empty = https://<app_hostname>."
  default     = ""
}

# --- Chart versions ---------------------------------------------------------
variable "ingress_nginx_chart_version" {
  type    = string
  default = "4.11.3"
}

variable "cert_manager_chart_version" {
  type    = string
  default = "v1.16.2"
}

# --- Secrets (from GitHub Environments; never in tfvars) --------------------
variable "postgres_password" {
  type      = string
  sensitive = true
  validation {
    condition     = length(var.postgres_password) > 0
    error_message = "postgres_password must be set (GitHub Environment secret)."
  }
}

variable "jwt_signing_key" {
  type      = string
  sensitive = true
  validation {
    condition     = length(var.jwt_signing_key) >= 32
    error_message = "jwt_signing_key must be >= 32 chars."
  }
}

variable "resend_api_key" {
  type      = string
  sensitive = true
  validation {
    condition     = length(var.resend_api_key) > 0
    error_message = "resend_api_key must be set (GitHub Environment secret)."
  }
}

variable "admin_emails" {
  type        = string
  description = "Comma-separated platform-admin allowlist."
  sensitive   = true
  default     = ""
}

# --- media object storage (feature 035 / #97) -------------------------------
variable "media_storage_replication_type" {
  type        = string
  description = "Blob replication tier. The only per-environment difference for media storage — sizing, never shape (Principle V). LRS for Dev, ZRS or better for Prod."
  default     = "LRS"
}

variable "media_storage_container_name" {
  type        = string
  description = "Container holding every media object. Identical everywhere — environments are isolated by having separate storage ACCOUNTS, not different container names."
  default     = "media"
}

# --- Analytics (feature 033 — self-hosted Umami) ----------------------------
variable "umami_image" {
  type    = string
  default = "docker.umami.is/umami-software/umami"
}

variable "umami_image_tag" {
  type        = string
  description = "Umami v3 dropped the `postgresql-` prefix and the `v` that every 1.x/2.x tag carried, so this is a bare `3.2.0`. The plausible-looking `postgresql-v3.2.0` does not exist and fails at pull with a bare `not found`."
  default     = "3.2.0"
}

variable "umami_replicas" {
  type    = number
  default = 1
}

variable "analytics_hostname" {
  type        = string
  description = "Dashboard hostname, e.g. analytics-dev.juggerhub.com. Requires a MANUALLY created DNS A record pointing at the static public IP BEFORE the first apply — the certificate is automatic, the DNS record is not."
}

variable "umami_website_id" {
  type        = string
  description = "UUID of the tracked website. Chosen by us and provisioned by the post-deploy Job rather than generated in the dashboard, which is what makes the first apply measure immediately. NOT a secret — it ships in page source, so it belongs in envs/*.tfvars. Empty ships no tracker at all."
  default     = ""

  validation {
    # A malformed ID is the worst failure available here: everything deploys, the tracker loads,
    # every beacon is rejected, and nothing anywhere reports an error.
    condition     = var.umami_website_id == "" || can(regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", var.umami_website_id))
    error_message = "umami_website_id must be a UUID, or empty to disable the tracker."
  }
}

# --- Session recording (feature 038 — extends 033 with the rrweb recorder) ---
# No recorder-behaviour variables by design: recording is on wherever analytics is on, and the
# sample rate, mask level and kill switch are all dashboard controls. Retention is the exception —
# Umami has no setting for it, so it is enforced here.
variable "umami_replay_retention_days" {
  type        = number
  default     = 30
  description = "Days before recordings are deleted by the retention CronJob. PUBLISHED IN THE PRIVACY POLICY — changing it without changing the policy text makes a legal document untrue (038 FR-012a)."

  validation {
    condition     = var.umami_replay_retention_days > 0 && var.umami_replay_retention_days <= 365
    error_message = "umami_replay_retention_days must be between 1 and 365."
  }
}

variable "umami_db_password" {
  type      = string
  sensitive = true
  validation {
    condition     = length(var.umami_db_password) > 0
    error_message = "umami_db_password must be set (GitHub Environment secret)."
  }
}

variable "umami_app_secret" {
  type        = string
  sensitive   = true
  description = "Signs dashboard session tokens. A shared or default value makes sessions forgeable."
  validation {
    condition     = length(var.umami_app_secret) >= 32
    error_message = "umami_app_secret must be >= 32 chars."
  }
}

variable "umami_admin_password_hash" {
  type        = string
  sensitive   = true
  description = "bcrypt hash of the dashboard password, written over Umami's seeded admin account by the post-deploy Job. Umami exposes no environment variable for this. Generate it once and store it in the GitHub Environment; the PLAINTEXT must never enter the repository, tfvars, or Terraform state."
  validation {
    # Catches the two mistakes that both end the same way - locked out of a dashboard that is
    # already publicly reachable: pasting the plaintext instead of the hash, and a truncated copy.
    # bcrypt is always exactly 60 characters.
    condition     = can(regex("^\\$2[aby]\\$[0-9]{2}\\$.{53}$", var.umami_admin_password_hash))
    error_message = "umami_admin_password_hash must be a 60-character bcrypt hash ($2a$/$2b$/$2y$), not a plaintext password."
  }
}

variable "ghcr_username" {
  type    = string
  default = "jnroesch"
}

variable "ghcr_pull_token" {
  type        = string
  sensitive   = true
  description = "GHCR pull token (read:packages). Leave empty for PUBLIC packages — no imagePullSecret is created."
  default     = ""
}
