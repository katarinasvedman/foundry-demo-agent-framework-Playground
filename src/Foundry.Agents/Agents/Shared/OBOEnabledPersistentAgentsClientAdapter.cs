using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.AI.Agents.Persistent;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Foundry.Agents.Agents.Shared
{
    /// <summary>
    /// OBO-enabled adapter that can propagate user tokens to MCP servers
    /// </summary>
    public class OBOEnabledPersistentAgentsClientAdapter : RealPersistentAgentsClientAdapter, IPersistentAgentsClientAdapterWithOBO
    {
        private readonly HttpClient _httpClient;
        
        public OBOEnabledPersistentAgentsClientAdapter(string endpoint, IConfiguration configuration, 
            ILogger<RealPersistentAgentsClientAdapter>? logger = null, HttpClient? httpClient = null) 
            : base(endpoint, configuration, logger)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<string?> RunAgentAsUserAsync(string agentId, object payload, UserContext userContext, CancellationToken cancellationToken = default)
        {
            // For now, delegate to the base implementation
            // In a full OBO implementation, you would:
            // 1. Create a custom TokenCredential using the user's access token
            // 2. Use OnBehalfOfCredential to get a token for the downstream service
            // 3. Pass this token to the MCP server calls
            
            return await RunAgentAsync(agentId, payload, cancellationToken);
        }

        public async Task<string?> CreateAgentWithUserContextAsync(string modelDeploymentName, string name, string? instructions, 
            UserContext userContext, IEnumerable<string>? toolTypes = null)
        {
            // For now, delegate to the base implementation
            // In a full implementation, you would configure the agent with user-specific settings
            
            return await CreateAgentAsync(modelDeploymentName, name, instructions, toolTypes);
        }

        /// <summary>
        /// Call MCP server with user token using OBO flow
        /// </summary>
        public async Task<string?> CallMcpServerWithUserTokenAsync(string mcpServerUrl, string method, object parameters, UserContext userContext)
        {
            try
            {
                if (string.IsNullOrEmpty(userContext.AccessToken))
                {
                    throw new InvalidOperationException("User access token is required for OBO flow");
                }

                // Create OBO credential to get token for the MCP server
                var oboCredential = new OnBehalfOfCredential(
                    tenantId: userContext.TenantId ?? throw new InvalidOperationException("Tenant ID required"),
                    clientId: GetClientId(), // Your application's client ID
                    clientSecret: GetClientSecret(), // Your application's client secret
                    userAssertion: userContext.AccessToken);

                // Get token for the target MCP server scope
                var tokenRequest = new TokenRequestContext(new[] { GetMcpServerScope(mcpServerUrl) });
                var tokenResult = await oboCredential.GetTokenAsync(tokenRequest, cancellationToken: default);

                // Call MCP server with the OBO token
                var mcpRequest = new
                {
                    jsonrpc = "2.0",
                    id = Guid.NewGuid().ToString(),
                    method = method,
                    @params = parameters
                };

                var json = JsonConvert.SerializeObject(mcpRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add the OBO token as Authorization header
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.Token);

                var response = await _httpClient.PostAsync(mcpServerUrl, content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to call MCP server with OBO flow: {ex.Message}", ex);
            }
        }

        private string GetClientId()
        {
            // Get from configuration or environment
            return Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? 
                   throw new InvalidOperationException("AZURE_CLIENT_ID environment variable required for OBO flow");
        }

        private string GetClientSecret()
        {
            // Get from configuration or Key Vault
            return Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? 
                   throw new InvalidOperationException("AZURE_CLIENT_SECRET environment variable required for OBO flow");
        }

        private string GetMcpServerScope(string mcpServerUrl)
        {
            // Determine the appropriate scope for the MCP server
            // This might be a specific API scope or the default scope for the service
            if (mcpServerUrl.Contains("apim-"))
            {
                // For API Management, use the default scope
                return $"{GetApiManagementBaseUrl(mcpServerUrl)}/.default";
            }
            
            // Default Azure scope
            return "https://cognitiveservices.azure.com/.default";
        }

        private string GetApiManagementBaseUrl(string mcpServerUrl)
        {
            var uri = new Uri(mcpServerUrl);
            return $"{uri.Scheme}://{uri.Host}";
        }
    }
}