variable "subscription_id" {
  type      = string
  sensitive = true
}

locals {
  tags = {
    cost-center = "rkw"
    environment = "dev"
    team        = "briane"
  }
}
