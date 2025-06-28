resource "azurerm_cognitive_account" "rkw" {
  name                = "ai-rkw-vision"
  location            = azurerm_resource_group.rkw.location
  resource_group_name = azurerm_resource_group.rkw.name
  kind                = "ComputerVision"

  sku_name              = "F0"
  custom_subdomain_name = "rkwjournalreader"

  tags = local.tags
}

# import {
#   id = "/subscriptions/${var.subscription_id}/resourceGroups/${azurerm_resource_group.rkw.name}/providers/Microsoft.CognitiveServices/accounts/ai-rkw-vision"
#   to = azurerm_cognitive_account.rkw
# }

