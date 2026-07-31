output "account_name" {
  description = "Generated storage account name (useful for the post-deploy direct-store check)."
  value       = azurerm_storage_account.media.name
}

output "container_name" {
  description = "Media container name."
  value       = azurerm_storage_container.media.name
}

output "blob_endpoint" {
  description = "Primary blob endpoint. Needed to verify per environment that the store refuses anonymous reads (SC-010)."
  value       = azurerm_storage_account.media.primary_blob_endpoint
}

output "connection_string" {
  description = "Connection string handed to the backend via the app Kubernetes Secret. Never written to tfvars or logs."
  value       = azurerm_storage_account.media.primary_connection_string
  sensitive   = true
}
