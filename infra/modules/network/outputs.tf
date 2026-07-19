output "resource_group_name" {
  value = azurerm_resource_group.this.name
}

output "resource_group_id" {
  value = azurerm_resource_group.this.id
}

output "location" {
  value = azurerm_resource_group.this.location
}

output "subnet_id" {
  value = azurerm_subnet.aks.id
}

output "public_ip_address" {
  value = azurerm_public_ip.ingress.ip_address
}

output "public_ip_resource_group" {
  value = azurerm_resource_group.this.name
}
