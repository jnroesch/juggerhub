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
  type    = string
  default = "no-reply@juggerhub.com"
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
