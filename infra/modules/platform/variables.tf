variable "ingress_nginx_chart_version" {
  type = string
}

variable "cert_manager_chart_version" {
  type = string
}

variable "public_ip_address" {
  type = string
}

variable "public_ip_resource_group" {
  type = string
}

variable "acme_email" {
  type = string
}

variable "ingress_class_name" {
  type    = string
  default = "nginx"
}

variable "cloudflare_ipv4_ranges" {
  type        = list(string)
  description = "Cloudflare's IPv4 ranges when the environment's hostnames are proxied through Cloudflare, else empty. Non-empty: the ingress takes the client address from CF-Connecting-IP for these peers only, and its LoadBalancer accepts connections from these ranges only (#244)."
  default     = []
}
