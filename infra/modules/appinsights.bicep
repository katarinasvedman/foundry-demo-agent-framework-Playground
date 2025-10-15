@description('Application Insights name')
param appInsightsName string

@description('Location for Application Insights')
param location string = resourceGroup().location

@description('Application type')
param applicationType string = 'web'

// Log Analytics Workspace for Application Insights
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${appInsightsName}-workspace'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: applicationType
  properties: {
    Application_Type: applicationType
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
  }
}

output appInsightsName string = appInsights.name
output appInsightsId string = appInsights.id
output instrumentationKey string = appInsights.properties.InstrumentationKey
output connectionString string = appInsights.properties.ConnectionString
output workspaceId string = logAnalyticsWorkspace.id
