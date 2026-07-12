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
  connection_string = "Host=postgres;Port=5432;Database=${var.postgres_db};Username=${var.postgres_user};Password=${var.postgres_password}"

  tags = {
    project     = local.project
    environment = local.env
    managed-by  = "terraform"
    feature     = "015-hosting"
  }
}
