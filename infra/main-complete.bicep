@description('Base name for all resources')
param baseName string = 'foundry-agents'

@description('Environment (dev, test, prod)')
param environment string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Model deployment name')
param modelDeploymentName string = 'gpt-4o'

@description('Email recipient for notifications')
param emailRecipient string = ''

@description('AI Foundry project endpoint')
param aiFoundryProjectEndpoint string

// Variables
var uniqueSuffix = uniqueString(resourceGroup().id)
var resourcePrefix = '${baseName}-${environment}-${uniqueSuffix}'

// Key Vault for storing secrets
module keyVault 'modules/keyvault.bicep' = {
  name: 'deploy-keyvault'
  params: {
    keyVaultName: '${baseName}-kv-${uniqueSuffix}'
    location: location
    tenantId: tenant().tenantId
  }
}

// Application Insights for monitoring
module appInsights 'modules/appinsights.bicep' = {
  name: 'deploy-appinsights'
  params: {
    appInsightsName: '${resourcePrefix}-ai'
    location: location
  }
}

// Storage Account for Azure Functions
module storage 'modules/storage.bicep' = {
  name: 'deploy-storage'
  params: {
    storageAccountName: replace('${baseName}st${uniqueSuffix}', '-', '')
    location: location
  }
}

// Azure Functions for External Signals API
module functionsApp 'modules/functions.bicep' = {
  name: 'deploy-functions'
  params: {
    functionAppName: '${resourcePrefix}-func'
    location: location
    storageAccountName: storage.outputs.storageAccountName
    appInsightsInstrumentationKey: appInsights.outputs.instrumentationKey
    appInsightsConnectionString: appInsights.outputs.connectionString
  }
}

// Container Apps for Foundry Agents (scalable hosting)
module containerApps 'modules/container-apps.bicep' = {
  name: 'deploy-container-apps'
  params: {
    containerAppName: '${resourcePrefix}-agents'
    location: location
    aiFoundryProjectEndpoint: aiFoundryProjectEndpoint
    modelDeploymentName: modelDeploymentName
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: appInsights.outputs.connectionString
    externalSignalsApiUrl: functionsApp.outputs.functionAppUrl
  }
}

// Logic App for Agent Triggers
module agentTriggerLogicApp 'modules/logicapp-trigger.bicep' = {
  name: 'deploy-agent-trigger'
  params: {
    logicAppName: '${resourcePrefix}-trigger-la'
    location: location
    aiFoundryProjectEndpoint: aiFoundryProjectEndpoint
  }
}

// Logic App for Email Sending
module emailLogicApp 'modules/logicapp-email.bicep' = {
  name: 'deploy-email-logicapp'
  params: {
    logicAppName: '${resourcePrefix}-email-la'
    location: location
    emailRecipient: emailRecipient
  }
}

// Grant Container Apps access to Key Vault
module keyVaultAccess 'modules/keyvault-access.bicep' = {
  name: 'grant-keyvault-access'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    principalId: containerApps.outputs.managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output functionAppUrl string = functionsApp.outputs.functionAppUrl
output containerAppUrl string = containerApps.outputs.containerAppUrl
output keyVaultName string = keyVault.outputs.keyVaultName
output appInsightsName string = appInsights.outputs.appInsightsName
output agentTriggerLogicAppId string = agentTriggerLogicApp.outputs.workflowResourceId
output emailLogicAppId string = emailLogicApp.outputs.workflowResourceId

// AI Foundry configuration
output aiFoundryProjectEndpointConfigured string = aiFoundryProjectEndpoint

// Connection strings and configuration
output configurationSummary object = {
  functionApp: {
    name: functionsApp.outputs.functionAppName
    url: functionsApp.outputs.functionAppUrl
  }
  containerApp: {
    name: containerApps.outputs.containerAppName
    url: containerApps.outputs.containerAppUrl
  }
  keyVault: {
    name: keyVault.outputs.keyVaultName
    uri: keyVault.outputs.keyVaultUri
  }
  monitoring: {
    appInsightsName: appInsights.outputs.appInsightsName
    connectionString: appInsights.outputs.connectionString
  }
}
