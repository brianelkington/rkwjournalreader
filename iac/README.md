## Azure Infrastructure as Code (Terraform)

This folder contains Terraform scripts to provision Azure resources for the Journal Reader application.

### Resources Provisioned

- **Resource Group**: `rg-rkwjournalreader-computervision`
- **User Assigned Managed Identity**: `id-rkw`
- **Log Analytics Workspace**: `law-rkw`
- **Storage Account**: `sarkwreadercompvis`
  - **Blob Containers**: `rkw-text-out`, `rkw-images`
- **Azure Cognitive Services (Computer Vision)**: `ai-rkw-vision`
- **Azure Key Vault**: `kv-rkw-computervision`
  - Stores Cognitive Services API keys and endpoint as secrets

### Usage

1. **Set your Azure subscription ID** in a `terraform.tfvars` file:
   ```hcl
   subscription_id = "<your-subscription-id>"
   ```

2. **Initialize Terraform:**
   ```sh
   terraform init
   ```

3. **Review the plan:**
   ```sh
   terraform plan
   ```

4. **Apply the configuration:**
   ```sh
   terraform apply
   ```

### File Overview

- [`main.tf`](iac/main.tf): Resource group, managed identity, log analytics
- [`storage.tf`](iac/storage.tf): Storage account and blob containers
- [`cognitiveservice.tf`](iac/cognitiveservice.tf): Cognitive Services resource and Key Vault secrets
- [`keyvault.tf`](iac/keyvault.tf): Key Vault and access policies
- [`providers.tf`](iac/providers.tf): Azure provider configuration
- [`variables.tf`](iac/variables.tf): Input variables and tags
- [`versions.tf`](iac/versions.tf): Provider version constraints

### Notes

- Key Vault stores API keys and endpoint for secure access.
- Managed identity is provisioned for secure resource access.
- All resources are tagged for cost and environment tracking.

**See also:**  
-