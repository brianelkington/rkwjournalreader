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
      "Recover",
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
      "Recover",
    ]
  }

  tags = local.tags
}

# import {
#   id = "/subscriptions/ab98a90e-7153-476e-b323-ae4a15843e5f/resourceGroups/rg-rkwjournalreader/providers/Microsoft.KeyVault/vaults/kv-rkw"
#   to = azurerm_key_vault.rkw
# }
