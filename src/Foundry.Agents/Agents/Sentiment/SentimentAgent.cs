using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.AI.Agents.Persistent;
using Microsoft.Agents.AI;
using Foundry.Agents.Agents.Shared;

namespace Foundry.Agents.Agents.Sentiment
{
    public class SentimentAgent
    {
        // Read per-agent instructions via InstructionReader
        public string Instructions => InstructionReader.ReadSection("Sentiment");

        /// <summary>
        /// Ensure a persisted AIAgent exists for SentimentMCP on the given endpoint. Uses
        /// <see cref="AgentCreationHelper"/> to locate an existing agent or create and persist
        /// a new one. Returns the found or created <see cref="AIAgent"/> instance, or null on failure.
        /// </summary>
        public static Task<AIAgent?> GetOrCreateAIAgentAsync(string endpoint, IConfiguration configuration, ILogger logger, IPersistentAgentsClientAdapter? adapter = null, CancellationToken cancellationToken = default)
        {
            // Sentiment agent needs MCP server tool for sentiment analysis
            return AgentCreationHelper.GetOrCreateAsync(endpoint, configuration, logger, "Sentiment", "SentimentMCP", () => InstructionReader.ReadSection("Sentiment"), adapter, new[] { "mcp-sentiment" }, cancellationToken);
        }
    }
}