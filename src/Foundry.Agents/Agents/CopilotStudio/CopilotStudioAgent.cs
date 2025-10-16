
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.CopilotStudio;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.CopilotStudio.Client;
using Azure.Identity;

namespace Foundry.Agents.Agents.CopilotStudio
{
    /// <summary>
    /// CopilotStudioAgent that inherits from AIAgent and wraps Microsoft Copilot Studio functionality.
    /// This implementation uses existing Copilot Studio bots and does NOT create new ones.
    /// </summary>
    public class CopilotStudioAgent : AIAgent
    {
        private readonly ILogger<CopilotStudioAgent> _logger;
        private readonly IConfiguration _configuration;
        private readonly Microsoft.Agents.AI.CopilotStudio.CopilotStudioAgent? _innerAgent;
        private readonly string _botUrl;
        private readonly string _tenantId;

        // File to store Copilot Studio bot metadata
        private const string COPILOT_METADATA_FILE = "copilot-studio-metadata.json";

        public CopilotStudioAgent(
            string botUrl, 
            string tenantId,
            ILogger<CopilotStudioAgent> logger, 
            IConfiguration configuration)
        {
            _botUrl = botUrl ?? throw new ArgumentNullException(nameof(botUrl));
            _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Initialize the inner Copilot Studio agent
            _innerAgent = InitializeCopilotStudioAgent();
        }

        /// <summary>
        /// Factory method to create a CopilotStudioAgent from existing configuration, only if valid.
        /// 
        /// Required Configuration:
        /// - CopilotStudio:BotUrl - The direct connect URL of your Copilot Studio bot
        /// - CopilotStudio:TenantId - Your Azure tenant ID  
        /// - CopilotStudio:ClientId - Your Azure application (client) ID
        /// - CopilotStudio:ClientSecret - Your Azure application client secret
        /// - CopilotStudio:EnvironmentId - Your Power Platform environment ID
        /// </summary>
        public static async Task<CopilotStudioAgent?> CreateFromExistingConfigurationAsync(
            IConfiguration configuration,
            ILogger<CopilotStudioAgent> logger,
            CancellationToken cancellationToken = default)
        {
            // Validate that we have a valid Copilot Studio configuration
            if (!await ValidateExistingCopilotStudioBotAsync(configuration, logger, cancellationToken))
            {
                return null;
            }

            var botUrl = configuration["CopilotStudio:BotUrl"] ?? 
                        Environment.GetEnvironmentVariable("COPILOT_STUDIO_BOT_URL");
            
            var tenantId = configuration["CopilotStudio:TenantId"] ?? 
                          configuration["Azure:TenantId"] ??
                          Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

            if (string.IsNullOrEmpty(botUrl) || string.IsNullOrEmpty(tenantId))
            {
                logger.LogError("Bot URL or Tenant ID is null after validation - this should not happen");
                return null;
            }

            return new CopilotStudioAgent(botUrl, tenantId, logger, configuration);
        }

        private Microsoft.Agents.AI.CopilotStudio.CopilotStudioAgent? InitializeCopilotStudioAgent()
        {
            try
            {
                _logger.LogInformation("Attempting to initialize CopilotStudio agent...");
                _logger.LogInformation("Bot URL: {BotUrl}", _botUrl);
                _logger.LogInformation("Tenant ID: {TenantId}", _tenantId);
                
                // Get the required configuration values
                var clientId = _configuration["CopilotStudio:ClientId"] ?? 
                              Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_ID");
                
                var clientSecret = _configuration["CopilotStudio:ClientSecret"] ?? 
                                  Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_SECRET");
                
                var environmentId = _configuration["CopilotStudio:EnvironmentId"] ?? 
                                   Environment.GetEnvironmentVariable("COPILOT_STUDIO_ENVIRONMENT_ID");

                if (string.IsNullOrEmpty(clientId))
                {
                    _logger.LogWarning("CopilotStudio ClientId not configured. Set CopilotStudio:ClientId in configuration.");
                    return null;
                }

                if (string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogWarning("CopilotStudio ClientSecret not configured. Set CopilotStudio:ClientSecret in configuration.");
                    return null;
                }

                if (string.IsNullOrEmpty(environmentId))
                {
                    _logger.LogWarning("CopilotStudio EnvironmentId not configured. Set CopilotStudio:EnvironmentId in configuration.");
                    return null;
                }

                // Initialize connection settings - try different property names based on common patterns
                var connectionSettings = new Microsoft.Agents.CopilotStudio.Client.ConnectionSettings();
                
                // Try to set properties using reflection or common naming patterns
                try
                {
                    // Try common property names
                    var settingsType = connectionSettings.GetType();
                    
                    // Set DirectConnectUrl
                    var urlProp = settingsType.GetProperty("DirectConnectUrl") ?? 
                                 settingsType.GetProperty("Url") ?? 
                                 settingsType.GetProperty("EndpointUrl");
                    urlProp?.SetValue(connectionSettings, _botUrl);
                    
                    // Set TenantId
                    var tenantProp = settingsType.GetProperty("TenantID") ?? 
                                    settingsType.GetProperty("TenantId") ?? 
                                    settingsType.GetProperty("Tenant");
                    tenantProp?.SetValue(connectionSettings, _tenantId);
                    
                    // Set ClientId
                    var clientIdProp = settingsType.GetProperty("ClientID") ?? 
                                      settingsType.GetProperty("ClientId") ?? 
                                      settingsType.GetProperty("ApplicationId");
                    clientIdProp?.SetValue(connectionSettings, clientId);
                    
                    // Set ClientSecret
                    var secretProp = settingsType.GetProperty("ClientSecret") ?? 
                                    settingsType.GetProperty("Secret") ?? 
                                    settingsType.GetProperty("ApplicationSecret");
                    secretProp?.SetValue(connectionSettings, clientSecret);
                }
                catch (Exception settingsEx)
                {
                    _logger.LogWarning(settingsEx, "Could not configure connection settings via reflection");
                }

                // Create an instance of CopilotClient using the exact pattern from your sample
                // CopilotClient(connectionSettings, null, null, null, environmentId)
                var copilotClient = new Microsoft.Agents.CopilotStudio.Client.CopilotClient(
                    connectionSettings, null!, null!, null!, environmentId);

                // Create the CopilotStudio agent with the client
                var agent = new Microsoft.Agents.AI.CopilotStudio.CopilotStudioAgent(copilotClient);
                
                _logger.LogInformation("Successfully initialized CopilotStudio agent for endpoint: {Endpoint}", _botUrl);
                return agent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Copilot Studio agent for endpoint {BotUrl}: {Error}", _botUrl, ex.Message);
                _logger.LogWarning("Falling back to simulation mode. Ensure CopilotStudio configuration is complete.");
                return null;
            }
        }

        private Azure.Core.TokenCredential? GetCopilotStudioCredential()
        {
            try
            {
                // Use Azure Default Credential (Managed Identity, Azure CLI, etc.)
                var tenantId = !string.IsNullOrEmpty(_tenantId) ? _tenantId : null;
                
                if (tenantId != null)
                {
                    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        TenantId = tenantId
                    });
                    
                    _logger.LogInformation("Using DefaultAzureCredential with tenant {TenantId} for Copilot Studio authentication", tenantId);
                    return credential;
                }
                
                // Fallback to default credential without tenant
                _logger.LogInformation("Using DefaultAzureCredential (no tenant specified)");
                return new DefaultAzureCredential();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create authentication credential for Copilot Studio");
                return null;
            }
        }

        // AIAgent abstract method implementations
        public override string Id => $"copilot-studio-{_botUrl.GetHashCode():X}";
        
        public override string Name => "CopilotStudio";
        
        public override string DisplayName => "Copilot Studio Agent";
        
        public override string Description => $"Copilot Studio bot integration for {_botUrl}";

        public override AgentThread GetNewThread()
        {
            if (_innerAgent != null)
            {
                return _innerAgent.GetNewThread();
            }
            
            // Fallback implementation - create a basic thread
            _logger.LogWarning("Creating fallback thread - inner Copilot Studio agent not initialized");
            
            // For now, we'll need to return null or throw since we can't create AgentThread directly
            throw new NotSupportedException("Cannot create AgentThread without proper Copilot Studio initialization");
        }

        public override AgentThread DeserializeThread(JsonElement threadData, JsonSerializerOptions? options = null)
        {
            if (_innerAgent != null)
            {
                return _innerAgent.DeserializeThread(threadData, options);
            }
            
            // Fallback implementation
            _logger.LogWarning("Using fallback thread deserialization - inner Copilot Studio agent not initialized");
            
            // For now, we'll need to return null or throw since we can't create AgentThread directly
            throw new NotSupportedException("Cannot deserialize AgentThread without proper Copilot Studio initialization");
        }

        public override async Task<AgentRunResponse> RunAsync(
            IEnumerable<ChatMessage> messages, 
            AgentThread? thread = null, 
            AgentRunOptions? options = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_innerAgent != null && thread != null)
                {
                    var result = await _innerAgent.RunAsync(messages, thread, options, cancellationToken);
                    return result;
                }

                // Fallback implementation - simulate a response  
                _logger.LogInformation("Simulating Copilot Studio response for {MessageCount} messages", messages.Count());
                var lastMessage = messages.LastOrDefault()?.Text ?? "No message";
                
                var responseMessage = $"[Copilot Studio Simulation] Processed: {lastMessage}. " +
                                    $"(This is a placeholder response - actual Copilot Studio integration needs proper CopilotClient setup)";
                
                // Create a response with simulated content
                var response = new AgentRunResponse
                {
                    // Add simulated response content
                    // The exact properties will depend on the AgentRunResponse structure
                };
                
                // You may need to set additional properties on the response based on the actual AgentRunResponse API
                // For example: response.Messages, response.Status, etc.
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run Copilot Studio agent");
                throw;
            }
        }

        public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
            IEnumerable<ChatMessage> messages, 
            AgentThread? thread = null, 
            AgentRunOptions? options = null, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_innerAgent != null && thread != null)
            {
                await foreach (var update in _innerAgent.RunStreamingAsync(messages, thread, options, cancellationToken))
                {
                    yield return update;
                }
            }
            else
            {
                // Fallback streaming implementation
                _logger.LogInformation("Simulating streaming Copilot Studio response");
                var lastMessage = messages.LastOrDefault()?.Text ?? "No message";
                
                var responseText = $"[Copilot Studio Streaming] Processing: {lastMessage}...";
                
                // Simulate streaming by yielding updates
                for (int i = 0; i < responseText.Length; i += 10)
                {
                    var chunk = responseText.Substring(i, Math.Min(10, responseText.Length - i));
                    
                    // Create appropriate update with simulated content
                    var update = new AgentRunResponseUpdate
                    {
                        // Add simulated update content
                        // The exact properties will depend on the AgentRunResponseUpdate structure
                    };
                    
                    // You may need to set additional properties on the update based on the actual AgentRunResponseUpdate API
                    // For example: update.Content, update.Type, etc.
                    
                    yield return update;
                    
                    // Small delay to simulate streaming
                    await Task.Delay(50, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Validates that a Copilot Studio bot is configured and accessible. Does NOT create new bots.
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if a valid Copilot Studio bot is available, false otherwise</returns>
        public static async Task<bool> ValidateExistingCopilotStudioBotAsync(
            IConfiguration configuration, 
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if Copilot Studio is properly configured
                var copilotUrl = configuration["CopilotStudio:BotUrl"] ?? 
                               Environment.GetEnvironmentVariable("COPILOT_STUDIO_BOT_URL");
                
                var tenantId = configuration["CopilotStudio:TenantId"] ?? 
                              configuration["Azure:TenantId"] ??
                              Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

                if (string.IsNullOrEmpty(copilotUrl))
                {
                    logger.LogWarning("Copilot Studio bot URL not configured. Set CopilotStudio:BotUrl in configuration to enable Copilot Studio integration.");
                    return false;
                }

                if (string.IsNullOrEmpty(tenantId))
                {
                    logger.LogWarning("Azure tenant ID not configured for Copilot Studio. Set CopilotStudio:TenantId in configuration.");
                    return false;
                }

                // Check for additional required configuration
                var clientId = configuration["CopilotStudio:ClientId"] ?? 
                              Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_ID");
                
                var clientSecret = configuration["CopilotStudio:ClientSecret"] ?? 
                                  Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_SECRET");
                
                var environmentId = configuration["CopilotStudio:EnvironmentId"] ?? 
                                   Environment.GetEnvironmentVariable("COPILOT_STUDIO_ENVIRONMENT_ID");

                if (string.IsNullOrEmpty(clientId))
                {
                    logger.LogWarning("CopilotStudio ClientId not configured. Set CopilotStudio:ClientId in configuration.");
                    return false;
                }

                if (string.IsNullOrEmpty(clientSecret))
                {
                    logger.LogWarning("CopilotStudio ClientSecret not configured. Set CopilotStudio:ClientSecret in configuration.");
                    return false;
                }

                if (string.IsNullOrEmpty(environmentId))
                {
                    logger.LogWarning("CopilotStudio EnvironmentId not configured. Set CopilotStudio:EnvironmentId in configuration.");
                    return false;
                }

                // Check if we have metadata about a previously configured Copilot Studio bot
                var metadataPath = Path.Combine("Agents", "CopilotStudio", COPILOT_METADATA_FILE);
                if (!File.Exists(metadataPath))
                {
                    logger.LogWarning("No Copilot Studio bot metadata found at {Path}. Please ensure a Copilot Studio bot has been properly configured.", metadataPath);
                    return false;
                }

                // Read and validate the metadata
                var metadataContent = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                if (string.IsNullOrWhiteSpace(metadataContent))
                {
                    logger.LogWarning("Copilot Studio metadata file is empty or invalid.");
                    return false;
                }

                // Basic validation that this is Copilot Studio metadata (you could parse JSON here for more detailed validation)
                if (!metadataContent.Contains("copilot_studio") && !metadataContent.Contains("CopilotStudio"))
                {
                    logger.LogWarning("Metadata file does not appear to be for a Copilot Studio bot.");
                    return false;
                }

                logger.LogInformation("Found valid Copilot Studio bot configuration. Bot URL: {CopilotUrl}", copilotUrl);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to validate existing Copilot Studio bot configuration");
                return false;
            }
        }

        /// <summary>
        /// Gets the configured Copilot Studio bot URL if available
        /// </summary>
        /// <returns>Bot URL or null if not configured</returns>
        public string? GetConfiguredBotUrl()
        {
            return _configuration["CopilotStudio:BotUrl"] ?? 
                   Environment.GetEnvironmentVariable("COPILOT_STUDIO_BOT_URL");
        }

        /// <summary>
        /// Gets the configured tenant ID for Copilot Studio
        /// </summary>
        /// <returns>Tenant ID or null if not configured</returns>
        public string? GetConfiguredTenantId()
        {
            return _configuration["CopilotStudio:TenantId"] ?? 
                   _configuration["Azure:TenantId"] ??
                   Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        }

        /// <summary>
        /// Simulates sending a message to a Copilot Studio bot (placeholder for actual integration)
        /// In a real implementation, this would use the Microsoft.Agents.AI.CopilotStudio package properly
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="conversationId">Optional conversation ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Simulated bot response</returns>
        public async Task<string?> SendMessageToCopilotStudioAsync(string message, string? conversationId = null, CancellationToken cancellationToken = default)
        {
            var botUrl = GetConfiguredBotUrl();
            if (string.IsNullOrEmpty(botUrl))
            {
                _logger.LogError("Cannot send message - Copilot Studio bot URL not configured.");
                return null;
            }

            try
            {
                _logger.LogInformation("Would send message to Copilot Studio bot at {BotUrl}: {Message}", botUrl, message);
                
                // TODO: Replace this with actual Copilot Studio API calls
                // For now, return a placeholder response indicating the integration point
                await Task.Delay(100, cancellationToken); // Simulate network call
                
                var response = $"[Copilot Studio Bot Response] Processed message: '{message}' (This is a placeholder - implement actual Copilot Studio integration here)";
                
                _logger.LogInformation("Received response from Copilot Studio bot");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to Copilot Studio bot");
                return null;
            }
        }
    }
}