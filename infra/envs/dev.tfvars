# Dev environment values. Architecture is identical to Prod — only these values
# differ. Secrets come from the GitHub 'Dev' Environment (TF_VAR_*), never here.
# Apply with:  terraform workspace select dev && terraform apply -var-file=envs/dev.tfvars -var image_tag=<sha>

location = "westeurope"

# Cluster — small, single node, no autoscaling. Both pools are Standard_D2s_v3.
#
# ⚠ DO NOT PUT A BURSTABLE SIZE HERE. It was tried and the AKS resource provider refused the
# apply with HTTP 400:
#
#   The VM size of Standard_B2ms is not allowed in your subscription in location 'westeurope'.
#
# This subscription's allow-list carries only the **v2** B-series (b2als_v2, b2as_v2, b2ls_v2,
# b2s_v2), never the original Bv1 sizes — and no v2 B-series has a local temp disk, so none of
# them supports an Ephemeral OS disk. Burstable and Ephemeral are therefore mutually exclusive
# here, and the arithmetic says Ephemeral wins: Standard_B2as_v2 at ~EUR 55/month plus a managed
# P10 at ~EUR 19 is ~EUR 74, against Standard_D2s_v3 at ~EUR 77 with a free Ephemeral disk. Three
# euros is not worth per-pool disk-type inputs and a slower reimage.
#
# Neither `terraform validate` nor `terraform plan` can catch a subscription allow-list — only the
# resource provider enforces it, at apply time, so CI goes green either way. `az vm list-skus`
# does know, but NOT in its capabilities: those answer for the REGION. The answer is in
# `restrictions[]`, and the `type` field is what matters —
#
#   az vm list-skus --location westeurope --size <size> --resource-type virtualMachines \
#     --query "[].restrictions[?type=='Location'].reasonCode" -o tsv
#
# A `type: "Location"` entry means the size is unusable here. `type: "Zone"` entries only narrow
# which zones, and reading those as a veto gives a false positive: Standard_D2s_v3 — what both
# pools actually run on — carries a Zone restriction for zones 2 and 3 on this subscription while
# being perfectly deployable. (Worth remembering if Prod is ever made zone-redundant.)
node_vm_size          = "Standard_D2s_v3"
system_node_vm_size   = "Standard_D2s_v3"
system_node_count     = 1
user_node_min         = 1
user_node_max         = 1
enable_user_autoscale = false

# Ephemeral OS disks cost nothing, where the Managed ones they replace billed a P10 per node.
# Already applied to the system pool; this is the user pool catching up.
#
# An Ephemeral disk cannot exceed the local storage it is placed on. Standard_D2s_v3 uses CACHE
# placement with 50 GiB available (`az vm list-skus` reports it as CachedDiskBytes), so 30 GiB
# fits with room left. Kept at 30 rather than raised to 50 because the cache is shared: an
# Ephemeral OS disk consumes what it sits on, and Prod's premium Postgres disk wants some of it.
#
# ⚠ The cache is NOT the temp disk, which is a different and smaller capacity (16 GiB here).
node_os_disk_type    = "Ephemeral"
node_os_disk_size_gb = 30

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

# dev.juggerhub.com and analytics-dev.juggerhub.com are proxied through Cloudflare (#244). The ingress
# takes the client IP from CF-Connecting-IP and ACCEPTS CONNECTIONS FROM CLOUDFLARE ONLY — set this
# to false BEFORE turning Cloudflare proxying off for either hostname, or it becomes unreachable.
cloudflare_proxied = true

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
