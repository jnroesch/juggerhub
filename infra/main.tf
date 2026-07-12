# Root module: guards the workspace, then composes network -> aks -> platform -> app.
# Every environment applies THIS file; differences come only from envs/<env>.tfvars.

# --- Workspace guard (FR-014) ----------------------------------------------
# Applying on the 'default' workspace (or any unknown env) fails before any
# resource is planned.
resource "terraform_data" "workspace_guard" {
  lifecycle {
    precondition {
      condition     = contains(["dev", "prod", "staging"], terraform.workspace)
      error_message = "Select an environment workspace first: terraform workspace select dev|prod|staging (not '${terraform.workspace}')."
    }
  }
}

module "network" {
  source = "./modules/network"

  name_prefix = local.name_prefix
  location    = var.location
  vnet_cidr   = var.vnet_cidr
  subnet_cidr = var.subnet_cidr
  tags        = local.tags

  depends_on = [terraform_data.workspace_guard]
}

module "aks" {
  source = "./modules/aks"

  name_prefix              = local.name_prefix
  location                 = var.location
  resource_group_name      = module.network.resource_group_name
  subnet_id                = module.network.subnet_id
  kubernetes_version       = var.kubernetes_version
  node_vm_size             = var.node_vm_size
  system_node_count        = var.system_node_count
  user_node_min            = var.user_node_min
  user_node_max            = var.user_node_max
  enable_user_autoscale    = var.enable_user_autoscale
  api_authorized_ip_ranges = var.api_authorized_ip_ranges
  tags                     = local.tags
}

# Let the AKS control-plane identity manage the static public IP (which lives in the
# network RG, not the AKS-managed node RG) so ingress-nginx's LB can bind it.
# This role assignment is why the CI service principal needs User Access Administrator.
resource "azurerm_role_assignment" "aks_ingress_ip" {
  scope                = module.network.resource_group_id
  role_definition_name = "Network Contributor"
  principal_id         = module.aks.cluster_identity_principal_id
}

module "platform" {
  source = "./modules/platform"

  ingress_nginx_chart_version = var.ingress_nginx_chart_version
  cert_manager_chart_version  = var.cert_manager_chart_version
  public_ip_address           = module.network.public_ip_address
  public_ip_resource_group    = module.network.public_ip_resource_group
  acme_email                  = var.acme_email

  depends_on = [module.aks, azurerm_role_assignment.aks_ingress_ip]
}

module "app" {
  source = "./modules/app"

  namespace          = local.namespace
  ingress_class_name = module.platform.ingress_class_name

  # routing / TLS
  app_hostname        = var.app_hostname
  enable_www_redirect = var.enable_www_redirect
  enable_tls          = var.enable_tls
  cluster_issuer      = var.letsencrypt_issuer

  # images
  image_repo_backend  = var.image_repo_backend
  image_repo_frontend = var.image_repo_frontend
  image_tag           = var.image_tag

  # sizing
  backend_replicas         = var.backend_replicas
  frontend_replicas        = var.frontend_replicas
  enable_backend_hpa       = var.enable_backend_hpa
  backend_hpa_max_replicas = var.backend_hpa_max_replicas
  backend_hpa_cpu_target   = var.backend_hpa_cpu_target

  # postgres
  postgres_storage_gb    = var.postgres_storage_gb
  postgres_storage_class = var.postgres_storage_class
  postgres_user          = var.postgres_user
  postgres_db            = var.postgres_db
  postgres_password      = var.postgres_password

  # app config
  aspnetcore_environment  = var.aspnetcore_environment
  connection_string       = local.connection_string
  jwt_issuer              = var.jwt_issuer
  jwt_audience            = var.jwt_audience
  jwt_signing_key         = var.jwt_signing_key
  resend_api_key          = var.resend_api_key
  email_from_address      = var.email_from_address
  email_frontend_base_url = local.email_frontend_base_url
  admin_emails            = var.admin_emails

  # registry pull
  ghcr_username   = var.ghcr_username
  ghcr_pull_token = var.ghcr_pull_token

  tags = local.tags

  depends_on = [module.platform]
}
