# Foundry Demo - Bicep Infrastructure Summary

## 📁 Files Created/Updated

### Main Infrastructure Templates
- **`main.bicep`** - Production-ready infrastructure with Container Apps, Key Vault, Logic Apps
- **`main-dev.bicep`** - Development environment with Web Apps and basic resources  
- **`main-complete.bicep`** - Full infrastructure template with comprehensive configuration
- **`agenttrigger.bicep`** - Enhanced Logic App deployment (updated from original)

### Modular Components (`modules/`)
- **`keyvault.bicep`** - Azure Key Vault for secure secrets management
- **`appinsights.bicep`** - Application Insights with Log Analytics workspace
- **`storage.bicep`** - Storage Account for Azure Functions
- **`functions.bicep`** - Azure Functions (External Signals API) with Linux consumption plan
- **`container-apps.bicep`** - Container Apps for scalable Foundry Agents hosting
- **`logicapp-trigger.bicep`** - Logic App for agent triggers with AI Foundry integration
- **`logicapp-email.bicep`** - Logic App for email automation with Office 365 connector
- **`keyvault-access.bicep`** - RBAC role assignments for Key Vault access
- **`ai-foundry.bicep`** - Optional AI Foundry Hub and Project creation

### Configuration Files  
- **`parameters.json`** - Production deployment parameters
- **`parameters-dev.json`** - Development deployment parameters
- **`parameters-complete.json`** - Complete deployment parameters
- **`deploy.ps1`** - PowerShell deployment script with validation and error handling
- **`README.md`** - Comprehensive infrastructure documentation

## 🏗️ Architecture Overview

### Production Environment (`main.bicep`)
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Azure Functions │    │  Container Apps │    │   Logic Apps    │
│ (External API)   │◄──►│ (Foundry Agents)│◄──►│  (Triggers)     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
          │                       │                       │
          ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Storage Account│    │   Key Vault     │    │App Insights     │
│   (Functions)   │    │  (Secrets)      │    │ (Monitoring)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Key Features
- **Scalability**: Container Apps with scale-to-zero capability
- **Security**: Managed identities, Key Vault, RBAC
- **Monitoring**: Application Insights with centralized logging
- **Cost Optimization**: Consumption-based pricing where possible
- **DevOps Ready**: Infrastructure as Code with parameterized templates

## 🚀 Deployment Options

### Quick Start - Development
```powershell
.\deploy.ps1 -Environment dev
```

### Production Deployment
```powershell  
.\deploy.ps1 -Environment prod -ResourceGroupName "rg-foundry-prod"
```

### What-If Analysis
```powershell
.\deploy.ps1 -Environment prod -WhatIf
```

### Manual Azure CLI
```bash
az deployment group create \
  --resource-group rg-foundry-demo \
  --template-file main.bicep \
  --parameters parameters.json
```

## ⚙️ Configuration Requirements

### Required Parameters
| Parameter | Description | Example |
|-----------|-------------|---------|
| `baseName` | Resource naming prefix | `foundry-agents` |
| `environment` | Environment identifier | `dev`, `test`, `prod` |
| `aiFoundryProjectEndpoint` | AI Foundry project API URL | `https://your-project.ai.azure.com/...` |
| `modelDeploymentName` | AI model deployment | `gpt-4o` |
| `emailRecipient` | Email for notifications | `admin@company.com` |

### Post-Deployment Setup
1. **Logic Apps Configuration**:
   - Configure AI Foundry connector actions
   - Authenticate Office 365 connection for email

2. **Application Deployment**:
   - Deploy Function App code (`src/ExternalSignals.Api`)
   - Deploy Container App image (`src/Foundry.Agents`)

3. **Key Vault Secrets** (Production):
   - Store agent IDs and sensitive configuration
   - Configure managed identity access

## 🔧 Development vs Production

| Component | Development | Production |
|-----------|-------------|------------|
| **Hosting** | Web Apps (Free/Basic) | Container Apps (Consumption) |
| **Storage** | Standard_LRS | Standard_LRS with security features |
| **Logic Apps** | Simple HTTP trigger | Full AI Foundry integration |
| **Monitoring** | Basic Application Insights | Full telemetry with sampling |
| **Security** | Minimal (dev-friendly) | Managed identities + Key Vault |

## 📊 Cost Estimation

### Development Environment
- App Service Plan (Free): $0/month
- Function App (Consumption): ~$5-20/month  
- Storage Account: ~$2-5/month
- Logic Apps: ~$1-10/month
- **Total: ~$8-35/month**

### Production Environment  
- Container Apps: ~$10-50/month (scale-to-zero)
- Function App (Consumption): ~$10-30/month
- Key Vault: ~$3-10/month
- Application Insights: ~$5-25/month
- Logic Apps: ~$5-20/month
- **Total: ~$33-135/month**

## 🛠️ Maintenance and Operations

### Monitoring
- Application Insights dashboards for performance metrics
- Log Analytics queries for troubleshooting
- Azure Monitor alerts for critical issues

### Updates
- Bicep templates are version-controlled
- Parameter files separate configuration from infrastructure
- Modular design allows component-level updates

### Backup and Recovery
- Key Vault automatically backed up
- Application Insights data retained per configuration
- Source code and infrastructure templates in Git

## 🔍 Troubleshooting

### Common Issues
1. **Logic App AI Foundry Connection**: Requires manual configuration in Azure portal
2. **Container Apps Image**: Ensure image is accessible and properly configured
3. **Key Vault Access**: Verify managed identity permissions
4. **Function App Deployment**: Check storage account connection strings

### Validation Steps
```powershell
# Check deployment status
az deployment group list --resource-group rg-foundry-demo

# Validate Function App
curl https://your-function-app.azurewebsites.net/api/health

# Test Logic App trigger
curl -X POST https://your-logic-app-trigger-url
```

## 🎯 Next Steps

1. **Deploy Infrastructure**: Choose development or production template
2. **Configure Connections**: Set up AI Foundry and Office 365 connectors
3. **Deploy Applications**: Use CI/CD pipelines for code deployment
4. **Set Up Monitoring**: Configure alerts and dashboards
5. **Production Hardening**: Review security settings and access controls

## 📚 Additional Resources

- [Azure Bicep Documentation](https://docs.microsoft.com/azure/azure-resource-manager/bicep/)
- [Azure Container Apps](https://docs.microsoft.com/azure/container-apps/)
- [AI Foundry Documentation](https://docs.microsoft.com/azure/ai-foundry/)
- [Azure Logic Apps](https://docs.microsoft.com/azure/logic-apps/)

---
**🎉 Your Foundry Demo infrastructure is now ready for deployment!**