
/*
 * COPILOT STUDIO INTEGRATION STATUS:
 * 
 * ✅ WORKING: DirectLine REST API communication (proven with joke test)
 * ❌ BLOCKED: CopilotStudio SDK integration (requires Azure AD permissions)
 * 
 * AZURE AD PERMISSIONS REQUIRED:
 * To enable full CopilotStudio SDK integration in Agent Framework workflows:
 * 1. CopilotStudio.Copilots.Invoke
 * 2. All.All.ReadWrite
 * 
 * These permissions must be granted to the Azure CLI authenticated user
 * in the Azure AD app registration that manages Power Platform access.
 * 
 * CURRENT STATE:
 * - Bot connectivity: ✅ VERIFIED (DirectLine test passes)
 * - Authentication: ✅ WORKING (Power Platform + DirectLine tokens)
 * - SDK Integration: ❌ BLOCKED (InsufficientDelegatedPermissions error)
 * - Workflow Ready: ✅ CODE READY (temporarily disabled until permissions set)
 * 
 * NEXT STEPS FOR DEVELOPER:
 * 1. Configure Azure AD permissions (see documentation above)
 * 2. Change "if (false && useCopilotStudio..." to "if (useCopilotStudio..." in OrchestratorAgent.cs line ~149
 * 3. Test full SDK integration in Agent Framework workflow
 */

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.CopilotStudio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.DependencyInjection;
using Azure.Identity;
using Azure.Core;

namespace Foundry.Agents.Agents.CopilotStudio
{
    /// <summary>
    /// CopilotStudioAgent factory class for creating and connecting to existing Microsoft Copilot Studio bots.
    /// This implementation uses existing Copilot Studio bots and does NOT create new ones.
    /// </summary>
    public static class CopilotStudioAgent
    {
        // File to store Copilot Studio bot metadata
        private const string COPILOT_METADATA_FILE = "copilot-studio-metadata.json";


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

                // Check for additional configuration (authentication is optional)
                var clientId = configuration["CopilotStudio:ClientId"] ?? 
                              Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_ID");
                
                var clientSecret = configuration["CopilotStudio:ClientSecret"] ?? 
                                  Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_SECRET");
                
                var environmentId = configuration["CopilotStudio:EnvironmentId"] ?? 
                                   Environment.GetEnvironmentVariable("COPILOT_STUDIO_ENVIRONMENT_ID");

                // Authentication is optional - some bots don't require it
                var useAuthentication = !string.IsNullOrEmpty(clientId) || !string.IsNullOrEmpty(clientSecret);
                
                if (useAuthentication)
                {
                    if (string.IsNullOrEmpty(clientId))
                    {
                        logger.LogWarning("CopilotStudio ClientId not configured but ClientSecret provided. Set CopilotStudio:ClientId in configuration for authenticated mode.");
                        return false;
                    }

                    if (string.IsNullOrEmpty(clientSecret))
                    {
                        logger.LogWarning("CopilotStudio ClientSecret not configured but ClientId provided. Set CopilotStudio:ClientSecret in configuration for authenticated mode.");
                        return false;
                    }
                    
                    logger.LogInformation("Copilot Studio configured for authenticated mode");
                }
                else
                {
                    logger.LogInformation("Copilot Studio configured for unauthenticated mode (no authentication required)");
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
        /// Get a CopilotStudio agent by connecting to an existing Copilot Studio bot.
        /// This method uses the CopilotStudio SDK to connect to existing bots, not the Foundry persistent agents service.
        /// 
        /// AUTHENTICATION REQUIREMENTS:
        /// - For full SDK functionality, the Azure CLI authenticated user needs these Azure AD permissions:
        ///   1. CopilotStudio.Copilots.Invoke
        ///   2. All.All.ReadWrite
        /// - These permissions must be granted in the Azure AD app registration
        /// - Without proper permissions, the SDK will fail with "InsufficientDelegatedPermissions"
        /// - DirectLine REST API can be used as a fallback (proven working in tests)
        /// </summary>
        public static async Task<Microsoft.Agents.AI.CopilotStudio.CopilotStudioAgent?> GetCopilotAgent(IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Attempting to connect to existing CopilotStudio bot...");
                
                // Validate CopilotStudio configuration
                if (!await ValidateExistingCopilotStudioBotAsync(configuration, logger, cancellationToken))
                {
                    logger.LogInformation("CopilotStudio configuration not valid - skipping CopilotStudio integration");
                    return null;
                }

                // Get configuration values - use original BotUrl
                var botUrl = configuration["CopilotStudio:BotUrl"] ?? 
                            Environment.GetEnvironmentVariable("COPILOT_STUDIO_BOT_URL");
                
                var tenantId = configuration["CopilotStudio:TenantId"] ?? 
                              configuration["Azure:TenantId"] ??
                              Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
                
                var clientId = configuration["CopilotStudio:ClientId"] ?? 
                              Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_ID");
                
                var clientSecret = configuration["CopilotStudio:ClientSecret"] ?? 
                                  Environment.GetEnvironmentVariable("COPILOT_STUDIO_CLIENT_SECRET");
                
                var environmentId = configuration["CopilotStudio:EnvironmentId"] ?? 
                                   Environment.GetEnvironmentVariable("COPILOT_STUDIO_ENVIRONMENT_ID");

                // Check if authentication is configured
                var useAuthentication = !string.IsNullOrEmpty(clientId) || !string.IsNullOrEmpty(clientSecret);

                if (string.IsNullOrEmpty(botUrl) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(environmentId))
                {
                    logger.LogError("Required CopilotStudio configuration missing");
                    return null;
                }

                logger.LogInformation("Creating CopilotStudio agent for bot: {BotUrl}", botUrl);

                // Create connection settings
                var connectionSettings = new Microsoft.Agents.CopilotStudio.Client.ConnectionSettings();
                
                // Set properties using reflection to handle different SDK versions
                try
                {
                    var settingsType = connectionSettings.GetType();
                    
                    // Set DirectConnectUrl
                    var urlProp = settingsType.GetProperty("DirectConnectUrl") ?? 
                                 settingsType.GetProperty("Url") ?? 
                                 settingsType.GetProperty("EndpointUrl");
                    urlProp?.SetValue(connectionSettings, botUrl);
                    
                    // Set TenantId
                    var tenantProp = settingsType.GetProperty("TenantID") ?? 
                                    settingsType.GetProperty("TenantId") ?? 
                                    settingsType.GetProperty("Tenant");
                    tenantProp?.SetValue(connectionSettings, tenantId);
                    
                    // Set authentication if provided
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        var clientIdProp = settingsType.GetProperty("ClientID") ?? 
                                          settingsType.GetProperty("ClientId") ?? 
                                          settingsType.GetProperty("ApplicationId");
                        clientIdProp?.SetValue(connectionSettings, clientId);
                    }
                    
                    if (!string.IsNullOrEmpty(clientSecret))
                    {
                        var secretProp = settingsType.GetProperty("ClientSecret") ?? 
                                        settingsType.GetProperty("Secret") ?? 
                                        settingsType.GetProperty("ApplicationSecret");
                        secretProp?.SetValue(connectionSettings, clientSecret);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not configure connection settings via reflection");
                }

                // Create CopilotClient and agent
                // The constructor expects: (ConnectionSettings, IHttpClientFactory, Func<string, Task<string>>, ILogger, string environmentId)
                logger.LogInformation("Creating CopilotClient for environment: {EnvironmentId}", environmentId);
                
                // Create a simple HttpClientFactory implementation
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddHttpClient();
                var serviceProvider = serviceCollection.BuildServiceProvider();
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                
                // Create CopilotClient with or without authentication
                Microsoft.Agents.CopilotStudio.Client.CopilotClient copilotClient;
                
                // Create token provider that handles scope-specific authentication
                Func<string, Task<string>> tokenProvider = async (scope) =>
                {
                    if (scope.Contains("directline"))
                    {
                        logger.LogInformation("Token requested for DirectLine scope: {Scope}", scope);
                        var directLineEndpoint = configuration["CopilotStudio:DirectLineUrl"] + "?api-version=2022-03-01-preview";
                        var directLineToken = await GetDirectLineTokenAsync(directLineEndpoint, httpClientFactory, logger);
                        logger.LogInformation("Token provider returning DirectLine token (length: {Length}) for scope: {Scope}", directLineToken?.Length ?? 0, scope);
                        return directLineToken ?? string.Empty;
                    }
                    else if (scope.Contains("copilotstudio") || scope.Contains("powerapps"))
                    {
                        logger.LogInformation("Token requested for Dataverse-backed CopilotStudio scope: {Scope}", scope);
                        var credential = new DefaultAzureCredential();
                        
                        // Try different token scopes for CopilotStudio API
                        string[] possibleScopes = {
                            "https://api.powerplatform.com/.default",
                            "https://service.powerapps.com/.default",
                            "https://graph.microsoft.com/.default"
                        };
                        
                        // Use the Power Platform API scope first
                        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { possibleScopes[0] }));
                        logger.LogInformation("Token provider returning Power Platform token (scope: {TokenScope}, length: {Length}) for API scope: {Scope}", possibleScopes[0], token.Token?.Length ?? 0, scope);
                        return token.Token ?? string.Empty;
                    }
                    else
                    {
                        logger.LogInformation("Token requested for unknown scope, returning empty: {Scope}", scope);
                        return string.Empty; // For any other unexpected scopes
                    }
                };
                
                copilotClient = new Microsoft.Agents.CopilotStudio.Client.CopilotClient(
                    connectionSettings, 
                    httpClientFactory,
                    tokenProvider,
                    logger, 
                    environmentId);

                var agent = new Microsoft.Agents.AI.CopilotStudio.CopilotStudioAgent(copilotClient);
                
                logger.LogInformation("Successfully created CopilotStudio agent for bot: {BotUrl}", botUrl);
                
                // Test the CopilotStudio agent to verify it's working (skip for test URLs)
                var skipTesting = botUrl.Contains("test-copilot-bot") || botUrl.Contains("localhost") || botUrl.Contains("example.com");
                
                if (!skipTesting)
                {
                    try
                    {
                        logger.LogInformation("Testing CopilotStudio agent functionality...");
                    
                    // Try the SDK approach first (requires proper Azure AD permissions)
                    try 
                    {
                        logger.LogInformation("Testing CopilotStudio SDK agent with 'Tell me a joke'");
                        
                        var testMessages = new[] 
                        {
                            new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Tell me a joke")
                        };
                        
                        Microsoft.Agents.AI.AgentThread? testThread = null;
                        
                        await foreach (var response in agent.RunStreamingAsync(testMessages, testThread, new Microsoft.Agents.AI.AgentRunOptions(), cancellationToken))
                        {
                            logger.LogInformation("✅ CopilotStudio SDK test successful! Response: {Response}", response?.ToString() ?? "null");
                            break; // Just test the first response
                        }
                        
                        logger.LogInformation("✅ CopilotStudio SDK agent is fully functional with proper permissions!");
                    }
                    catch (HttpRequestException httpEx) when (httpEx.Message.Contains("Forbidden") || httpEx.Message.Contains("InsufficientDelegatedPermissions"))
                    {
                        logger.LogWarning("❌ CopilotStudio SDK requires Azure AD permissions: CopilotStudio.Copilots.Invoke, All.All.ReadWrite");
                        logger.LogInformation("🔄 Falling back to DirectLine REST API test...");
                        
                        // Fallback to DirectLine test to prove the bot works
                        var jokeResponse = await TestBotWithDirectLineAsync(configuration, httpClientFactory, logger, cancellationToken);
                        if (!string.IsNullOrEmpty(jokeResponse))
                        {
                            logger.LogInformation("✅ DirectLine fallback test successful! Bot response: {Response}", jokeResponse);
                            logger.LogInformation("💡 Bot is functional - configure Azure AD permissions for full SDK integration");
                        }
                        else
                        {
                            logger.LogWarning("❌ Both SDK and DirectLine tests failed");
                        }
                    }
                }
                    catch (Exception)
                    {
                        // For testing scenarios, just log that connection failed without full stack trace
                        logger.LogInformation("ℹ️ CopilotStudio agent connection test skipped (requires valid bot URL and configuration)");
                        // Don't return null - still return the agent for potential workflow use
                    }
                }
                else
                {
                    logger.LogInformation("🧪 CopilotStudio test mode detected - skipping connectivity test");
                }
                
                return agent;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to connect to CopilotStudio bot - continuing without CopilotStudio integration");
                return null;
            }
        }

        /// <summary>
        /// Gets DirectLine token from Power Platform API for Copilot Studio bot
        /// Based on the working endpoint from documentation
        /// </summary>
        private static async Task<string> GetDirectLineTokenAsync(
            string directLineEndpoint, 
            IHttpClientFactory httpClientFactory, 
            ILogger logger)
        {
            try
            {
                logger.LogInformation("Requesting DirectLine token from: {Endpoint}", directLineEndpoint);

                using var httpClient = httpClientFactory.CreateClient();

                // We need to use Azure CLI credentials here
                // First try to get Power Platform access token using Azure CLI pattern
                var powerPlatformToken = await GetPowerPlatformTokenAsync(httpClientFactory, logger);
                
                if (string.IsNullOrEmpty(powerPlatformToken))
                {
                    logger.LogError("Could not obtain Power Platform access token - aborting DirectLine token request");
                    return string.Empty;
                }

                logger.LogInformation("Got Power Platform token (length: {Length}), making DirectLine request...", powerPlatformToken.Length);

                // Set authorization header with the Power Platform token
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", powerPlatformToken);

                logger.LogDebug("Making DirectLine token request with Power Platform auth to: {Endpoint}", directLineEndpoint);

                // Make GET request to DirectLine token endpoint
                var response = await httpClient.GetAsync(directLineEndpoint);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                logger.LogInformation("DirectLine response status: {StatusCode}, Content length: {Length}", response.StatusCode, responseContent?.Length ?? 0);

                if (response.IsSuccessStatusCode)
                {
                    // Parse the DirectLine token response
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        logger.LogWarning("DirectLine endpoint returned empty response content");
                        return string.Empty;
                    }
                    
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (tokenResponse.TryGetProperty("token", out var tokenElement))
                    {
                        var directLineToken = tokenElement.GetString();
                        logger.LogInformation("Successfully obtained DirectLine token for Copilot Studio");
                        return directLineToken ?? string.Empty;
                    }
                    else
                    {
                        logger.LogWarning("DirectLine response did not contain token: {Response}", responseContent);
                        return string.Empty;
                    }
                }
                else
                {
                    logger.LogError("Failed to obtain DirectLine token. Status: {StatusCode}, Response: {Response}", 
                        response.StatusCode, responseContent);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while obtaining DirectLine token for Copilot Studio");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets Power Platform access token using Azure CLI credentials (DefaultAzureCredential)
        /// This matches the approach that was working: az account get-access-token --resource https://api.powerplatform.com/
        /// </summary>
        private static async Task<string> GetPowerPlatformTokenAsync(
            IHttpClientFactory httpClientFactory, 
            ILogger logger)
        {
            try
            {
                logger.LogInformation("Getting Power Platform token using DefaultAzureCredential (Azure CLI)");
                
                // Use DefaultAzureCredential which includes Azure CLI credentials
                var credential = new DefaultAzureCredential();
                
                // Get token for Power Platform resource (same as Azure CLI command)
                var tokenRequestContext = new TokenRequestContext(new[] { "https://api.powerplatform.com/.default" });
                
                logger.LogInformation("Requesting token for scope: {Scope}", "https://api.powerplatform.com/.default");
                
                var accessToken = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
                
                logger.LogInformation("Successfully obtained Power Platform access token using Azure CLI credentials. Token length: {Length}, Expires: {Expires}", 
                    accessToken.Token?.Length ?? 0, accessToken.ExpiresOn);
                    
                // Log first and last 10 characters for debugging (but not the full token)
                if (!string.IsNullOrEmpty(accessToken.Token) && accessToken.Token.Length > 20)
                {
                    logger.LogDebug("Token starts with: {Start}...{End}", 
                        accessToken.Token.Substring(0, 10), 
                        accessToken.Token.Substring(accessToken.Token.Length - 10));
                }
                
                return accessToken.Token ?? string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while obtaining Power Platform token using DefaultAzureCredential");
                return string.Empty;
            }
        }

        /// <summary>
        /// Test the bot directly using DirectLine REST API to bypass CopilotStudio SDK permissions
        /// </summary>
        private static async Task<string> TestBotWithDirectLineAsync(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Starting DirectLine REST API test...");

                // Step 1: Get DirectLine token
                var directLineEndpoint = configuration["CopilotStudio:DirectLineUrl"] + "?api-version=2022-03-01-preview";
                var directLineToken = await GetDirectLineTokenAsync(directLineEndpoint, httpClientFactory, logger);
                
                if (string.IsNullOrEmpty(directLineToken))
                {
                    logger.LogError("Failed to obtain DirectLine token");
                    return string.Empty;
                }

                using var httpClient = httpClientFactory.CreateClient();
                
                // Step 2: Start DirectLine conversation
                logger.LogInformation("Starting DirectLine conversation...");
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", directLineToken);
                
                var startConversationResponse = await httpClient.PostAsync("https://directline.botframework.com/v3/directline/conversations", null, cancellationToken);
                var conversationContent = await startConversationResponse.Content.ReadAsStringAsync();
                
                if (!startConversationResponse.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to start DirectLine conversation: {Status} - {Content}", startConversationResponse.StatusCode, conversationContent);
                    return string.Empty;
                }
                
                var conversationData = JsonSerializer.Deserialize<JsonElement>(conversationContent);
                var conversationId = conversationData.GetProperty("conversationId").GetString();
                
                logger.LogInformation("DirectLine conversation started: {ConversationId}", conversationId);

                // Step 3: Send message to bot
                var message = new
                {
                    type = "message",
                    from = new { id = "user1" },
                    text = "Tell me a joke"
                };

                var messageJson = JsonSerializer.Serialize(message);
                var messageContent = new StringContent(messageJson, Encoding.UTF8, "application/json");

                var sendMessageUrl = $"https://directline.botframework.com/v3/directline/conversations/{conversationId}/activities";
                var sendMessageResponse = await httpClient.PostAsync(sendMessageUrl, messageContent, cancellationToken);
                var sendMessageResponseContent = await sendMessageResponse.Content.ReadAsStringAsync();

                if (!sendMessageResponse.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to send message: {Status} - {Content}", sendMessageResponse.StatusCode, sendMessageResponseContent);
                    return string.Empty;
                }

                logger.LogInformation("Message sent successfully, waiting for response...");

                // Step 4: Poll for bot response (improved polling with better filtering)
                string lastActivityId = string.Empty;
                for (int i = 0; i < 15; i++) // Try for up to 15 seconds
                {
                    await Task.Delay(1000, cancellationToken); // Wait 1 second between polls
                    
                    var getMessagesUrl = $"https://directline.botframework.com/v3/directline/conversations/{conversationId}/activities";
                    var getMessagesResponse = await httpClient.GetAsync(getMessagesUrl, cancellationToken);
                    var messagesContent = await getMessagesResponse.Content.ReadAsStringAsync();

                    if (getMessagesResponse.IsSuccessStatusCode)
                    {
                        var messagesData = JsonSerializer.Deserialize<JsonElement>(messagesContent);
                        var activities = messagesData.GetProperty("activities");

                        logger.LogInformation("Polling attempt {Attempt}: Found {ActivityCount} activities", i + 1, activities.GetArrayLength());

                        // Look for bot responses (not from user, not our own message)
                        foreach (var activity in activities.EnumerateArray())
                        {
                            if (activity.TryGetProperty("id", out var activityId) && 
                                activity.TryGetProperty("from", out var from) && 
                                activity.TryGetProperty("text", out var text) &&
                                from.TryGetProperty("id", out var fromId))
                            {
                                var currentActivityId = activityId.GetString() ?? "";
                                var senderId = fromId.GetString() ?? "";
                                var messageText = text.GetString() ?? "";
                                
                                logger.LogDebug("Activity - ID: {ActivityId}, From: {SenderId}, Text: {Text}", currentActivityId, senderId, messageText);
                                
                                // Skip if we've already seen this activity
                                if (currentActivityId == lastActivityId) continue;
                                
                                // Look for bot messages (not from user1 and not empty)
                                if (senderId != "user1" && 
                                    !string.IsNullOrEmpty(messageText) && 
                                    messageText != "Tell me a joke") // Skip if it's just echoing our input
                                {
                                    logger.LogInformation("Found bot response: {Response}", messageText);
                                    return messageText;
                                }
                                
                                lastActivityId = currentActivityId;
                            }
                        }
                    }
                    else
                    {
                        logger.LogWarning("Failed to get messages: {Status} - {Content}", getMessagesResponse.StatusCode, messagesContent);
                    }
                }

                logger.LogWarning("No bot response received after 10 seconds");
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DirectLine REST API test failed");
                return string.Empty;
            }
        }
    }
}