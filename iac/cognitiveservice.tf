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

# import {
#   id = "/subscriptions/${var.subscription_id}/resourceGroups/${azurerm_resource_group.rkw.name}/providers/Microsoft.CognitiveServices/accounts/ai-rkw-vision"
#   to = azurerm_cognitive_account.rkw
# }

# import {
#   id = "https://kv-rkw.vault.azure.net/secrets/api-key-1/db47b24e8f73419e8c4040a15f002219"
#   to = azurerm_key_vault_secret.api_key_1
# }

# import {
#   id = "https://kv-rkw.vault.azure.net/secrets/api-key-2/44b51997e1964e30a7e531ddae7cd165"
#   to = azurerm_key_vault_secret.api_key_2
# }

# import {
#   id = "https://kv-rkw.vault.azure.net/secrets/api-endpoint/441935426afd4d1d84804bb7eb08a301"
#   to = azurerm_key_vault_secret.endpoint
# }

resource "azurerm_cognitive_account" "rkw-training" {
  name                = "ai-rkw-vision-training"
  location            = azurerm_resource_group.rkw.location
  resource_group_name = azurerm_resource_group.rkw.name
  kind                = "CustomVision.Training"

  sku_name              = "F0"
  custom_subdomain_name = "rkwjournalreadertraining"

  tags = local.tags
}

resource "azurerm_key_vault_secret" "api_key_1_training" {
  name         = "api-key-1-training"
  value        = azurerm_cognitive_account.rkw-training.primary_access_key
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "api_key_2_training" {
  name         = "api-key-2-training"
  value        = azurerm_cognitive_account.rkw-training.secondary_access_key
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "endpoint_training" {
  name         = "api-endpoint-training"
  value        = azurerm_cognitive_account.rkw-training.endpoint
  key_vault_id = azurerm_key_vault.rkw.id
  tags         = local.tags
}

