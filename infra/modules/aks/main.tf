# AKS Standard cluster: separate system + user node pools, Azure CNI Overlay,
# system-assigned managed identity, optional API-server IP allowlist.

resource "azurerm_kubernetes_cluster" "this" {
  name                = "aks-${var.name_prefix}"
  location            = var.location
  resource_group_name = var.resource_group_name
  dns_prefix          = var.name_prefix
  kubernetes_version  = var.kubernetes_version

  # System pool: tainted (CriticalAddonsOnly) so app workloads land on the user pool.
  default_node_pool {
    name                         = "system"
    vm_size                      = var.node_vm_size
    node_count                   = var.system_node_count
    vnet_subnet_id               = var.subnet_id
    only_critical_addons_enabled = true
    orchestrator_version         = var.kubernetes_version
    temporary_name_for_rotation  = "systmp"
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin      = "azure"
    network_plugin_mode = "overlay"
    load_balancer_sku   = "standard"
    pod_cidr            = "10.244.0.0/16"
    service_cidr        = "10.0.0.0/16"
    dns_service_ip      = "10.0.0.10"
  }

  dynamic "api_server_access_profile" {
    for_each = length(var.api_authorized_ip_ranges) > 0 ? [1] : []
    content {
      authorized_ip_ranges = var.api_authorized_ip_ranges
    }
  }

  tags = var.tags
}

# User pool: app workloads. Autoscales only when enabled (prod); fixed on dev.
resource "azurerm_kubernetes_cluster_node_pool" "user" {
  name                  = "user"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.this.id
  vm_size               = var.node_vm_size
  vnet_subnet_id        = var.subnet_id
  orchestrator_version  = var.kubernetes_version

  auto_scaling_enabled = var.enable_user_autoscale
  node_count           = var.enable_user_autoscale ? null : var.user_node_min
  min_count            = var.enable_user_autoscale ? var.user_node_min : null
  max_count            = var.enable_user_autoscale ? var.user_node_max : null

  tags = var.tags
}
