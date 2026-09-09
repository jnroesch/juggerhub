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

  tags = {
    project     = local.project
    environment = local.env
    managed-by  = "terraform"
    feature     = "015-hosting"
  }
}
