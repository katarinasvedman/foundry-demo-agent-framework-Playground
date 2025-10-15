@description('AI Foundry Hub name')
param aiFoundryHubName string

@description('AI Foundry Project name') 
param aiFoundryProjectName string

@description('Location for AI Foundry resources')
param location string = resourceGroup().location

@description('Key Vault name for storing AI Foundry connection details')
param keyVaultName string

@description('Tags for resources')
param tags object = {}

// AI Foundry Hub (AI Studio Hub)
resource aiFoundryHub 'Microsoft.MachineLearningServices/workspaces@2024-04-01' = {
  name: aiFoundryHubName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  kind: 'Hub'
  properties: {
    friendlyName: aiFoundryHubName
    description: 'AI Foundry Hub for Foundry Demo'
    
    // Enable public network access for development
    publicNetworkAccess: 'Enabled'
    
    // Disable high business impact features for basic setup
    hbiWorkspace: false
  }
}

// AI Foundry Project (connected to the hub)
resource aiFoundryProject 'Microsoft.MachineLearningServices/workspaces@2024-04-01' = {
  name: aiFoundryProjectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  kind: 'Project'
  properties: {
    friendlyName: aiFoundryProjectName
    description: 'AI Foundry Project for Foundry Demo Agents'
    
    // Link to the hub
    hubResourceId: aiFoundryHub.id
    
    // Enable public network access for development
    publicNetworkAccess: 'Enabled'
  }
}

// Store AI Foundry project endpoint in Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource aiFoundryEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AIFoundryProjectEndpoint'
  properties: {
    value: 'https://${aiFoundryProject.properties.workspaceId}.${location}.api.azureml.ms'
    contentType: 'text/plain'
  }
}

resource aiFoundryProjectIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AIFoundryProjectId'
  properties: {
    value: aiFoundryProject.properties.workspaceId
    contentType: 'text/plain'
  }
}

// Note: Logic Apps will need appropriate RBAC permissions to AI Foundry
// This can be configured post-deployment via Azure Portal or additional role assignments

// Outputs
output aiFoundryHubId string = aiFoundryHub.id
output aiFoundryHubName string = aiFoundryHub.name
output aiFoundryProjectId string = aiFoundryProject.id  
output aiFoundryProjectName string = aiFoundryProject.name
output aiFoundryProjectEndpoint string = 'https://${aiFoundryProject.properties.workspaceId}.${location}.api.azureml.ms'
output aiFoundryProjectWorkspaceId string = aiFoundryProject.properties.workspaceId
