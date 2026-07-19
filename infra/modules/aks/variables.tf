variable "name_prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "subnet_id" {
  type = string
}

variable "kubernetes_version" {
  type    = string
  default = null
}

variable "node_vm_size" {
  type = string
}

variable "system_node_count" {
  type = number
}

variable "user_node_min" {
  type = number
}

variable "user_node_max" {
  type = number
}

variable "enable_user_autoscale" {
  type = bool
}

variable "api_authorized_ip_ranges" {
  type    = list(string)
  default = []
}

variable "tags" {
  type    = map(string)
  default = {}
}
