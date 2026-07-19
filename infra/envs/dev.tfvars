# Dev environment values. Architecture is identical to Prod — only these values
# differ. Secrets come from the GitHub 'Dev' Environment (TF_VAR_*), never here.
# Apply with:  terraform workspace select dev && terraform apply -var-file=envs/dev.tfvars -var image_tag=<sha>

location = "westeurope"

# Cluster — small, single node, no autoscaling.
node_vm_size          = "Standard_D2s_v3"
system_node_count     = 1
user_node_min         = 1
user_node_max         = 1
enable_user_autoscale = false

# Workloads — one of each.
backend_replicas   = 1
frontend_replicas  = 1
enable_backend_hpa = false

# Postgres — small standard disk.
postgres_storage_gb    = 8
postgres_storage_class = "managed-csi"

# App / domain — HTTPS via Let's Encrypt STAGING first (swap to prod once verified).
aspnetcore_environment = "Development"
app_hostname           = "dev.juggerhub.com"
enable_www_redirect    = false
enable_tls             = true
letsencrypt_issuer     = "letsencrypt-staging"
acme_email             = "admin@juggerhub.com"

# Lock the API server to your CI + operator IPs (fill in real CIDRs), or leave [] open.
api_authorized_ip_ranges = []
