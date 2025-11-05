using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Foundry.Agents.Agents.Sentiment;

namespace Foundry.Agents.TestClient
{
    public class SentimentAgentTest
    {
        public static async Task TestSentimentAgentCreation()
        {
            // Setup configuration and logging
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AzureAI:Endpoint", "https://your-endpoint.cognitiveservices.azure.com" }
                })
                .Build();

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<SentimentAgentTest>();

            try
            {
                // Test creating the sentiment agent
                var endpoint = configuration["AzureAI:Endpoint"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    logger.LogError("AzureAI:Endpoint not configured");
                    return;
                }

                logger.LogInformation("Testing SentimentAgent creation...");
                
                var sentimentAgent = await SentimentAgent.GetOrCreateAIAgentAsync(
                    endpoint, 
                    configuration, 
                    logger);

                if (sentimentAgent != null)
                {
                    logger.LogInformation("✅ SentimentAgent created successfully with ID: {AgentId}", sentimentAgent.Id);
                    logger.LogInformation("📝 Agent Name: {AgentName}", sentimentAgent.Name);
                    logger.LogInformation("� Agent Description: {Description}", sentimentAgent.Description);
                }
                else
                {
                    logger.LogError("❌ Failed to create SentimentAgent");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error testing SentimentAgent: {Message}", ex.Message);
            }
        }
    }
}