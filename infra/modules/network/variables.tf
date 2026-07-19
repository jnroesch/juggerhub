variable "name_prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "vnet_cidr" {
  type = string
}

variable "subnet_cidr" {
  type = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
