using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI;
using Newtonsoft.Json;
using System.Text.Json;
using Foundry.Agents.Agents.Shared;

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

            var endpoint = System.Environment.GetEnvironmentVariable("PROJECT_ENDPOINT") ?? "http://localhost:3000";
            // Create a PersistentAgentsClient for the provided endpoint. When PROJECT_ENDPOINT is https, DefaultAzureCredential will be used.
            var persistentAgentsClient = new Azure.AI.Agents.Persistent.PersistentAgentsClient(endpoint, new Azure.Identity.DefaultAzureCredential());

            // Prefer persisted local agent ids (Agents/<Agent>/agent-id.txt). The
            // GetOrCreateAIAgentAsync helper (used below) already reads persisted
            // agent ids first using AgentFileHelpers.ReadPersistedAgentIdAsync, so
            // there's no need to read environment variables here.

            // Ensure the RemoteData agent exists on the target persistent agents service. Create if missing.
            AIAgent? remoteDataAIAgent = await Foundry.Agents.Agents.RemoteData.RemoteDataAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
            if (remoteDataAIAgent == null)
            {
                _logger.LogError("Failed to obtain or create RemoteData agent. Aborting orchestration.");
                return JsonConvert.SerializeObject(new { error = "Failed to obtain RemoteData agent" });
            }

            // Ensure the Energy agent exists (create if necessary) 
            var energyAIAgent = await Foundry.Agents.Agents.Energy.EnergyAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
            if (energyAIAgent == null)
            {
                _logger.LogError("Failed to obtain or create Energy agent. Aborting orchestration.");
                return JsonConvert.SerializeObject(new { error = "Failed to obtain Energy agent" });
            }            
            var emailGeneratorAIAgent = await Foundry.Agents.Agents.EmailGenerator.EmailGeneratorAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
            if (emailGeneratorAIAgent == null)
            {
                _logger.LogError("Failed to obtain or create EmailGenerator agent. Aborting orchestration.");
                return JsonConvert.SerializeObject(new { error = "Failed to obtain EmailGenerator agent" });
            }

            var emailAIAgent = await Foundry.Agents.Agents.EmailAssistant.EmailAssistantAgent.GetOrCreateAIAgentAsync(endpoint, _configuration, _logger);
            if (emailAIAgent == null)
            {
                _logger.LogError("Failed to obtain or create EmailAssistant agent. Aborting orchestration.");
                return JsonConvert.SerializeObject(new { error = "Failed to obtain EmailAssistant agent" });
            }
            _logger.LogInformation($"remote data agent: {remoteDataAIAgent.DisplayName}");
            _logger.LogInformation($"energy agent: {energyAIAgent.DisplayName}");
            _logger.LogInformation($"email composer agent: {emailGeneratorAIAgent.DisplayName}");
            _logger.LogInformation($"email assistant agent: {emailAIAgent.DisplayName}");

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
            var runPrompt = JsonConvert.SerializeObject(payload);
            _logger.LogInformation($"Running workflow with prompt: {runPrompt}");

            // Build a list of executors conditionally: when emailRequested is false we only
            // run RemoteData -> Energy (no EmailGenerator/EmailAssistant). When email is
            // requested include the generator and assistant in the pipeline so the full
            // sequence RemoteData -> Energy -> EmailGenerator -> EmailAssistant runs.
            var executors = new System.Collections.Generic.List<AIAgent>();
            executors.Add(remoteDataAIAgent);
            executors.Add(energyAIAgent);
            if (emailRequested && emailGeneratorAIAgent != null && emailAIAgent != null)
            {
                executors.Add(emailGeneratorAIAgent);
                executors.Add(emailAIAgent);
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
            var run = await InProcessExecution.StreamAsync(workflow, new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, runPrompt));
            //var test = await InProcessExecution.RunAsync(workflow, runPrompt);

            // Execute the workflow using the streaming API.
            // Capture the last agent update data into resultJson and return it.
            //var run = await InProcessExecution.StreamAsync(workflow, new Microsoft.Agents.AI.ChatMessage(Microsoft.Agents.AI.ChatRole.User, "Compute a deterministic baseline and three energy-saving measures for zone SE3 in Stockholm on 2025-10-01. Send the summary by email to kapeltol@microsoft.com. Return only the GlobalEnvelope JSON."));

            // Must send the turn token to trigger the agents.
            // The agents are wrapped as executors. When they receive messages,
            // they will cache the messages and only start processing when they receive a TurnToken.
            // Send the TurnToken (emit events) to kick the workflow into executing the agent runs.
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

            string? lastExecutorId = null;
            // Guard to ensure we inject the normalized EmailGenerator payload only once
            bool emailAssistantPayloadInjected = false;

            await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
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
                                ConsoleWriteSafe(SanitizeForConsole(text));
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

                            // If we reach here the output was not from the EmailGenerator (final output),
                            // so stop streaming after capturing it.
                            shouldBreak = true;
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
            if (resultJson != null)
            {
                SaveEnergyOutputAndPlot(resultJson);
            }
            else
            {
                _logger.LogWarning("Failed to save/plot energy output from orchestrator");
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

    }
}