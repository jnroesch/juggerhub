variable "name_prefix" {
  description = "Environment-scoped name prefix, e.g. juggerhub-dev. Sanitised into the globally unique storage account name."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group that owns the storage account (the environment's RG from the network module)."
  type        = string
}

variable "replication_type" {
  description = "Account replication. The ONE knob that differs per environment (LRS for Dev, ZRS/GRS for Prod) — sizing, never shape."
  type        = string
  default     = "LRS"

  validation {
    condition     = contains(["LRS", "ZRS", "GRS", "RAGRS", "GZRS", "RAGZRS"], var.replication_type)
    error_message = "replication_type must be one of LRS, ZRS, GRS, RAGRS, GZRS, RAGZRS."
  }
}

variable "container_name" {
  description = "Container holding every media object. Identical in every environment — isolation comes from separate accounts, not container names."
  type        = string
  default     = "media"
}

variable "tags" {
  description = "Tags applied to the storage account."
  type        = map(string)
  default     = {}
}
