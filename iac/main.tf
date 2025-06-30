resource "azurerm_resource_group" "rkw" {
  name     = "rg-rkwjournalreader"
  location = var.location

  tags     = merge(local.tags, { createdOn = formatdate("YYYY-MM-DD HH:mm:ss ZZZ", timestamp()) })

  lifecycle {
    ignore_changes = [
      tags,
    ]
  }
}

resource "azurerm_user_assigned_identity" "rkw" {
  name                = "id-rkw"
  location            = azurerm_resource_group.rkw.location
  resource_group_name = azurerm_resource_group.rkw.name

  tags = local.tags
}

resource "azurerm_log_analytics_workspace" "rkw" {
  name                = "law-rkw"
  location            = azurerm_resource_group.rkw.location
  resource_group_name = azurerm_resource_group.rkw.name

  tags = local.tags
}

# import {
#   id = "/subscriptions/${var.subscription_id}/resourceGroups/${azurerm_resource_group.rkw.name}/providers/Microsoft.OperationalInsights/workspaces/law-rkw"
#   to = azurerm_log_analytics_workspace.rkw
# }

resource "azurerm_monitor_diagnostic_setting" "rkw" {
  name                       = "diag-cog-to-law"
  target_resource_id         = azurerm_cognitive_account.rkw.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.rkw.id

  # enable all metrics categories
  enabled_metric {
    category = "AllMetrics"
  }

  # enable the built-in log categories (Audit, Operational, etc)
  # adjust categories as needed for your scenario
  enabled_log {
    category = "allLogs"
  }
  # enabled_log {
  #   category = "AuditEvent"
  # }
}
