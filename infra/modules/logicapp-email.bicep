@description('Logic App name for email sending')
param logicAppName string

@description('Location for Logic App')
param location string = resourceGroup().location

@description('Email recipient')
param emailRecipient string

// Office 365 Connection (requires manual configuration in portal)
resource office365Connection 'Microsoft.Web/connections@2016-06-01' = {
  name: '${logicAppName}-office365-connection'
  location: location
  properties: {
    displayName: 'Office 365 Connection'
    api: {
      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'office365')
    }
    // Note: This connection will need to be authenticated manually in the Azure portal
  }
}

// Logic App workflow for email sending
resource emailSendingWorkflow 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
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
          inputs: {
            schema: {
              type: 'object'
              properties: {
                email_to: {
                  type: 'string'
                }
                email_subject: {
                  type: 'string'
                }
                email_body: {
                  type: 'string'
                }
                attachments: {
                  type: 'array'
                  items: {
                    type: 'object'
                    properties: {
                      filename: {
                        type: 'string'
                      }
                      content_base64: {
                        type: 'string'
                      }
                      content_type: {
                        type: 'string'
                      }
                    }
                  }
                }
              }
              required: [
                'email_to'
                'email_subject'
                'email_body'
              ]
            }
          }
        }
      }
      actions: {
        Send_an_email_V2: {
          type: 'ApiConnection'
          inputs: {
            host: {
              connection: {
                name: '@parameters(\'$connections\')[\'office365\'][\'connectionId\']'
              }
            }
            method: 'post'
            path: '/v2/Mail'
            body: {
              To: '@coalesce(triggerBody()?[\'email_to\'], \'${emailRecipient}\')'
              Subject: '@triggerBody()?[\'email_subject\']'
              Body: '@triggerBody()?[\'email_body\']'
              Attachments: '@triggerBody()?[\'attachments\']'
            }
          }
        }
        Response: {
          type: 'Response'
          inputs: {
            statusCode: 200
            body: {
              status: 'sent'
              timestamp: '@utcnow()'
              recipient: '@coalesce(triggerBody()?[\'email_to\'], \'${emailRecipient}\')'
            }
          }
          runAfter: {
            Send_an_email_V2: [
              'Succeeded'
            ]
          }
        }
      }
      parameters: {
        '$connections': {
          defaultValue: {}
          type: 'Object'
        }
      }
    }
    parameters: {
      '$connections': {
        value: {
          office365: {
            connectionId: office365Connection.id
            connectionName: 'office365'
            id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'office365')
          }
        }
      }
    }
  }
}

output workflowResourceId string = emailSendingWorkflow.id
output managedIdentityPrincipalId string = emailSendingWorkflow.identity.principalId
output triggerUrl string = emailSendingWorkflow.properties.accessEndpoint
output connectionId string = office365Connection.id
