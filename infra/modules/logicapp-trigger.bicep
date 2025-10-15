@description('Logic App name for agent triggers')
param logicAppName string

@description('Location for Logic App')
param location string = resourceGroup().location

@description('AI Foundry project endpoint')
param aiFoundryProjectEndpoint string

// Logic App workflow for agent triggers
resource agentTriggerWorkflow 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    definition: json(loadTextContent('../agenttrigger-workflow.json'))
    parameters: {
      projectEndpoint: {
        value: aiFoundryProjectEndpoint
      }
    }
  }
}

output workflowResourceId string = agentTriggerWorkflow.id
output managedIdentityPrincipalId string = agentTriggerWorkflow.identity.principalId
output triggerUrl string = agentTriggerWorkflow.properties.accessEndpoint
