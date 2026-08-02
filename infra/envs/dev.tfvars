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

# Media object storage (035) — locally redundant is fine for Dev; the data is reproducible.
media_storage_replication_type = "LRS"

# App / domain — HTTPS via Let's Encrypt STAGING first (swap to prod once verified).
aspnetcore_environment = "Development"
app_hostname           = "dev.juggerhub.com"
enable_www_redirect    = false
enable_tls             = true
letsencrypt_issuer     = "letsencrypt-prod"
acme_email             = "admin@juggerhub.com"

# Lock the API server to your CI + operator IPs (fill in real CIDRs), or leave [] open.
api_authorized_ip_ranges = []

# Analytics (feature 033). REQUIRES a DNS A record for analytics_hostname pointing at the static
# public IP BEFORE the first apply — cert-manager issues automatically, DNS does not.
#
# umami_website_id is a UUID we chose, not one generated in the dashboard: the post-deploy Job
# provisions the matching row, so the first apply measures immediately with no bootstrap step and
# no second apply. It is NOT a secret — it ships in page source — which is why it lives here
# rather than in the GitHub Environment. It MUST differ from Prod's, since that separation is what
# keeps development traffic out of the real figures (FR-018).
umami_replicas     = 1
analytics_hostname = "analytics-dev.juggerhub.com"
umami_website_id   = "b7e4d21c-0a53-4f18-9c62-3d5a81f0e447"

# --- Session recording (feature 038) ----------------------------------------
# Recording is on wherever analytics is on. How much it records, how it masks, and whether it runs
# at all are dashboard settings, not Terraform's business — Dev records every session today.
# Retention is here because Umami has no setting for it and the privacy policy publishes the number.
umami_replay_retention_days = 30
