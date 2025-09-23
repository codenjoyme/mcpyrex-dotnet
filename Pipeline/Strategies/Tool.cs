using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Pipeline.Strategies
{
    /// <summary>
    /// Tool execution strategy.
    /// 
    /// This strategy handles basic tool execution with parameter substitution
    /// and result storage. It's the fundamental building block of the pipeline system.
    /// </summary>

    public class ToolStrategy : ExecutionStrategy
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ToolStrategy>();

        private readonly Func<string, Dictionary<string, object>, Task<object>> _toolRunner;

        /// <summary>
        /// Strategy for executing tool calls - the basic pipeline functionality.
        /// </summary>
        public ToolStrategy(Func<string, Dictionary<string, object>, Task<object>> toolRunner)
        {
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
        }

        public override bool CanHandle(Dictionary<string, object> step)
        {
            /// Handle steps that have a 'tool' field.
            return step.ContainsKey("tool") && step.GetValueOrDefault("type", "tool").ToString() == "tool";
        }

        public override async Task<PipelineResult> ExecuteAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute a tool call with parameter substitution.
            try
            {
                var toolName = step["tool"].ToString() ?? throw new InvalidOperationException("Tool name is required");
                var parameters = step.GetValueOrDefault("params", new Dictionary<string, object>()) as Dictionary<string, object> ?? new();
                var outputVar = step.GetValueOrDefault("output")?.ToString();

                Logger.LogInformation($"Step {context.StepNumber}: Executing tool '{toolName}'");

                // Determine if tool expects native .NET objects or strings
                var preserveObjects = ShouldPreserveObjects(toolName);

                // Substitute variables in parameters using unified expression processing
                var substitutedParams = ExpressionEvaluator.SubstituteInObject(parameters, context.Variables, 
                    preserveObjects: preserveObjects) as Dictionary<string, object> ?? new();

                // Special handling for string params - try to parse as JSON if it looks like JSON
                if (substitutedParams.Count == 0 && step.ContainsKey("params") && step["params"] is string paramsString)
                {
                    substitutedParams = ProcessSingleStringParam(paramsString, toolName, context.StepNumber);
                }

                var paramsStr = JsonSerializer.Serialize(substitutedParams);
                var truncatedParams = paramsStr.Length > 1000 ? paramsStr.Substring(0, 1000) + "..." : paramsStr;
                Logger.LogDebug($"Parameters after substitution: {truncatedParams}");

                // Execute the tool
                var toolResult = await _toolRunner(toolName, substitutedParams);

                // Parse and store result - extract and parse JSON from TextContent
                var parsedResult = ParseToolResult(toolResult);

                // Ensure result is JSON-compatible before storing
                var jsonCompatibleResult = EnsureJsonCompatible(parsedResult);

                // Check if tool result indicates failure
                if (jsonCompatibleResult is Dictionary<string, object> resultDict && 
                    resultDict.GetValueOrDefault("success") is bool success && !success)
                {
                    var errorMsg = $"Step {context.StepNumber} tool '{toolName}' reported failure: " +
                                 $"{resultDict.GetValueOrDefault("error", "Unknown error")}";
                    Logger.LogError(errorMsg);
                    return new PipelineResult
                    {
                        Success = false,
                        Error = errorMsg,
                        Step = context.StepNumber,
                        Tool = toolName,
                        Context = context.Variables
                    };
                }

                if (!string.IsNullOrEmpty(outputVar))
                {
                    context.Variables[outputVar] = jsonCompatibleResult;
                    Logger.LogDebug($"Stored JSON-compatible result in variable '{outputVar}': {jsonCompatibleResult.GetType().Name}");

                    // Save output log if output_log is specified
                    if (step.ContainsKey("output_log"))
                    {
                        try
                        {
                            SaveOutputLog(step["output_log"].ToString()!, jsonCompatibleResult);
                        }
                        catch (Exception logError)
                        {
                            Logger.LogWarning($"Failed to save output log: {logError.Message}");
                        }
                    }
                }

                return new PipelineResult
                {
                    Success = true,
                    Context = context.Variables
                };
            }
            catch (Exception e)
            {
                var toolName = step.GetValueOrDefault("tool")?.ToString() ?? "unknown";
                var errorMsg = $"Step {context.StepNumber} tool '{toolName}' failed: {e.Message}";
                Logger.LogError(errorMsg);
                return new PipelineResult
                {
                    Success = false,
                    Error = errorMsg,
                    Step = context.StepNumber,
                    Tool = toolName,
                    Context = context.Variables
                };
            }
        }

        public override string StrategyName => "Tool";

        private object EnsureJsonCompatible(object obj)
        {
            /// Ensure object is JSON-serializable.
            try
            {
                // Test if object is already JSON-serializable
                JsonSerializer.Serialize(obj);
                return obj;
            }
            catch (Exception)
            {
                // Convert problematic types
                return obj switch
                {
                    object tuple when tuple.GetType().Name.StartsWith("Tuple") => ConvertTupleToList(tuple),
                    HashSet<object> set => set.Select(EnsureJsonCompatible).ToList(),
                    DateTime dateTime => dateTime.ToString("O"), // ISO 8601 format
                    decimal dec => (double)dec,
                    Dictionary<string, object> dict => dict.ToDictionary(kvp => kvp.Key, kvp => EnsureJsonCompatible(kvp.Value)),
                    List<object> list => list.Select(EnsureJsonCompatible).ToList(),
                    _ => obj.ToString() ?? "null"
                };
            }
        }

        private List<object> ConvertTupleToList(object tuple)
        {
            var type = tuple.GetType();
            var items = new List<object>();
            
            for (int i = 1; i <= 8; i++) // Tuples in .NET support up to 8 items (Item1 to Item8)
            {
                var property = type.GetProperty($"Item{i}");
                if (property == null) break;
                items.Add(EnsureJsonCompatible(property.GetValue(tuple)!));
            }
            
            return items;
        }

        private bool ShouldPreserveObjects(string toolName)
        {
            /// Determine if tool expects native .NET objects or strings.
            var dotnetObjectTools = new HashSet<string>
            {
                "lng_json_to_csv",      // Expects json_data as array/object
                "lng_javascript_execute", // parameters field expects objects
            };

            return dotnetObjectTools.Contains(toolName);
        }

        private void SaveOutputLog(string logName, object outputData)
        {
            /// Save output data to a timestamped log file.
            // Create debug logs directory if it doesn't exist
            var debugDir = Path.Combine(".", "logs", "pipeline");
            Directory.CreateDirectory(debugDir);

            // Generate timestamped filename
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffffff");
            var filename = $"{timestamp}_{logName}.log";
            var filePath = Path.Combine(debugDir, filename);

            // Save data as pretty-printed JSON
            try
            {
                var json = JsonSerializer.Serialize(outputData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                Logger.LogInformation($"Saved output log: {filename}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to save output log {filename}: {e.Message}");
                throw;
            }
        }

        private Dictionary<string, object> ProcessSingleStringParam(string paramsString, string toolName, object stepNumber)
        {
            /// Process a single string that should be parsed as JSON parameters.
            /// 
            /// This handles the case where entire params field is a JSON string from webhook.
            Logger.LogInformation($"Step {stepNumber}: Processing single string param for tool '{toolName}': {paramsString}");
            Logger.LogInformation($"Step {stepNumber}: String type: {paramsString.GetType().Name}");

            var strippedValue = paramsString.Trim();
            Logger.LogInformation($"Step {stepNumber}: Stripped value: '{strippedValue}'");

            // First, try to parse as JSON
            if ((strippedValue.StartsWith('{') && strippedValue.EndsWith('}')) ||
                (strippedValue.StartsWith('[') && strippedValue.EndsWith(']')))
            {
                try
                {
                    var parsedParams = JsonSerializer.Deserialize<Dictionary<string, object>>(strippedValue);
                    Logger.LogInformation($"Step {stepNumber}: Successfully parsed as JSON: {JsonSerializer.Serialize(parsedParams)}");
                    return parsedParams ?? new Dictionary<string, object>();
                }
                catch (JsonException e)
                {
                    Logger.LogInformation($"Step {stepNumber}: JSON parsing failed: {e.Message}");
                }
            }

            // If parsing fails, return empty dict (so tool gets no params)
            Logger.LogInformation($"Step {stepNumber}: Returning empty dict as fallback");
            return new Dictionary<string, object>();
        }

        private object ParseToolResult(object toolResult)
        {
            /// Parse and store result - extract and parse JSON from TextContent or direct string
            
            // Handle direct string result (from .NET tools)
            if (toolResult is string stringResult)
            {
                // Try to parse as JSON, but if it fails, keep as text
                try
                {
                    var parsedResult = JsonSerializer.Deserialize<object>(stringResult, new JsonSerializerOptions
                    {
                        Converters = { new McpDotnet.Common.ObjectConverter() }
                    });
                    Logger.LogDebug("Successfully parsed direct string result as JSON");
                    return parsedResult!;
                }
                catch (JsonException)
                {
                    // Not JSON - keep as plain text (useful for CSV, HTML, etc.)
                    Logger.LogDebug("Direct string result kept as plain text (not JSON)");
                    return stringResult;
                }
            }
            
            // Handle List<object> result (from MCP TextContent)
            if (toolResult is List<object> list && list.Count > 0)
            {
                var firstResult = list[0];
                
                // If it's a TextContent-like object, extract the text
                if (HasTextProperty(firstResult))
                {
                    var textContent = GetTextProperty(firstResult);
                    
                    // Try to parse as JSON, but if it fails, keep as text
                    try
                    {
                        var parsedResult = JsonSerializer.Deserialize<object>(textContent, new JsonSerializerOptions
                        {
                            Converters = { new McpDotnet.Common.ObjectConverter() }
                        });
                        Logger.LogDebug("Successfully parsed tool result as JSON");
                        return parsedResult!;
                    }
                    catch (JsonException)
                    {
                        // Not JSON - keep as plain text (useful for CSV, HTML, etc.)
                        Logger.LogDebug("Tool result kept as plain text (not JSON)");
                        return textContent;
                    }
                }
                else
                {
                    return firstResult;
                }
            }
            else
            {
                return toolResult;
            }
        }

        private bool HasTextProperty(object obj)
        {
            return obj.GetType().GetProperty("Text") != null;
        }

        private string GetTextProperty(object obj)
        {
            return obj.GetType().GetProperty("Text")?.GetValue(obj)?.ToString() ?? string.Empty;
        }
    }
}
