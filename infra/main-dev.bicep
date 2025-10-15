@description('Base name for all resources')
param baseName string = 'foundry-dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('AI Foundry project endpoint for development')
param aiFoundryProjectEndpoint string = 'http://localhost:3000'

@description('Model deployment name')
param modelDeploymentName string = 'gpt-4o'

// Variables for development environment
var uniqueSuffix = uniqueString(resourceGroup().id)
var resourcePrefix = '${baseName}-${uniqueSuffix}'

// Storage Account for development (cheaper tier)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: replace('${baseName}st${uniqueSuffix}', '-', '')
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

// App Service Plan for development (Free tier)
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${resourcePrefix}-plan'
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
}

// Function App for External Signals API (development configuration)
resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: '${resourcePrefix}-func'
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Development'
        }
      ]
    }
  }
}

// Web App for Foundry Agents (development)
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: '${resourcePrefix}-agents'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'PROJECT_ENDPOINT'
          value: aiFoundryProjectEndpoint
        }
        {
          name: 'MODEL_DEPLOYMENT_NAME'
          value: modelDeploymentName
        }
        {
          name: 'OpenApi__BaseUrl'
          value: 'https://${functionApp.properties.defaultHostName}/api'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Development'
        }
        {
          name: 'Azure__UseManagedIdentity'
          value: 'false'
        }
      ]
    }
  }
}

// Simple Logic App for development testing
resource simpleLogicApp 'Microsoft.Logic/workflows@2019-05-01' = {
  name: '${resourcePrefix}-simple-trigger'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      triggers: {
        manual: {
          type: 'Request'
          kind: 'Http'
        }
      }
      actions: {
        Response: {
          type: 'Response'
          inputs: {
            statusCode: 200
            body: 'Development Logic App triggered successfully'
          }
        }
      }
    }
  }
}

// Outputs for development
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output logicAppTriggerUrl string = simpleLogicApp.properties.accessEndpoint
output storageAccountName string = storageAccount.name
output resourceGroupName string = resourceGroup().name
