data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "rkw" {
  name                = "kv-rkw"
  location            = azurerm_resource_group.rkw.location
  resource_group_name = azurerm_resource_group.rkw.name

  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 7
  purge_protection_enabled    = false

  sku_name = "standard"

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

    secret_permissions = [
      "Get",
      "Set",
      "List",
      "Delete",
      "Purge",
    ]
  }

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = azurerm_user_assigned_identity.rkw.principal_id

    secret_permissions = [
      "Get",
      "Set",
      "List",
      "Delete",
      "Purge",
    ]
  }

  tags = local.tags
}

resource "azurerm_key_vault_secret" "api_key_1" {
  name         = "API-KEY-1"
  value        = azurerm_cognitive_account.rkw.primary_access_key
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "api_key_2" {
  name         = "API-KEY-2"
  value        = azurerm_cognitive_account.rkw.secondary_access_key
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "endpoint" {
  name         = "ENDPOINT"
  value        = azurerm_cognitive_account.rkw.endpoint
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}
