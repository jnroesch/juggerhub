variable "namespace" {
  type = string
}

variable "ingress_class_name" {
  type = string
}

# --- routing / TLS ----------------------------------------------------------
variable "app_hostname" {
  type = string
}

variable "enable_www_redirect" {
  type = bool
}

variable "enable_tls" {
  type = bool
}

variable "cluster_issuer" {
  type = string
}

# --- images -----------------------------------------------------------------
variable "image_repo_backend" {
  type = string
}

variable "image_repo_frontend" {
  type = string
}

variable "image_tag" {
  type = string
}

# --- sizing -----------------------------------------------------------------
variable "backend_replicas" {
  type = number
}

variable "frontend_replicas" {
  type = number
}

variable "enable_backend_hpa" {
  type = bool
}

variable "backend_hpa_max_replicas" {
  type = number
}

variable "backend_hpa_cpu_target" {
  type = number
}

# --- postgres ---------------------------------------------------------------
variable "postgres_storage_gb" {
  type = number
}

variable "postgres_storage_class" {
  type = string
}

variable "postgres_user" {
  type = string
}

variable "postgres_db" {
  type = string
}

variable "postgres_password" {
  type      = string
  sensitive = true
}

# --- Geocoder (feature 030) — same image + API everywhere; extract/size differ per env ---
variable "geocoder_image" {
  type        = string
  description = "Self-hosted Photon geocoder image (pin the tag; same across environments)."
}

variable "geocoder_region" {
  type        = string
  description = "OSM extract Photon imports on first start (e.g. \"de\", \"europe\"). The env sizing knob."
}

variable "geocoder_storage_gb" {
  type        = number
  description = "PersistentVolume size for the Photon index; scales with the extract."
}

variable "geocoder_storage_class" {
  type = string
}

# --- app config -------------------------------------------------------------
variable "aspnetcore_environment" {
  type = string
}

variable "connection_string" {
  type      = string
  sensitive = true
}

variable "jwt_issuer" {
  type = string
}

variable "jwt_audience" {
  type = string
}

variable "jwt_signing_key" {
  type      = string
  sensitive = true
}

variable "resend_api_key" {
  type      = string
  sensitive = true
}

variable "email_from_address" {
  description = "From header on outgoing mail; may carry a display name (\"Name <addr>\")."
  type        = string
}

variable "email_frontend_base_url" {
  type = string
}

variable "admin_emails" {
  type      = string
  sensitive = true
}

# --- registry pull ----------------------------------------------------------
variable "ghcr_username" {
  type = string
}

variable "ghcr_pull_token" {
  type      = string
  sensitive = true
}

variable "tags" {
  type    = map(string)
  default = {}
}
