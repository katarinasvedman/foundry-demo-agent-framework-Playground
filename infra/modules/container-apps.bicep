@description('Container App name')
param containerAppName string

@description('Location for Container Apps')
param location string = resourceGroup().location

@description('AI Foundry project endpoint')
param aiFoundryProjectEndpoint string

@description('Model deployment name')
param modelDeploymentName string

@description('Key Vault name')
param keyVaultName string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('External Signals API URL')
param externalSignalsApiUrl string

@description('Container image')
param containerImage string = 'mcr.microsoft.com/dotnet/runtime:8.0'

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${containerAppName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: ''
        sharedKey: ''
      }
    }
  }
}

// Container App for Foundry Agents
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      secrets: []
    }
    template: {
      containers: [
        {
          name: 'foundry-agents'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'PROJECT_ENDPOINT'
              value: aiFoundryProjectEndpoint
            }
            {
              name: 'MODEL_DEPLOYMENT_NAME'
              value: modelDeploymentName
            }
            {
              name: 'Azure__UseManagedIdentity'
              value: 'true'
            }
            {
              name: 'Azure__KeyVaultName'
              value: keyVaultName
            }
            {
              name: 'ApplicationInsights__ConnectionString'
              value: appInsightsConnectionString
            }
            {
              name: 'OpenApi__BaseUrl'
              value: '${externalSignalsApiUrl}/api'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output containerAppName string = containerApp.name
output containerAppId string = containerApp.id
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output managedIdentityPrincipalId string = containerApp.identity.principalId
