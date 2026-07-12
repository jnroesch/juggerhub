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
