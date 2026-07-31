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

# Media object storage (035) — zone-redundant: member-uploaded pictures are not reproducible, and
# there is no backfill path if they are lost. Same resource shape as Dev, larger only in durability.
media_storage_replication_type = "ZRS"

# App / domain — apex + www redirect, real Let's Encrypt certificates.
aspnetcore_environment = "Production"
app_hostname           = "juggerhub.com"
enable_www_redirect    = true
enable_tls             = true
letsencrypt_issuer     = "letsencrypt-prod"
acme_email             = "admin@juggerhub.com"

api_authorized_ip_ranges = []

# Analytics (feature 033). REQUIRES a DNS A record for analytics_hostname pointing at the static
# public IP BEFORE the first apply — cert-manager issues automatically, DNS does not.
#
# Only sizing, hostname and website ID differ from Dev; the resource set is identical
# (constitution V). The website ID is DELIBERATELY different from Dev's — that difference is the
# whole mechanism keeping development traffic out of the figures decisions are made on (FR-018,
# SC-008), so these two values must never be made to match.
umami_replicas     = 2
analytics_hostname = "analytics.juggerhub.com"
umami_website_id   = "f3c9a5e8-27b1-4d06-8ea4-91b7c62df530"
