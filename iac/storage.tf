resource "azurerm_storage_account" "rkw" {
  name                     = "sarkwreader1"
  location                 = azurerm_resource_group.rkw.location
  resource_group_name      = azurerm_resource_group.rkw.name
  account_tier             = "Standard"
  account_replication_type = "LRS"

  tags = local.tags

  lifecycle {
    prevent_destroy = true
    ignore_changes = [
      tags,
    ]
  }
}

resource "azurerm_storage_container" "rkw" {
  name                  = "rkw-text-out"
  storage_account_id    = azurerm_storage_account.rkw.id
  container_access_type = "blob"
}
