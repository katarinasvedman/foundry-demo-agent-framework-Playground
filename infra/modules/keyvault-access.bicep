@description('Key Vault name')
param keyVaultName string

@description('Principal ID to grant access')
param principalId string

@description('Principal type (User or ServicePrincipal)')
@allowed(['User', 'ServicePrincipal'])
param principalType string = 'ServicePrincipal'

// Get reference to existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Role definition for Key Vault Secrets User
resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
}

// Grant access to Key Vault secrets
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, principalId, keyVaultSecretsUserRole.id)
  properties: {
    roleDefinitionId: keyVaultSecretsUserRole.id
    principalId: principalId
    principalType: principalType
  }
}

output roleAssignmentId string = keyVaultRoleAssignment.id
