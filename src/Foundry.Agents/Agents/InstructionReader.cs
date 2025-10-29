using System;
using System.Collections.Concurrent;
using System.IO;

namespace Foundry.Agents.Agents
{
    public static class InstructionReader
    {
        // Cache for resolved file paths to avoid repeated File.Exists checks
        private static readonly ConcurrentDictionary<string, string?> _pathCache = new ConcurrentDictionary<string, string?>();
        
        // Read the section for agentName from Agents/RemoteData/RemoteDataInstructions.md
        // Prefer a per-agent file at Agents/<Agent>/<Agent>Instructions.md, falling back to the shared RemoteData file.
        // If neither exists, return empty string.
        public static string ReadSection(string agentName)
        {
            try
            {
                // Check cache first for already resolved paths
                var cacheKey = $"agent:{agentName}";
                if (_pathCache.TryGetValue(cacheKey, out var cachedPath))
                {
                    if (!string.IsNullOrEmpty(cachedPath))
                    {
                        try
                        {
                            return File.ReadAllText(cachedPath).Trim();
                        }
                        catch
                        {
                            // If cached path fails, remove from cache and continue
                            _pathCache.TryRemove(cacheKey, out _);
                        }
                    }
                    else if (cachedPath == null)
                    {
                        // Cached negative result (no file found)
                        return string.Empty;
                    }
                }
                
                // Candidate locations for per-agent instruction files. Hosted runs may execute from
                // the repository root or from inside the src/Foundry.Agents folder. Try both places
                // and also AppContext.BaseDirectory variants for robustness.
                var candidates = new[]
                {
                    Path.Combine("Agents", agentName, agentName + "Instructions.md"),
                    Path.Combine("src", "Foundry.Agents", "Agents", agentName, agentName + "Instructions.md"),
                    Path.Combine(AppContext.BaseDirectory, "Agents", agentName, agentName + "Instructions.md"),
                    Path.Combine(AppContext.BaseDirectory, "src", "Foundry.Agents", "Agents", agentName, agentName + "Instructions.md")
                };

                foreach (var perAgentPath in candidates)
                {
                    try
                    {
                        if (File.Exists(perAgentPath))
                        {
                            // Cache the successful path for future use
                            _pathCache.TryAdd(cacheKey, perAgentPath);
                            return File.ReadAllText(perAgentPath).Trim();
                        }
                    }
                    catch
                    {
                        // ignore and continue to next candidate
                    }
                }

                // 2) legacy composite file (RemoteDataInstructions.md) with headers - try same candidate roots
                var legacyCandidates = new[]
                {
                    Path.Combine("Agents", "RemoteData", "RemoteDataInstructions.md"),
                    Path.Combine("src", "Foundry.Agents", "Agents", "RemoteData", "RemoteDataInstructions.md"),
                    Path.Combine(AppContext.BaseDirectory, "Agents", "RemoteData", "RemoteDataInstructions.md"),
                    Path.Combine(AppContext.BaseDirectory, "src", "Foundry.Agents", "Agents", "RemoteData", "RemoteDataInstructions.md")
                };

                string? txt = null;
                foreach (var path in legacyCandidates)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            txt = File.ReadAllText(path);
                            break;
                        }
                    }
                    catch
                    {
                        // ignore and continue
                    }
                }

                if (string.IsNullOrEmpty(txt))
                {
                    // Cache negative result
                    _pathCache.TryAdd(cacheKey, null);
                    return string.Empty;
                }

                var header = $"# {agentName} Agent Instructions";
                var idx = txt.IndexOf(header, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    // Fallback: return the whole file
                    return txt.Trim();
                }

                var start = idx + header.Length;
                var sepIdx = txt.IndexOf("\n---", start, StringComparison.Ordinal);
                if (sepIdx < 0)
                {
                    sepIdx = txt.IndexOf("\n# ", start, StringComparison.Ordinal);
                }

                var section = sepIdx > 0 ? txt.Substring(start, sepIdx - start) : txt.Substring(start);
                return section.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
