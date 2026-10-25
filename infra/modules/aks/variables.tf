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

# The SYSTEM pool's size. Separate from node_vm_size because AKS does not support B-series VMs
# for system node pools; see the root variables for the documentation and the known deviation.
variable "system_node_vm_size" {
  type = string
}

# Applied to BOTH pools. See the root variables for the cache-size ceiling that Ephemeral imposes.
variable "node_os_disk_type" {
  type = string
}

variable "node_os_disk_size_gb" {
  type = number
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
