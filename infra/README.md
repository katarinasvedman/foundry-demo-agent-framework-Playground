# Foundry Demo - Infrastructure as Code

This directory contains Bicep templates for deploying the Foundry Demo infrastructure to Azure.

## Architecture Overview

The solution deploys the following Azure resources:

### Production Environment (`main.bicep`)
- **Azure Container Apps**: Scalable hosting for the Foundry Agents application
- **Azure Functions**: External Signals API endpoints
- **Azure Key Vault**: Secure storage for secrets and configuration
- **Application Insights**: Monitoring and telemetry
- **Logic Apps**: 
  - Agent trigger workflows
  - Email sending automation
- **Storage Account**: Required for Azure Functions
- **Managed Identities**: Secure service-to-service authentication

### Development Environment (`main-dev.bicep`)
- **Azure Web Apps**: Simple hosting for development testing
- **Azure Functions**: External Signals API (Free tier)
- **Storage Account**: Basic storage for development
- **Logic Apps**: Simplified trigger for testing

## Deployment

### Prerequisites
- Azure CLI installed and logged in
- Appropriate Azure subscription permissions
- PowerShell (for using the deployment script)

### Quick Start

1. **Clone and navigate to infra directory**:
   ```bash
   cd foundry-demo/infra
   ```

2. **Update parameters**:
   - Edit `parameters.json` for production deployment
   - Edit `parameters-dev.json` for development deployment
   - Set your AI Foundry project endpoint
   - Configure email recipient

3. **Deploy using PowerShell script**:
   ```powershell
   # Development deployment
   .\deploy.ps1 -Environment dev
   
   # Production deployment  
   .\deploy.ps1 -Environment prod
   
   # What-if analysis
   .\deploy.ps1 -Environment prod -WhatIf
   ```

### Manual Deployment

Alternatively, deploy manually using Azure CLI:

```bash
# Create resource group
az group create --name rg-foundry-demo --location swedencentral

# Deploy infrastructure
az deployment group create \
  --resource-group rg-foundry-demo \
  --template-file main.bicep \
  --parameters parameters.json
```

## Configuration

### Required Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `baseName` | Base name for all resources | `foundry-agents` |
| `environment` | Environment identifier | `dev`, `test`, `prod` |
| `location` | Azure region | `swedencentral` |
| `aiFoundryProjectEndpoint` | AI Foundry project URL | `https://your-project.ai.azure.com/api/projects/your-project` |
| `modelDeploymentName` | Model deployment name | `gpt-4o` |
| `emailRecipient` | Email for notifications | `admin@company.com` |

### Post-Deployment Configuration

After deployment, you'll need to configure:

1. **AI Foundry Connector in Logic Apps**:
   - Open the Agent Trigger Logic App in Azure portal
   - Configure the AI Foundry connector actions
   - Replace the placeholder action with actual AI Foundry operations

2. **Office 365 Connection**:
   - Authenticate the Office 365 connection for email sending
   - Test the email Logic App workflow

3. **Key Vault Secrets** (Production):
   - Store sensitive configuration values in Key Vault
   - Configure agent IDs and API keys

4. **Application Code Deployment**:
   - Deploy Function App code from `src/ExternalSignals.Api`
   - Deploy Container App image or Web App from `src/Foundry.Agents`

## Resource Naming Convention

Resources follow the naming pattern: `{baseName}-{environment}-{uniqueSuffix}-{resourceType}`

Examples:
- `foundry-agents-prod-abc123-func` (Function App)
- `foundry-agents-prod-abc123-agents` (Container App)
- `foundry-agents-kv-abc123` (Key Vault)

## Monitoring and Observability

The infrastructure includes:
- **Application Insights**: Centralized logging and telemetry
- **Managed Identities**: Secure authentication without stored credentials
- **Azure Monitor**: Resource health and performance monitoring

## Security

- **Managed Identities**: Used for service-to-service authentication
- **Key Vault**: Secure storage of secrets and certificates
- **RBAC**: Least-privilege access to resources
- **Network Security**: Configured for secure communication

## Cost Optimization

### Development Environment
- Uses Free/Basic tiers where possible
- Single App Service Plan for multiple apps
- Standard storage with LRS replication

### Production Environment
- Container Apps with scale-to-zero capability
- Consumption-based Logic Apps
- Application Insights with sampling

## Troubleshooting

### Common Issues

1. **Deployment Failures**:
   - Check Azure CLI authentication: `az account show`
   - Verify subscription permissions
   - Review error messages in deployment logs

2. **Logic App Configuration**:
   - AI Foundry connector requires manual setup
   - Office 365 connection needs authentication

3. **Container Apps**:
   - Ensure container image is accessible
   - Check environment variables configuration

### Logs and Monitoring

- View deployment logs: Azure Portal > Resource Group > Deployments
- Application logs: Application Insights > Logs
- Resource health: Azure Monitor > Resource Health

## Development Workflow

1. **Local Development**:
   - Use `main-dev.bicep` for development resources
   - Point to local endpoints when possible

2. **Testing**:
   - Deploy to development environment
   - Test Logic App triggers manually
   - Verify Function App endpoints

3. **Production**:
   - Use `main.bicep` for production deployment
   - Configure proper scaling and monitoring
   - Set up backup and disaster recovery

## Contributing

When modifying the infrastructure:
1. Update Bicep templates in the `modules/` directory
2. Test changes in development environment first
3. Update documentation for any new parameters or resources
4. Follow Azure Well-Architected Framework principles

## Support

For issues with the infrastructure deployment:
1. Check the deployment logs in Azure Portal
2. Review the troubleshooting section above
3. Ensure all prerequisites are met
4. Verify parameter values are correct