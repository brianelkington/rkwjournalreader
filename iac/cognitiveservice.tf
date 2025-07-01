resource "azurerm_cognitive_account" "rkw" {
  name                = "ai-rkw-vision"
  location            = azurerm_resource_group.rkw.location
  resource_group_name = azurerm_resource_group.rkw.name
  kind                = "ComputerVision"

  sku_name              = "F0"
  custom_subdomain_name = "rkwjournalreader"

  tags = local.tags
}

resource "azurerm_key_vault_secret" "api_key_1" {
  name         = "api-key-1"
  value        = azurerm_cognitive_account.rkw.primary_access_key
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "api_key_2" {
  name         = "api-key-2"
  value        = azurerm_cognitive_account.rkw.secondary_access_key
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "endpoint" {
  name         = "api-endpoint"
  value        = azurerm_cognitive_account.rkw.endpoint
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}
