using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.AI.Agents.Persistent;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // If first argument is "test", run the Foundry sentiment test
        if (args.Length > 0 && args[0].Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            var testArgs = args.Skip(1).ToArray(); // Pass remaining args as test message
            return await RunSentimentTest(testArgs);
        }
        
        try
        {
            var endpoint = Environment.GetEnvironmentVariable("PROJECT_ENDPOINT") ?? "https://persistent-agents-proj-resource.services.ai.azure.com/api/projects/persistent-agents-proj";
            Console.WriteLine($"Using endpoint: {endpoint}");

            var ids = args?.ToList() ?? new System.Collections.Generic.List<string>();

            if (ids.Count == 0)
            {
                // Try to read agent ids from Agents/*/agent-id.txt
                var agentsDir = Path.Combine(Directory.GetCurrentDirectory(), "Agents");
                if (Directory.Exists(agentsDir))
                {
                    foreach (var dir in Directory.GetDirectories(agentsDir))
                    {
                        var p = Path.Combine(dir, "agent-id.txt");
                        if (File.Exists(p))
                        {
                            try { var t = File.ReadAllText(p).Trim(); if (!string.IsNullOrWhiteSpace(t)) ids.Add(t); } catch { }
                        }
                    }
                }
            }

            if (ids.Count == 0)
            {
                Console.WriteLine("No agent ids provided via args or Agents/*/agent-id.txt files. Provide agent ids as args or create agent-id.txt files.");
                return 2;
            }

            var client = new PersistentAgentsClient(endpoint, new DefaultAzureCredential());
            var admin = client.Administration;

            foreach (var id in ids)
            {
                Console.WriteLine($"Querying agent id: {id}");
                try
                {
                    var resp = await admin.GetAgentAsync(id);
                    var agent = resp?.Value;
                    if (agent == null)
                    {
                        Console.WriteLine($"Agent {id} not found (null response)");
                        continue;
                    }
                    try
                    {
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(agent, Newtonsoft.Json.Formatting.Indented);
                        Console.WriteLine("Agent JSON:\n" + json);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Agent returned but failed to serialize: {ex}");
                    }
                }
                catch (Azure.RequestFailedException rf)
                {
                    Console.WriteLine($"RequestFailedException querying {id}: {rf.Status} {rf.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to query agent {id}: {ex}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex);
            return 1;
        }
    }

    static async Task<int> RunSentimentTest(string[] args)
    {
        try
        {
            Console.WriteLine("🚀 Starting SentimentAgent Foundry Test");

            var endpoint = Environment.GetEnvironmentVariable("PROJECT_ENDPOINT") ?? 
                          "https://persistent-agents-proj-resource.services.ai.azure.com/api/projects/persistent-agents-proj";
            
            Console.WriteLine($"📡 Using Foundry endpoint: {endpoint}");

            // Read the agent ID from the file created by our deployment
            var agentIdFile = "Agents/Sentiment/agent-id.txt";
            if (!File.Exists(agentIdFile))
            {
                Console.WriteLine($"❌ Agent ID file not found: {agentIdFile}. Make sure the SentimentAgent has been deployed.");
                return 1;
            }

            var agentId = File.ReadAllText(agentIdFile).Trim();
            Console.WriteLine($"🤖 Using SentimentAgent ID: {agentId}");

            // Create client with Azure authentication
            var credential = new DefaultAzureCredential();
            var client = new PersistentAgentsClient(endpoint, credential);

            // Test message - complex sentiment to trigger MCP server
            var testMessage = args.Length > 0 ? string.Join(" ", args) : 
                "Analyze the complex emotional landscape of this customer feedback: I'm incredibly excited about the innovative new features you've announced - they show real vision and understanding of our needs. However, I'm deeply concerned about the recurring bugs in the current version that are affecting my daily workflow, and I'm honestly disappointed with how long these issues have persisted without resolution. While I appreciate the potential of what's coming, I need stability now.";

            Console.WriteLine($"💬 Test message: {testMessage}");

            // Step 1: Get the agent to verify it exists
            Console.WriteLine("🔍 Getting agent details from Foundry...");
            var agentResponse = await client.Administration.GetAgentAsync(agentId);
            var agent = agentResponse.Value;
            
            if (agent == null)
            {
                Console.WriteLine($"❌ SentimentAgent not found in Foundry with ID: {agentId}");
                return 1;
            }

            Console.WriteLine($"✅ SentimentAgent found in Foundry: {agent.Name} ({agent.Id})");
            Console.WriteLine($"📝 Description: {agent.Description}");
            Console.WriteLine($"🛠️ Tools count: {agent.Tools?.Count ?? 0}");

            // Step 2: Create a conversation thread
            Console.WriteLine("🧵 Creating conversation thread...");
            var threadResponse = await client.Threads.CreateThreadAsync();
            var thread = threadResponse.Value;
            Console.WriteLine($"✅ Thread created: {thread.Id}");

            // Step 3: Add our test message to the thread
            Console.WriteLine("💌 Adding message to thread...");
            var messageContent = BinaryData.FromObjectAsJson(new { role = "user", content = testMessage });
            await client.Messages.CreateMessageAsync(thread.Id, messageContent);
            Console.WriteLine("✅ Message added to thread");

            // Step 4: Run the agent on the thread
            Console.WriteLine("🏃 Running SentimentAgent on the thread...");
            var runResponse = await client.Runs.CreateRunAsync(thread.Id, agent.Id);
            var run = runResponse.Value;
            
            Console.WriteLine($"⚡ Run created: {run.Id}, Initial status: {run.Status}");

            // Step 5: Poll for completion and monitor progress
            var maxAttempts = 30; // 60 seconds max
            var attempts = 0;
            
            while (run.Status == "queued" || run.Status == "in_progress" || run.Status == "requires_action")
            {
                if (attempts >= maxAttempts)
                {
                    Console.WriteLine($"❌ Run timed out after {maxAttempts} attempts ({maxAttempts * 2} seconds)");
                    return 1;
                }

                await Task.Delay(2000); // Wait 2 seconds between polls
                attempts++;
                
                // Get updated run status
                var runUpdateResponse = await client.Runs.GetRunAsync(thread.Id, run.Id);
                run = runUpdateResponse.Value;
                
                Console.WriteLine($"📊 Run status #{attempts}: {run.Status} (elapsed: {attempts * 2}s)");

                // If the run requires action (tool calls), handle MCP tool approvals
                if (run.Status == "requires_action")
                {
                    Console.WriteLine("🔧 Run requires action - checking for tool approvals...");
                    Console.WriteLine($"🔧 RequiredAction type: {run.RequiredAction?.GetType().Name}");
                    
                    if (run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
                    {
                        Console.WriteLine("🎯 MCP Tool approval required - auto-approving...");
                        var toolApprovals = new List<ToolApproval>();
                        
                        foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                        {
                            if (toolCall is RequiredMcpToolCall mcpToolCall)
                            {
                                Console.WriteLine($"✅ Approving MCP tool: {mcpToolCall.Name}");
                                Console.WriteLine($"📋 Arguments: {mcpToolCall.Arguments}");
                                toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                            }
                        }
                        
                        if (toolApprovals.Count > 0)
                        {
                            Console.WriteLine($"🚀 Submitting {toolApprovals.Count} tool approvals...");
                            var submitResponse = await client.Runs.SubmitToolOutputsToRunAsync(thread.Id, run.Id, toolApprovals: toolApprovals);
                            run = submitResponse?.Value ?? run;
                            Console.WriteLine($"✅ Tool approvals submitted, new status: {run.Status}");
                        }
                    }
                }
            }

            // Step 6: Check final status and get results
            Console.WriteLine($"🏁 Run completed with final status: {run.Status}");

            if (run.Status == "completed")
            {
                // Get the conversation messages to see the agent's response
                Console.WriteLine("📥 Retrieving conversation messages...");
                var messages = client.Messages.GetMessages(thread.Id).ToList();

                Console.WriteLine($"💬 Found {messages.Count} messages in conversation");
                
                // Display the agent's response
                foreach (var message in messages)
                {
                    if (message.Role.ToString().Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("🤖 SentimentAgent Response:");
                        foreach (var contentItem in message.ContentItems)
                        {
                            if (contentItem is MessageTextContent textContent)
                            {
                                Console.WriteLine($"📝 {textContent.Text}");
                            }
                        }
                    }
                }
                
                Console.WriteLine("✅ SentimentAgent test completed successfully!");
                Console.WriteLine("💡 Check the logs above to see if MCP tools were called");
                return 0;
            }
            else
            {
                Console.WriteLine($"❌ Run failed with status: {run.Status}");
                if (!string.IsNullOrEmpty(run.LastError?.Message))
                {
                    Console.WriteLine($"💥 Error details: {run.LastError.Message}");
                }
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed with exception: {ex}");
            return 1;
        }
    }
}
