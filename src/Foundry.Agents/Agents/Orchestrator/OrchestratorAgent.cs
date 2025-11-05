using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI;
using Newtonsoft.Json;
using System.Text.Json;
using Foundry.Agents.Agents.Shared;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace Foundry.Agents.Agents.Orchestrator
{
    public class OrchestratorAgent
    {
        private readonly ILogger<OrchestratorAgent> _logger;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public OrchestratorAgent(ILogger<OrchestratorAgent> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        // Run the orchestration for a given zone/city/date. Returns the final GlobalEnvelope-like JSON string.
        public async Task<string> RunAsync(string zone, string city, string date, string? userRequest = null)
        {
            _logger.LogInformation("Running Agents");
            // Generate a run id early so handler closures can persist run-scoped diagnostic files
            var runId = Guid.NewGuid().ToString();

            var endpoint = System.Environment.GetEnvironmentVariable("PROJECT_ENDPOINT") ?? 
                          _configuration["Project:Endpoint"] ?? 
                          "http://localhost:3000";
            // Create a PersistentAgentsClient for the provided endpoint. When PROJECT_ENDPOINT is https, DefaultAzureCredential will be used.
            var persistentAgentsClient = new Azure.AI.Agents.Persistent.PersistentAgentsClient(endpoint, new Azure.Identity.DefaultAzureCredential());

            // Simple feature flag system - defaults to full workflow
            // Set FEATURE_FLAG=Sentiment or FEATURE_FLAG=Copilot to use single-agent modes
            var featureFlag = System.Environment.GetEnvironmentVariable("FEATURE_FLAG");
            
            var useSentimentAgent = "Sentiment".Equals(featureFlag, StringComparison.OrdinalIgnoreCase);
            var useCopilotStudioOnly = "Copilot".Equals(featureFlag, StringComparison.OrdinalIgnoreCase);
            var useFullWorkflow = !useSentimentAgent && !useCopilotStudioOnly; // Default behavior
            
            if (useSentimentAgent)
                _logger.LogInformation("🎯 FEATURE_FLAG=Sentiment - Running SentimentAgent only");
            else if (useCopilotStudioOnly) 
                _logger.LogInformation("🤖 FEATURE_FLAG=Copilot - Running CopilotStudio only");
            else
                _logger.LogInformation("� Full workflow mode (default) - Running energy pipeline");
            
            AIAgent? sentimentAIAgent = null;
            Microsoft.Agents.AI.CopilotStudio.CopilotStudioAgent? copilotOnlyAIAgent = null;
            
            if (useSentimentAgent)
            {
                _logger.LogInformation("🎯 SentimentAgent mode detected - initializing ONLY SentimentAgent");
                sentimentAIAgent = await Foundry.Agents.Agents.Sentiment.SentimentAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
                if (sentimentAIAgent == null)
                {
                    _logger.LogError("Failed to create SentimentAgent. Aborting.");
                    return JsonConvert.SerializeObject(new { error = "Failed to create SentimentAgent" });
                }
                else
                {
                    _logger.LogInformation("✅ SentimentAgent created successfully - ready for text analysis");
                }
            }
            else if (useCopilotStudioOnly)
            {
                _logger.LogInformation("🤖 CopilotStudio-only mode detected - initializing ONLY CopilotStudio agent");
                copilotOnlyAIAgent = await Foundry.Agents.Agents.CopilotStudio.CopilotStudioAgent.GetCopilotAgent(_configuration, _logger);
                if (copilotOnlyAIAgent == null)
                {
                    _logger.LogError("Failed to create CopilotStudio agent. Aborting.");
                    return JsonConvert.SerializeObject(new { error = "Failed to create CopilotStudio agent" });
                }
                else
                {
                    _logger.LogInformation("✅ CopilotStudio agent created successfully - ready for conversation");
                }
            }
            
            // Only initialize other agents if NOT in single-agent mode
            AIAgent? remoteDataAIAgent = null;
            AIAgent? energyAIAgent = null;
            AIAgent? emailGeneratorAIAgent = null;
            AIAgent? emailAIAgent = null;
            Microsoft.Agents.AI.CopilotStudio.CopilotStudioAgent? copilotAIAgent = null;
            
            if (useFullWorkflow)
            {
                _logger.LogInformation("🏭 Full workflow mode - initializing all energy pipeline agents");
                
                // Ensure the RemoteData agent exists on the target persistent agents service. Create if missing.
                remoteDataAIAgent = await Foundry.Agents.Agents.RemoteData.RemoteDataAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
                if (remoteDataAIAgent == null)
                {
                    _logger.LogError("Failed to obtain or create RemoteData agent. Aborting orchestration.");
                    return JsonConvert.SerializeObject(new { error = "Failed to obtain RemoteData agent" });
                }

                // Ensure the Energy agent exists (create if necessary) 
                energyAIAgent = await Foundry.Agents.Agents.Energy.EnergyAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
                if (energyAIAgent == null)
                {
                    _logger.LogError("Failed to obtain or create Energy agent. Aborting orchestration.");
                    return JsonConvert.SerializeObject(new { error = "Failed to obtain Energy agent" });
                }            
                emailGeneratorAIAgent = await Foundry.Agents.Agents.EmailGenerator.EmailGeneratorAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
                if (emailGeneratorAIAgent == null)
                {
                    _logger.LogError("Failed to obtain or create EmailGenerator agent. Aborting orchestration.");
                    return JsonConvert.SerializeObject(new { error = "Failed to obtain EmailGenerator agent" });
                }

                emailAIAgent = await Foundry.Agents.Agents.EmailAssistant.EmailAssistantAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
                if (emailAIAgent == null)
                {
                    _logger.LogError("Failed to obtain or create EmailAssistant agent. Aborting orchestration.");
                    return JsonConvert.SerializeObject(new { error = "Failed to obtain EmailAssistant agent" });
                }

                // Optional: Get Copilot Studio agent ONLY if explicitly enabled
                var useCopilotStudio = _configuration.GetValue<bool>("CopilotStudio:Enabled") && 
                                       !string.IsNullOrEmpty(_configuration["CopilotStudio:BotUrl"]);
                
                // Also check environment variable (more explicit control)
                var copilotEnabledEnv = System.Environment.GetEnvironmentVariable("CopilotStudio__Enabled");
                if (!string.IsNullOrEmpty(copilotEnabledEnv) && copilotEnabledEnv.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    useCopilotStudio = false;
                    _logger.LogInformation("🚫 CopilotStudio explicitly disabled via environment variable");
                }
                
                if (useCopilotStudio)
                {
                    _logger.LogInformation("🤖 Initializing CopilotStudio agent...");
                    copilotAIAgent = await Foundry.Agents.Agents.CopilotStudio.CopilotStudioAgent.GetCopilotAgent(_configuration, _logger);
                    if (copilotAIAgent == null)
                    {
                        _logger.LogWarning("Failed to connect to CopilotStudio bot - continuing without it.");
                    }
                    else
                    {
                        _logger.LogInformation("CopilotStudio agent connected successfully - ready for integration");
                    }
                }
                else
                {
                    _logger.LogInformation("⏭️ Skipping CopilotStudio initialization (disabled)");
                }
            }

            // Log initialized agents
            if (useSentimentAgent)
            {
                _logger.LogInformation($"🎯 sentiment agent: {sentimentAIAgent?.DisplayName}");
            }
            else if (useCopilotStudioOnly)
            {
                _logger.LogInformation($"🤖 copilot agent: {copilotOnlyAIAgent?.DisplayName}");
            }
            else
            {
                _logger.LogInformation($"remote data agent: {remoteDataAIAgent?.DisplayName}");
                _logger.LogInformation($"energy agent: {energyAIAgent?.DisplayName}");
                _logger.LogInformation($"email composer agent: {emailGeneratorAIAgent?.DisplayName}");
                _logger.LogInformation($"email assistant agent: {emailAIAgent?.DisplayName}");
                if (copilotAIAgent != null)
                {
                    _logger.LogInformation($"copilot agent: {copilotAIAgent.DisplayName}");
                }
            }

            // Execute the workflow using the streaming API.
            // Capture the last agent update data into resultJson and return it.
            string? resultJson = null;
          
            bool emailRequested = false;
            var emailRecipients = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(userRequest))
            {
                try
                {
                    // crude email detection: look for '@' tokens and simple regex matches
                    var m = System.Text.RegularExpressions.Regex.Matches(userRequest, "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}");
                    foreach (System.Text.RegularExpressions.Match mm in m)
                    {
                        if (!string.IsNullOrWhiteSpace(mm.Value)) emailRecipients.Add(mm.Value);
                    }

                    if (emailRecipients.Count > 0 || userRequest.IndexOf("email", StringComparison.OrdinalIgnoreCase) >= 0 || userRequest.IndexOf("send", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        emailRequested = true;
                    }
                }
                catch { }
            }

            // Create different prompts based on workflow mode
            string runPrompt;
            string sentimentText = ""; // Make available for later reference
            if (useSentimentAgent)
            {
                // For sentiment analysis, use a simple text prompt instead of complex energy metadata
                sentimentText = "I love sunny days and beautiful weather! The forecast looks amazing for this weekend.";
                runPrompt = sentimentText;
                _logger.LogInformation($"Running SentimentAgent with text: {sentimentText}");
            }
            else if (useCopilotStudioOnly)
            {
                // For CopilotStudio-only mode, use a conversational prompt
                var copilotText = "Hello! Can you help me understand how CopilotStudio agents work? I'm testing the integration.";
                runPrompt = copilotText;
                _logger.LogInformation($"Running CopilotStudio with text: {copilotText}");
            }
            else
            {
                // For full workflow, use structured energy analysis payload
                var payload = new
                {
                    task_id = "remote-phase-1",
                    zone = zone,
                    city = city,
                    date = date,
                    user_request = userRequest ?? string.Empty,
                    email_requested = emailRequested && emailAIAgent != null,
                    email_recipients = emailRecipients.ToArray()
                };
                runPrompt = JsonConvert.SerializeObject(payload);
                _logger.LogInformation($"Running energy workflow with prompt: {runPrompt}");
            }

            // Build a list of executors conditionally: 
            // If useSentimentAgent is true, ONLY run SentimentAgent (standalone sentiment analysis)
            // Otherwise run the normal pipeline: RemoteData -> Energy (-> CopilotStudio if enabled)
            // When email is requested, add EmailGenerator -> EmailAssistant to the pipeline
            var executors = new System.Collections.Generic.List<AIAgent>();
            
            if (useSentimentAgent && sentimentAIAgent != null)
            {
                // ONLY run SentimentAgent when specifically enabled - standalone sentiment analysis workflow
                _logger.LogInformation("Running ONLY SentimentAgent workflow for dedicated text analysis");
                
                // For sentiment-only mode, use direct thread-based approach like TestClient for proper response capture
                return await RunSentimentAgentDirectly(sentimentAIAgent, sentimentText);
            }
            else if (useCopilotStudioOnly && copilotOnlyAIAgent != null)
            {
                // Check if this is a test URL to avoid runtime exceptions
                var copilotUrl = _configuration["CopilotStudio:BotUrl"] ?? string.Empty;
                var isTestUrl = copilotUrl.Contains("test-copilot-bot") || copilotUrl.Contains("localhost") || copilotUrl.Contains("example.com");
                
                if (isTestUrl)
                {
                    _logger.LogInformation("🧪 CopilotStudio test URL detected - workflow validation successful but skipping execution to avoid connection errors");
                    _logger.LogInformation("✅ CopilotStudio-only mode is properly configured and would work with a real bot URL");
                    return JsonConvert.SerializeObject(new { 
                        mode = "copilot-only", 
                        status = "validation-success", 
                        message = "CopilotStudio-only workflow is properly configured. Replace with real bot URL for actual execution.",
                        test_url = copilotUrl 
                    });
                }
                else
                {
                    // ONLY run CopilotStudio agent when specifically enabled - standalone conversation workflow
                    _logger.LogInformation("Running ONLY CopilotStudio workflow for conversation testing");
                    executors.Add(copilotOnlyAIAgent);
                }
            }
            else if (remoteDataAIAgent != null && energyAIAgent != null)
            {
                // Normal workflow: RemoteData -> Energy -> (optional agents)
                _logger.LogInformation("Running standard energy analysis workflow");
                
                // Add CopilotStudio agent if enabled and properly configured
                // Note: Requires Azure AD permissions: CopilotStudio.Copilots.Invoke, All.All.ReadWrite
                // Temporarily enabled for testing (will fail with permissions error if not properly configured)
                if (copilotAIAgent != null)
                {
                    _logger.LogInformation("Adding CopilotStudio agent to workflow (requires proper Azure AD permissions)");
                    executors.Add(copilotAIAgent);
                }
                else if (copilotAIAgent != null)
                {
                    _logger.LogInformation("CopilotStudio agent created successfully but not added to workflow until Azure AD permissions are configured");
                    _logger.LogInformation("Required permissions: CopilotStudio.Copilots.Invoke, All.All.ReadWrite");
                }
                
                executors.Add(remoteDataAIAgent);
                executors.Add(energyAIAgent);
                
                // Add email workflow if requested
                if (emailRequested && emailGeneratorAIAgent != null && emailAIAgent != null)
                {
                    executors.Add(emailGeneratorAIAgent);
                    executors.Add(emailAIAgent);
                }
            }
            else
            {
                _logger.LogError("No valid agents initialized for execution");
                return JsonConvert.SerializeObject(new { error = "No valid agents available for execution" });
            }

            // Defensive check: ensure no two executors resolved to the same underlying AIAgent.Id
            var idGroups = executors.GroupBy(a => a.Id).Where(g => g.Count() > 1).ToList();
            if (idGroups.Count > 0)
            {
                foreach (var g in idGroups)
                {
                    _logger.LogError("Detected multiple executors referencing the same persisted assistant id {AssistantId}: {Executors}", g.Key, string.Join(',', g.Select(x => x.DisplayName ?? x.Id)));
                }
                return JsonConvert.SerializeObject(new { error = "Duplicate persisted assistant ids detected among executors; aborting orchestration." });
            }

            // Use the convenience builder which wires a sequential agent pipeline and the TurnToken/Output executor correctly.
            var workflow = AgentWorkflowBuilder.BuildSequential(executors.ToArray());
            _logger.LogInformation("Workflow initialized");

            // Demo: host-driven cancellation path. Set USE_RUN_CANCEL=1 to exercise RunAsync with a CancellationTokenSource.
            try
            {
                var env = System.Environment.GetEnvironmentVariable("USE_RUN_CANCEL");
                if (!string.IsNullOrWhiteSpace(env) && (env == "1" || env.Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("USE_RUN_CANCEL enabled - running workflow with RunAsync and host cancellation demo.");
                    var cts = new System.Threading.CancellationTokenSource();
                    // cancel after 3s to demonstrate cooperative cancellation
                    _ = System.Threading.Tasks.Task.Run(async () => { await System.Threading.Tasks.Task.Delay(3000).ConfigureAwait(false); try { cts.Cancel(); } catch { } });

                    try
                    {
                        var inputObj = JsonConvert.DeserializeObject(runPrompt) ?? new { };
                        var runResult = await InProcessExecution.RunAsync(workflow, inputObj, runId, cts.Token).ConfigureAwait(false);
                        var serialized = JsonConvert.SerializeObject(runResult);
                        return serialized;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("RunAsync was cancelled by host CancellationTokenSource.");
                        return JsonConvert.SerializeObject(new { runId = runId, status = "cancelled" });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "RunAsync threw an exception");
                        return JsonConvert.SerializeObject(new { runId = runId, error = ex.Message });
                    }
                }
            }
            catch { }

            // Use a single ChatMessage so the underlying client sends one content item (avoids content array splitting)
            _logger.LogInformation("🚀 Starting workflow execution...");
            var run = await InProcessExecution.StreamAsync(workflow, new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, runPrompt));

            // Must send the turn token to trigger the agents.
            // The agents are wrapped as executors. When they receive messages,
            // they will cache the messages and only start processing when they receive a TurnToken.
            // Send the TurnToken (emit events) to kick the workflow into executing the agent runs.
            _logger.LogInformation("📤 Sending TurnToken to start agent execution...");
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

            string? lastExecutorId = null;
            string? sentimentResult = null; // To capture sentiment analysis output
            // Guard to ensure we inject the normalized EmailGenerator payload only once
            bool emailAssistantPayloadInjected = false;

            _logger.LogInformation("👁️ Starting workflow monitoring...");
            
            // Add timeout to prevent hanging
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // 5 minute timeout
            var combinedToken = CancellationToken.None;
            
            try
            {
                await foreach (WorkflowEvent evt in run.WatchStreamAsync().WithCancellation(timeoutCts.Token).ConfigureAwait(false))
                {
                bool shouldBreak = false;
                switch (evt)
                {
                    case AgentRunUpdateEvent e:
                        try
                        {
                            if (e.ExecutorId != lastExecutorId)
                            {
                                lastExecutorId = e.ExecutorId;
                                _logger.LogInformation($"{e.ExecutorId}");
                            }

                            var upd = e.GetType().GetProperty("Update")?.GetValue(e);
                            var text = upd?.GetType().GetProperty("Text")?.GetValue(upd)?.ToString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (useSentimentAgent)
                                {
                                    // For sentiment workflow, capture and log the actual agent output
                                    sentimentResult = text;
                                    _logger.LogInformation("🎯 SentimentAgent output: {Text}", text);
                                }
                                else if (useCopilotStudioOnly)
                                {
                                    // For CopilotStudio workflow, log the actual agent output directly
                                    _logger.LogInformation("🤖 CopilotStudio output: {Text}", text);
                                }
                                ConsoleWriteSafe(SanitizeForConsole(text));
                            }
                            else if (useSentimentAgent)
                            {
                                // Debug: log when we get updates but no text for sentiment mode
                                _logger.LogInformation("🔍 AgentRunUpdateEvent with no text content in sentiment mode");
                            }
                        }
                        catch { }

                        try
                        {
                            var upd = e.GetType().GetProperty("Update")?.GetValue(e);
                            var contents = upd?.GetType().GetProperty("Contents")?.GetValue(upd) as System.Collections.IEnumerable;
                            if (contents != null)
                            {
                                foreach (var item in contents)
                                {
                                    var typeName = item?.GetType().Name ?? string.Empty;
                                    if (typeName.IndexOf("FunctionCall", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        var name = item?.GetType().GetProperty("Name")?.GetValue(item)?.ToString() ?? "<fn>";
                                        var args = item?.GetType().GetProperty("Arguments")?.GetValue(item);
                                        _logger.LogInformation($"  [Calling function '{name}' with arguments: {System.Text.Json.JsonSerializer.Serialize(args)}]");
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                        break;

                    case WorkflowOutputEvent output:
                        // Capture final output into resultJson for later processing.
                        try
                        {
                            if (output.Data != null)
                            {
                                resultJson = SerializeData(output.Data);
                                if (useSentimentAgent)
                                {
                                    _logger.LogInformation("🔍 WorkflowOutputEvent data for sentiment: {Data}", resultJson);
                                }
                            }
                            else
                            {
                                resultJson = null;
                            }
                        }
                        catch { resultJson = SerializeData(output.Data); }

                        // If the last executor to produce updates was the EmailGenerator,
                        // run the Transformator and inject the normalized payload into the
                        // existing streaming run so the EmailAssistant (if included in the
                        // main executors list) can process it in the same run/thread.
                        try
                        {
                            // Ensure we only trigger for generator output and when sender is available
                            if (!string.IsNullOrEmpty(lastExecutorId) && emailGeneratorAIAgent != null && emailAIAgent != null && lastExecutorId == emailGeneratorAIAgent.Id)
                            {
                                if (!string.IsNullOrWhiteSpace(resultJson))
                                {
                                    using var parsed = System.Text.Json.JsonDocument.Parse(resultJson);
                                    var normalized = Transformator.NormalizeEnvelope(parsed.RootElement, _configuration, _logger);


                                    var normalizedText = normalized.GetRawText();

                                    // Inject the normalized payload into the existing main run so the
                                    // EmailAssistant processes it inline as part of the same workflow.
                                    if (!emailAssistantPayloadInjected)
                                    {
                                        await run.TrySendMessageAsync(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, normalizedText)).ConfigureAwait(false);
                                        await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);
                                        emailAssistantPayloadInjected = true;
                                    }

                                    // Continue watching the same run to capture assistant updates.
                                    continue;
                                }
                            }

                            // Check if we have more agents in the pipeline before breaking
                            // Only break if this is the final agent in our executor list
                            var currentAgentIndex = executors.FindIndex(a => a.Id == lastExecutorId);
                            var isLastAgent = currentAgentIndex >= 0 && currentAgentIndex == executors.Count - 1;
                            
                            if (isLastAgent)
                            {
                                _logger.LogInformation("🏁 Final agent completed - stopping workflow");
                                shouldBreak = true;
                            }
                            else
                            {
                                _logger.LogInformation($"✅ Agent {lastExecutorId} completed - continuing to next agent in pipeline");
                                // Don't break - let the workflow continue to the next agent
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to run Transformator + EmailAssistant inline after generator output");
                            shouldBreak = true;
                        }

                        break;

                    default:
                        // Unhandled event types are ignored by default; keep watching.
                        break;
                }

                if (shouldBreak) break; // observed final output; stop streaming
            }

            _logger.LogInformation("WatchStreamAsync enumeration completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("⏰ Workflow execution timed out after 5 minutes");
                return JsonConvert.SerializeObject(new { runId = runId, error = "Workflow execution timeout", status = "timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during workflow execution");
                return JsonConvert.SerializeObject(new { runId = runId, error = ex.Message, status = "error" });
            }
            
            // Only do energy-specific processing for full workflow mode
            if (!useSentimentAgent && !useCopilotStudioOnly)
            {
                if (resultJson != null)
                {
                    SaveEnergyOutputAndPlot(resultJson);
                }
                else
                {
                    _logger.LogWarning("Failed to save/plot energy output from orchestrator");
                }
            }
            else if (useSentimentAgent)
            {
                // For sentiment workflow, use the captured streaming result or workflow output
                if (!string.IsNullOrWhiteSpace(sentimentResult))
                {
                    resultJson = sentimentResult; // Use the actual sentiment analysis output from streaming
                    _logger.LogInformation("🎯 SentimentAgent result (from streaming): {Result}", resultJson);
                }
                else if (!string.IsNullOrWhiteSpace(resultJson))
                {
                    // Use workflow output but check if it's valid sentiment analysis or just echo
                    _logger.LogInformation("🎯 SentimentAgent result (from workflow output): {Result}", resultJson);
                    
                    // If the result is just echoing back input, this indicates the agent didn't perform analysis
                    if (resultJson.Contains(sentimentText) && !resultJson.Contains("sentiment"))
                    {
                        _logger.LogWarning("⚠️ SentimentAgent appears to be echoing input instead of performing analysis. Check agent configuration and MCP tool usage.");
                    }
                }
                else
                {
                    _logger.LogInformation("🎯 SentimentAgent completed but no output captured");
                }
            }
            else if (useCopilotStudioOnly)
            {
                // For CopilotStudio workflow, just log the result
                if (!string.IsNullOrWhiteSpace(resultJson))
                {
                    _logger.LogInformation("🤖 CopilotStudio result: {Result}", resultJson);
                }
                else
                {
                    _logger.LogInformation("🤖 CopilotStudio completed but no output captured");
                }
            }

            // Ensure we always return a JSON string. If the executor emitted JSON already return it; otherwise wrap it.
            if (string.IsNullOrWhiteSpace(resultJson))
            {
                var fallback = new { runId = runId, message = "no executor output captured" };
                return JsonConvert.SerializeObject(fallback);
            }

            var trimmedResult = resultJson.TrimStart();
            if (trimmedResult.StartsWith("{") || trimmedResult.StartsWith("["))
            {
                return resultJson;
            }

            return JsonConvert.SerializeObject(new { runId = runId, result = resultJson });
        }

        /// <summary>
        /// Save the Energy GlobalEnvelope JSON to docs/last_agent_output.json (pretty printed)
        /// and invoke the plotting script to create a timestamped PNG. This helper isolates
        /// filesystem and process interactions so the message loop remains easy to read.
        /// </summary>
        private void SaveEnergyOutputAndPlot(string jsonText)
        {
            try
            {
                string cleaned = jsonText ?? string.Empty;
                // If the captured content is an array of strings, try to extract the likely energy payload
                try
                {
                    var arr = JsonConvert.DeserializeObject<string[]>(cleaned);
                    if (arr != null && arr.Length > 0)
                    {
                        // prefer last item as final
                        cleaned = arr[arr.Length - 1] ?? cleaned;
                    }
                }
                catch { }

                try
                {
                    var sanitized = SanitizeJsonText(cleaned) ?? string.Empty;
                    string? formatted = null;
                    bool parsedOk = false;

                    // First attempt: direct parse with System.Text.Json
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(sanitized);
                        formatted = System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        parsedOk = true;
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogDebug(parseEx, "Direct JSON parse failed for Energy output; will attempt substring extraction");
                    }

                    // Second attempt: try to find a balanced JSON object/array substring (first '{' or '[' to matching brace)
                    if (!parsedOk)
                    {
                        try
                        {
                            var s = sanitized;
                            int start = s.IndexOf('{');
                            if (start < 0) start = s.IndexOf('[');
                            if (start >= 0)
                            {
                                int depth = 0;
                                char open = s[start];
                                char close = open == '{' ? '}' : ']';
                                int end = -1;
                                for (int i = start; i < s.Length; i++)
                                {
                                    var c = s[i];
                                    if (c == open) depth++;
                                    else if (c == close) depth--;
                                    if (depth == 0)
                                    {
                                        end = i;
                                        break;
                                    }
                                }

                                if (end > start)
                                {
                                    var candidate = s.Substring(start, end - start + 1);
                                    try
                                    {
                                        var doc2 = System.Text.Json.JsonDocument.Parse(candidate);
                                        formatted = System.Text.Json.JsonSerializer.Serialize(doc2.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                                        parsedOk = true;
                                    }
                                    catch (Exception ex2)
                                    {
                                        _logger.LogDebug(ex2, "Substring JSON parse failed for candidate payload");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed during JSON substring extraction attempt");
                        }
                    }

                    var outDir = System.IO.Path.Combine("docs");
                    System.IO.Directory.CreateDirectory(outDir);
                    var outPath = System.IO.Path.Combine(outDir, "last_agent_output.json");

                    if (parsedOk && formatted != null)
                    {
                        System.IO.File.WriteAllText(outPath, formatted);
                        _logger.LogInformation("Saved Energy GlobalEnvelope (pretty JSON) to {Path}", outPath);

                        // Run the plotting script and capture output for demo exposition
                        var scriptPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "docs", "energy_measures_plot.py"));
                        var jsonPath = System.IO.Path.GetFullPath(outPath);

                        if (!System.IO.File.Exists(scriptPath))
                        {
                            _logger.LogWarning("Plot script not found at {ScriptPath}. Skipping plot. Current directory: {Cwd}", scriptPath, System.IO.Directory.GetCurrentDirectory());
                            return;
                        }

                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "python",
                            Arguments = $"\"{scriptPath}\" \"{jsonPath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null)
                        {
                            var stdout = proc.StandardOutput.ReadToEnd();
                            var stderr = proc.StandardError.ReadToEnd();
                            proc.WaitForExit(60000);

                            if (!string.IsNullOrWhiteSpace(stdout)) _logger.LogInformation("Plot script output:\n{Stdout}", stdout);
                            if (!string.IsNullOrWhiteSpace(stderr)) _logger.LogWarning("Plot script errors:\n{Stderr}", stderr);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to start plot script process (Process.Start returned null)");
                        }
                    }
                    else
                    {
                        // Could not parse JSON robustly. Save a wrapped raw output for debugging and skip plotting.
                        var wrapped = Newtonsoft.Json.JsonConvert.SerializeObject(new { raw = sanitized });
                        System.IO.File.WriteAllText(outPath, wrapped);
                        _logger.LogWarning("Failed to parse Energy JSON payload; saved raw cleaned output to {Path} for inspection and skipped plotting", outPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error while saving or plotting Energy output");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error in SaveEnergyOutputAndPlot");
            }
        }

        // Remove common Markdown code fences and surrounding backticks so JSON can be parsed robustly.
        public static string SanitizeJsonText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? string.Empty;
            var txt = raw.Trim();

            try
            {
                // If the assistant returned a fenced block like ```json\n{...}\n```
                var firstFence = txt.IndexOf("```");
                if (firstFence >= 0)
                {
                    var startLineEnd = txt.IndexOf('\n', firstFence);
                    if (startLineEnd >= 0)
                    {
                        var lastFence = txt.LastIndexOf("```");
                        if (lastFence > startLineEnd)
                        {
                            var inner = txt.Substring(startLineEnd + 1, lastFence - (startLineEnd + 1));
                            return inner.Trim();
                        }
                    }
                }

                // If it's wrapped with single backticks around whole value: `...`
                if (txt.Length >= 2 && txt[0] == '`' && txt[txt.Length - 1] == '`')
                {
                    return txt.Trim('`').Trim();
                }

                // Otherwise return trimmed text
                return txt;
            }
            catch
            {
                return txt;
            }
        }

        // Moved from RunAsync: serialize runtime 'Data' objects into a JSON string robustly
        // Thread-safe console write helper for streaming text without newline
        private static readonly object _consoleWriteLock = new object();
        private static void ConsoleWriteSafe(string text)
        {
            try
            {
                lock (_consoleWriteLock)
                {
                    System.Console.Write(text);
                    try { System.Console.Out.Flush(); } catch { }
                }
            }
            catch { }
        }

        private static string SerializeData(object? dataVal)
        {
            if (dataVal == null) return string.Empty;
            if (dataVal is string ss) return ss;

            // If it's an IEnumerable, extract item fields reflectively (e.g., ChatMessage has 'Role' and 'Content')
            if (dataVal is System.Collections.IEnumerable enumerable)
            {
                var list = new System.Collections.Generic.List<object?>();
                foreach (var item in enumerable)
                {
                    if (item == null) { list.Add(null); continue; }

                    if (item is string sItem) { list.Add(sItem); continue; }

                    var itType = item.GetType();
                    var contentProp = itType.GetProperty("Content");
                    var roleProp = itType.GetProperty("Role");
                    if (contentProp != null)
                    {
                        var contentVal = contentProp.GetValue(item)?.ToString();
                        var roleVal = roleProp?.GetValue(item)?.ToString();
                        list.Add(new { role = roleVal, content = contentVal });
                        continue;
                    }

                    // Fallback: use ToString()
                    list.Add(item?.ToString());
                }

                try { return JsonConvert.SerializeObject(list); }
                catch { return string.Join("\n", System.Linq.Enumerable.Select(list, x => x?.ToString() ?? string.Empty)); }
            }

            // Fallback: try to serialize the object
            try { return JsonConvert.SerializeObject(dataVal); }
            catch { return dataVal.ToString() ?? string.Empty; }
        }

        // Truncate very large attachment/base64 blobs and long texts for console output.
        // If the JSON contains a field named "content_base64" or "content", replace it with a short placeholder.
        private static string SanitizeForConsole(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            try
            {
                // If it looks like JSON, try to parse and sanitize attachments
                var s = input.Trim();
                if ((s.StartsWith("{") && s.EndsWith("}")) || (s.StartsWith("[") && s.EndsWith("]")))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(s);
                        var root = doc.RootElement;
                        // Walk and sanitize
                        string SanitizedElement(System.Text.Json.JsonElement el)
                        {
                            switch (el.ValueKind)
                            {
                                case System.Text.Json.JsonValueKind.Object:
                                    var props = new System.Collections.Generic.List<string>();
                                    foreach (var p in el.EnumerateObject())
                                    {
                                        if (string.Equals(p.Name, "content_base64", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, "content", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var placeholder = "<attachment: (content suppressed)>";
                                            props.Add($"\"{p.Name}\": \"{placeholder}\"");
                                            continue;
                                        }
                                        var v = SanitizedElement(p.Value);
                                        props.Add($"\"{p.Name}\": {v}");
                                    }
                                    return "{" + string.Join(", ", props) + "}";

                                case System.Text.Json.JsonValueKind.Array:
                                    var items = new System.Collections.Generic.List<string>();
                                    foreach (var it in el.EnumerateArray()) items.Add(SanitizedElement(it));
                                    return "[" + string.Join(", ", items) + "]";

                                case System.Text.Json.JsonValueKind.String:
                                    var txt = el.GetString() ?? string.Empty;
                                    if (txt.Length > 200) return "\"" + txt.Substring(0, 200) + "... (truncated)\"";
                                    return JsonConvert.SerializeObject(txt);

                                default:
                                    return el.ToString() ?? string.Empty;
                            }
                        }

                        var sanitized = SanitizedElement(root);
                        // Keep overall length bounded
                        if (sanitized.Length > 2000) sanitized = sanitized.Substring(0, 2000) + "... (truncated)";
                        return sanitized;
                    }
                    catch { /* fallthrough to basic truncation */ }
                }

                // Not JSON or parse failed: truncate long raw text
                if (input.Length > 2000) return input.Substring(0, 2000) + "... (truncated)";
                if (input.Length > 500) return input.Substring(0, 500) + "... (truncated)";
                return input;
            }
            catch { return input.Length > 1000 ? input.Substring(0, 1000) + "..." : input; }
        }

        /// <summary>
        /// Run SentimentAgent directly using thread-based approach similar to TestClient
        /// to ensure proper response capture instead of relying on workflow streaming events.
        /// </summary>
        private async Task<string> RunSentimentAgentDirectly(AIAgent sentimentAgent, string inputText, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("🚀 Running SentimentAgent directly via PersistentAgentsClient");
                
                var endpoint = _configuration["PROJECT_ENDPOINT"] ?? 
                              _configuration["Project:Endpoint"] ?? 
                              Environment.GetEnvironmentVariable("PROJECT_ENDPOINT") ??
                              throw new InvalidOperationException("Project endpoint not configured");
                var client = new PersistentAgentsClient(endpoint, new DefaultAzureCredential());

                // Step 1: Create a conversation thread
                _logger.LogInformation("🧵 Creating conversation thread for sentiment analysis");
                var threadResponse = await client.Threads.CreateThreadAsync();
                var thread = threadResponse.Value;
                _logger.LogInformation("✅ Thread created: {ThreadId}", thread.Id);

                // Step 2: Add our input message to the thread
                _logger.LogInformation("💌 Adding message to thread: {InputText}", inputText);
                var messageContent = BinaryData.FromObjectAsJson(new { role = "user", content = inputText });
                await client.Messages.CreateMessageAsync(thread.Id, messageContent);

                // Step 3: Run the sentiment agent on the thread
                _logger.LogInformation("🏃 Running SentimentAgent on thread...");
                var runResponse = await client.Runs.CreateRunAsync(thread.Id, sentimentAgent.Id);
                var run = runResponse.Value;
                _logger.LogInformation("⚡ Run created: {RunId}, Initial status: {Status}", run.Id, run.Status);

                // Step 4: Poll for completion with proper tool approval handling
                var maxAttempts = 30; // 60 seconds max
                var attempts = 0;
                
                while (run.Status == "queued" || run.Status == "in_progress" || run.Status == "requires_action")
                {
                    if (attempts >= maxAttempts)
                    {
                        _logger.LogError("❌ SentimentAgent run timed out after {MaxAttempts} attempts", maxAttempts);
                        return JsonConvert.SerializeObject(new { error = "SentimentAgent run timed out", timeout_seconds = maxAttempts * 2 });
                    }

                    await Task.Delay(2000, cancellationToken); // Wait 2 seconds between polls
                    attempts++;
                    
                    // Get updated run status
                    var runUpdateResponse = await client.Runs.GetRunAsync(thread.Id, run.Id);
                    run = runUpdateResponse.Value;
                    _logger.LogInformation("📊 Run status #{Attempts}: {Status} (elapsed: {Elapsed}s)", attempts, run.Status, attempts * 2);

                    // Handle tool approvals for MCP sentiment analysis
                    if (run.Status == "requires_action" && run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
                    {
                        _logger.LogInformation("🔧 MCP Tool approval required - auto-approving sentiment analysis...");
                        var toolApprovals = new List<ToolApproval>();
                        
                        foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                        {
                            if (toolCall is RequiredMcpToolCall mcpToolCall)
                            {
                                _logger.LogInformation("✅ Approving MCP tool: {ToolName}", mcpToolCall.Name);
                                toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                            }
                        }
                        
                        if (toolApprovals.Count > 0)
                        {
                            _logger.LogInformation("🚀 Submitting {Count} tool approvals...", toolApprovals.Count);
                            var submitResponse = await client.Runs.SubmitToolOutputsToRunAsync(thread.Id, run.Id, toolApprovals: toolApprovals);
                            run = submitResponse?.Value ?? run;
                            _logger.LogInformation("✅ Tool approvals submitted, new status: {Status}", run.Status);
                        }
                    }
                }

                // Step 5: Check final status and retrieve response
                _logger.LogInformation("🏁 Run completed with final status: {Status}", run.Status);

                if (run.Status == "completed")
                {
                    // Get the conversation messages to retrieve the agent's sentiment analysis response
                    _logger.LogInformation("📥 Retrieving conversation messages...");
                    var messages = client.Messages.GetMessages(thread.Id).ToList();

                    _logger.LogInformation("💬 Found {MessageCount} messages in conversation", messages.Count);
                    
                    // Find the assistant's response (sentiment analysis result)
                    foreach (var message in messages)
                    {
                        if (message.Role.ToString().Equals("assistant", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var contentItem in message.ContentItems)
                            {
                                if (contentItem is MessageTextContent textContent)
                                {
                                    var sentimentResult = textContent.Text;
                                    _logger.LogInformation("🎯 SentimentAgent analysis result: {Result}", sentimentResult);
                                    
                                    // Return the sentiment analysis JSON directly
                                    return JsonConvert.SerializeObject(new { 
                                        runId = Guid.NewGuid().ToString(), 
                                        result = sentimentResult,
                                        mode = "sentiment-only",
                                        status = "completed"
                                    });
                                }
                            }
                        }
                    }
                    
                    _logger.LogWarning("⚠️ No assistant response found in conversation messages");
                    return JsonConvert.SerializeObject(new { error = "No sentiment analysis response found", status = "completed_no_response" });
                }
                else
                {
                    _logger.LogError("❌ SentimentAgent run failed with status: {Status}", run.Status);
                    var errorMessage = run.LastError?.Message ?? "Unknown error";
                    return JsonConvert.SerializeObject(new { error = $"SentimentAgent run failed: {errorMessage}", status = run.Status });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception in RunSentimentAgentDirectly: {Message}", ex.Message);
                return JsonConvert.SerializeObject(new { error = $"SentimentAgent execution failed: {ex.Message}" });
            }
        }

    }
}