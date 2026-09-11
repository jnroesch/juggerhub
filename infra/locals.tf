locals {
  project = "juggerhub"

  # Environment is the selected Terraform workspace (dev|prod|staging).
  env         = terraform.workspace
  name_prefix = "${local.project}-${local.env}"
  namespace   = "juggerhub"

  # SPA base URL used in transactional emails; default derives from the hostname.
  email_frontend_base_url = coalesce(
    var.email_frontend_base_url != "" ? var.email_frontend_base_url : null,
    "https://${var.app_hostname}",
  )

  # .NET connection string assembled from parts (kept out of tfvars/state as
  # plaintext beyond the sensitive secret).
  #
  # Feature 047: TLS is required AND the server is verified. `SSL Mode=VerifyFull`, never
  # `Require`, and `Trust Server Certificate` must never appear here — it would silently undo the
  # check this exists for. The CA arrives as a file mounted by the backend Deployment, at the same
  # path docker-compose uses, so this string differs from the local one only in host and
  # credentials (Principle V).
  #
  # `Host=postgres` must be in the certificate's SAN list — VerifyFull matches the host as written.
  connection_string = "Host=postgres;Port=5432;Database=${var.postgres_db};Username=${var.postgres_user};Password=${var.postgres_password};SSL Mode=VerifyFull;Root Certificate=/etc/juggerhub/certs/ca.crt"

  # Cloudflare's published IPv4 ranges (https://www.cloudflare.com/ips-v4), fetched 2026-09-11 (#244).
  # Only IPv4: the origin has only an IPv4 address, so Cloudflare always connects to it over IPv4.
  # Hardcoded rather than fetched at plan time on purpose — a plan must not depend on a third-party
  # website being up. The list changes rarely, but when it does, a new edge IP is REJECTED at the
  # origin (loadBalancerSourceRanges) until this is updated: re-check it when Cloudflare announces a
  # change, and if visitors in one region suddenly cannot connect.
  cloudflare_ipv4_ranges = [
    "173.245.48.0/20",
    "103.21.244.0/22",
    "103.22.200.0/22",
    "103.31.4.0/22",
    "141.101.64.0/18",
    "108.162.192.0/18",
    "190.93.240.0/20",
    "188.114.96.0/20",
    "197.234.240.0/22",
    "198.41.128.0/17",
    "162.158.0.0/15",
    "104.16.0.0/13",
    "104.24.0.0/14",
    "172.64.0.0/13",
    "131.0.72.0/22",
  ]

  tags = {
    project     = local.project
    environment = local.env
    managed-by  = "terraform"
    feature     = "015-hosting"
  }
}
