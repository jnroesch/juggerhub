# Media object storage (feature 035 / #97): the blob account and container holding avatars and
# catalogue icons, which used to live as Postgres `bytea`.
#
# Defined ONCE and applied to every environment (constitution Principle V). Environments differ
# only in `account_replication_type` — never in which resources exist.

# Storage account names are globally unique across Azure and allow ONLY 3-24 lowercase
# alphanumeric characters. `local.name_prefix` is "juggerhub-dev", which is invalid twice over, so
# strip the separator and append a deterministic suffix for global uniqueness. `random_string` is
# held in state with keepers tied to the prefix, so it is stable across applies and only rerolls if
# the environment itself is renamed.
resource "random_string" "suffix" {
  length  = 6
  lower   = true
  upper   = false
  numeric = true
  special = false

  keepers = {
    name_prefix = var.name_prefix
  }
}

locals {
  # e.g. "juggerhubdev" + 6 chars = 18, comfortably inside the 24-character limit.
  account_name = "${replace(var.name_prefix, "-", "")}${random_string.suffix.result}"
}

resource "azurerm_storage_account" "media" {
  name                = local.account_name
  resource_group_name = var.resource_group_name
  location            = var.location

  account_tier             = "Standard"
  account_kind             = "StorageV2"
  account_replication_type = var.replication_type

  # The account-level kill switch for anonymous access, and the backstop behind the application's
  # own "never create a container with a public access level" rule. With this false, a container
  # mistakenly created as public STILL cannot serve anonymously — which is what keeps the
  # feature-026 visibility gate from being bypassable by a single misconfiguration.
  allow_nested_items_to_be_public = false

  # Media is reached only by the backend, which authorizes every request first; there is never a
  # browser hitting this account directly, so no CORS rules are configured by design.
  https_traffic_only_enabled = true
  min_tls_version            = "TLS1_2"

  # Public network access stays enabled: the AKS cluster reaches storage over the public endpoint
  # (no private endpoint / VNet integration in this architecture yet). Access is controlled by the
  # account key, which is held only in the environment's Kubernetes Secret. A private endpoint is
  # the natural hardening step once workload identity replaces the key.
  public_network_access_enabled = true

  tags = var.tags
}

resource "azurerm_storage_container" "media" {
  name               = var.container_name
  storage_account_id = azurerm_storage_account.media.id

  # Private: the container serves nobody directly. Every byte reaches a caller through the API,
  # after the platform has decided they may see it.
  container_access_type = "private"
}
