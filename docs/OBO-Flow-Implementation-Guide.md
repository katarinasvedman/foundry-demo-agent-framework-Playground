# On-Behalf-Of (OBO) Flow Implementation Guide

## Overview
This guide explains how to implement On-Behalf-Of flow for Azure AI agents to call external services (like MCP servers) using the original user's identity rather than the service's managed identity.

## 🔧 Configuration Steps

### 1. Azure App Registration
Create an Azure App Registration for your application:

```bash
# Create app registration
az ad app create --display-name "FoundryAgentsOBO" --required-resource-accesses '[
    {
        "resourceAppId": "https://cognitiveservices.azure.com",
        "resourceAccess": [
            {
                "id": "b340eb25-3456-403f-be2f-af17a6c65e73",
                "type": "Scope"
            }
        ]
    }
]'

# Create service principal
az ad sp create --id <app-id-from-above>

# Create client secret
az ad app credential reset --id <app-id> --display-name "OBOSecret"
```

### 2. API Management Configuration (if using APIM)
Configure your API Management service to accept OBO tokens:

```xml
<!-- Add to your API policy -->
<inbound>
    <validate-jwt header-name="Authorization" failed-validation-httpcode="401">
        <openid-config url="https://login.microsoftonline.com/{tenant-id}/v2.0/.well-known/openid-configuration" />
        <audiences>
            <audience>api://{your-app-id}</audience>
        </audiences>
        <issuers>
            <issuer>https://sts.windows.net/{tenant-id}/</issuer>
        </issuers>
    </validate-jwt>
</inbound>
```

### 3. Environment Variables
Set the following environment variables:

```bash
# Required for OBO flow
AZURE_CLIENT_ID=<your-app-registration-client-id>
AZURE_CLIENT_SECRET=<your-app-registration-client-secret>
AZURE_TENANT_ID=<your-tenant-id>

# Optional: Specific scopes for your MCP server
MCP_SERVER_SCOPE=api://your-mcp-server/.default
```

### 4. Application Configuration
Update your `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-app-registration-client-id",
    "ClientSecret": "your-client-secret"
  },
  "SentimentAgent": {
    "Enabled": true,
    "McpServerUrl": "https://apim-jqucwaho6edqo.azure-api.net/sentimentmcp/mcp",
    "RequiredScope": "api://your-mcp-server/.default"
  }
}
```

## 🔄 Implementation Patterns

### Pattern 1: Web Application with User Authentication
For web applications where users authenticate first:

```csharp
// In your web controller
[Authorize]
public async Task<IActionResult> AnalyzeSentiment([FromBody] string text)
{
    var accessToken = await HttpContext.GetTokenAsync("access_token");
    var userContext = new UserContext
    {
        AccessToken = accessToken,
        UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        UserPrincipalName = User.FindFirst(ClaimTypes.Upn)?.Value,
        TenantId = User.FindFirst("tid")?.Value
    };
    
    var adapter = new OBOEnabledPersistentAgentsClientAdapter(endpoint, configuration);
    var result = await adapter.RunAgentAsUserAsync(agentId, new { text }, userContext);
    
    return Ok(result);
}
```

### Pattern 2: Service-to-Service with Delegated Permissions
For services that need to act on behalf of users:

```csharp
// Get user token from the incoming request
var userToken = ExtractBearerToken(Request);
var userContext = new UserContext 
{ 
    AccessToken = userToken,
    TenantId = configuration["AzureAd:TenantId"]
};

// Use OBO-enabled adapter
var oboAdapter = new OBOEnabledPersistentAgentsClientAdapter(endpoint, configuration);
var result = await oboAdapter.CallMcpServerWithUserTokenAsync(
    mcpServerUrl, "tools/call", parameters, userContext);
```

## 🛡️ Security Considerations

### 1. Token Validation
- Always validate incoming user tokens
- Check token expiration and refresh if needed
- Verify audience and issuer claims

### 2. Scope Management
- Define specific scopes for your MCP server
- Use least-privilege principle
- Document required permissions

### 3. Error Handling
- Handle OBO token acquisition failures gracefully
- Log security events for auditing
- Implement token caching where appropriate

## 🧪 Testing OBO Flow

### Test the OBO Token Exchange
```bash
# Get user token (interactive login)
az account get-access-token --scope "api://your-app-id/.default"

# Test OBO token exchange
curl -X POST "https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer" \
  -d "client_id={your-app-id}" \
  -d "client_secret={your-client-secret}" \
  -d "assertion={user-access-token}" \
  -d "scope=https://cognitiveservices.azure.com/.default" \
  -d "requested_token_use=on_behalf_of"
```

## 📋 Next Steps

1. **Configure App Registration**: Set up the Azure AD app with proper permissions
2. **Update MCP Server**: Ensure your MCP server can validate OBO tokens
3. **Modify Agent Code**: Use the OBO-enabled adapter in your agent implementations
4. **Test End-to-End**: Verify the complete OBO flow works as expected

## 🔗 References
- [Microsoft Identity Platform OBO Flow](https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-on-behalf-of-flow)
- [Azure AI Services Authentication](https://docs.microsoft.com/en-us/azure/cognitive-services/authentication)