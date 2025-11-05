using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Foundry.Agents.Agents.CopilotStudio;
using System.Text.Json;

// NOTE: This example needs to be updated for the new static CopilotStudioAgent factory class.
// Temporarily disabled while the CopilotStudio integration is being refactored.

/*

namespace Foundry.Agents.Examples
{
    /// <summary>
    /// Example demonstrating how to use the Copilot Studio agent independently
    /// </summary>
    public class CopilotStudioExample
    {
        public static async Task RunExampleAsync(IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("Starting Copilot Studio agent example...");

            try
            {
                // Validate existing Copilot Studio bot configuration
                var isValid = await CopilotStudioAgent.ValidateExistingCopilotStudioBotAsync(
                    configuration: configuration, 
                    logger: logger);

                if (!isValid)
                {
                    logger.LogError("No valid Copilot Studio bot configuration found. Please ensure the bot is properly configured.");
                    return;
                }

                logger.LogInformation("Successfully validated Copilot Studio bot configuration");
                
                // Create the Copilot agent using the static factory method
                var copilotAgent = await CopilotStudioAgent.GetCopilotAgent(configuration, logger);
                
                if (copilotAgent == null)
                {
                    logger.LogError("Failed to create CopilotStudio agent");
                    return;
                }

                // Example 1: Simple conversation
                var simpleMessage = new
                {
                    message = "Hello! Can you help me understand energy consumption patterns?",
                    context = new
                    {
                        conversation_id = Guid.NewGuid().ToString(),
                        user_id = "example-user"
                    }
                };

                logger.LogInformation("Sending simple message to Copilot Studio...");
                // Note: Actual conversation handling would be done through the agent framework
                // This is just an example of the expected input format

                // Example 2: Energy analysis context
                var energyContextMessage = new
                {
                    message = "Based on the energy analysis data, can you explain the optimization recommendations?",
                    context = new
                    {
                        zone = "SE3",
                        city = "Stockholm",
                        date = "2025-10-15",
                        previous_agent_output = new
                        {
                            baseline_consumption = 1200.5,
                            optimized_consumption = 980.3,
                            savings_percentage = 18.4,
                            measures = new[]
                            {
                                new { name = "Smart heating", savings = 8.2 },
                                new { name = "LED lighting", savings = 5.1 },
                                new { name = "Insulation upgrade", savings = 5.1 }
                            }
                        },
                        conversation_id = Guid.NewGuid().ToString()
                    }
                };

                logger.LogInformation("Energy context message prepared: {Message}", 
                    JsonSerializer.Serialize(energyContextMessage, new JsonSerializerOptions { WriteIndented = true }));

                logger.LogInformation("Copilot Studio agent example completed successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Copilot Studio example");
            }
        }

        /// <summary>
        /// Example of creating a custom Copilot Studio agent with specific configuration
        /// </summary>
        public static async Task RunCustomAgentExampleAsync(
            IConfiguration configuration, 
            ILogger logger,
            string copilotUrl,
            string tenantId)
        {
            logger.LogInformation("Creating custom Copilot Studio agent...");

            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var typedLogger = loggerFactory.CreateLogger<CopilotStudioAgent>();
                // Get configuration values
                var botUrlConfig = configuration["CopilotStudio:BotUrl"] ?? "https://default-bot-url";
                var tenantIdConfig = configuration["CopilotStudio:TenantId"] ?? "default-tenant";
                
                var copilotStudioService = new CopilotStudioAgent(
                    botUrl: botUrlConfig,
                    tenantId: tenantIdConfig,
                    logger: typedLogger, 
                    configuration: configuration);
                
                // Validate existing Copilot Studio bot configuration
                var isValid = await CopilotStudioAgent.ValidateExistingCopilotStudioBotAsync(
                    configuration: configuration, 
                    logger: typedLogger);

                if (isValid)
                {
                    logger.LogInformation("Successfully validated Copilot Studio bot configuration");
                    
                    // Example of sending a message to the agent
                    var testMessage = "Hello from the energy analysis system. I have some energy data to analyze.";
                    logger.LogInformation("Sending test message: {Message}", testMessage);
                    
                    var response = await copilotStudioService.SendMessageToCopilotStudioAsync(testMessage);
                    if (response != null)
                    {
                        logger.LogInformation("Received response: {Response}", response);
                    }
                }
                else
                {
                    logger.LogError("Failed to validate Copilot Studio bot configuration");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error connecting to Copilot Studio agent");
            }
        }
    }
}
*/