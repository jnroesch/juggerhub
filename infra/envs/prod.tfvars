# Prod environment values. Same architecture as Dev — only these values differ.
# Secrets come from the GitHub 'Prod' Environment (TF_VAR_*), never here.
# Apply with:  terraform workspace select prod && terraform apply -var-file=envs/prod.tfvars -var image_tag=<sha>

location = "westeurope"

# Cluster — larger nodes, autoscaling user pool.
node_vm_size          = "Standard_D2s_v3"
system_node_count     = 1
user_node_min         = 2
user_node_max         = 4
enable_user_autoscale = true

# Workloads — HA-oriented: multiple backend replicas + HPA.
backend_replicas         = 2
frontend_replicas        = 2
enable_backend_hpa       = true
backend_hpa_max_replicas = 6
backend_hpa_cpu_target   = 70

# Postgres — larger premium disk.
postgres_storage_gb    = 32
postgres_storage_class = "managed-csi-premium"

# App / domain — apex + www redirect, real Let's Encrypt certificates.
aspnetcore_environment = "Production"
app_hostname           = "juggerhub.com"
enable_www_redirect    = true
enable_tls             = true
letsencrypt_issuer     = "letsencrypt-prod"
acme_email             = "admin@juggerhub.com"

api_authorized_ip_ranges = []
