using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpDotnet.Pipeline;
using McpDotnet.Pipeline.Strategies;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpDotnet.Tools.LngBatchRun
{
    /// <summary>
    /// Executes a batch pipeline of tool calls with variable substitution using .NET expressions.
    /// 
    /// **Now powered by extensible architecture for maximum flexibility!**
    /// 
    /// Strategy-Based Features:
    /// • Tool Execution - Sequential tool calls with variable passing
    /// • Conditional Logic - if-then-else with .NET expressions  
    /// • Loops - forEach, while, repeat iterations
    /// • Parallel Execution - Run multiple steps concurrently
    /// • Timing Control - Delays and sleep functionality
    /// • Modular Design - Easy to extend with new strategies
    /// </summary>
    [McpServerToolType]
    public static class Tool
    {



        /// <summary>
        /// Execute a batch pipeline using strategy-based architecture.
        /// </summary>
        [McpServerTool]
        [Description(@"Executes a batch pipeline of tool calls with variable substitution using .NET expressions.

**Now powered by extensible architecture for maximum flexibility!**

**Pipeline Structure:**
```json
{
  ""pipeline"": [
    {
      ""tool"": ""tool_name"",
      ""params"": {
        ""param1"": ""{! variable_name !}"",
        ""param2"": ""{! variable_name.property || 'default' !}""
      },
      ""output"": ""variable_name""
    },
    {
      ""type"": ""condition"",
      ""condition"": ""{! variable_name.property > 5 !}"",
      ""then"": [
        {""tool"": ""tool_if_true"", ""params"": {...}}
      ],
      ""else"": [
        {""tool"": ""tool_if_false"", ""params"": {...}}
      ]
    }
  ],
  ""final_result"": ""{! last_variable || 'ok' !}""
}
```

**Available Step Types:**
• Tool execution with variable passing
• Conditional logic (if-then-else)  
• Loops (forEach, while, repeat)
• Parallel execution
• Delays and timing control

**Variable Substitution:**
• `{! variable !}` - Direct variable access
• `{! variable.property !}` - Object properties
• `{! variable ?? ""default"" !}` - Fallback values
• Complex expressions with operators

**Advanced Features:**
• Context filtering for output control
• File-based pipeline configuration
• User parameters support
• Debug logging capabilities

**Error Handling:**
Returns detailed error information with context when steps fail.")]
        public static async Task<string> lng_batch_run(
            [Description("Array of pipeline steps")] List<Dictionary<string, object>>? pipeline = null,
            [Description("Final result expression")] string? final_result = null,
            [Description("Pipeline file path")] string? pipeline_file = null,
            [Description("User parameters")] Dictionary<string, object>? user_params = null,
            [Description("Context fields to include")] List<string>? context_fields = null)
        {
            try
            {
                // Build merged arguments from method parameters
                var parsedArguments = new Dictionary<string, object>();
                
                if (pipeline != null)
                    parsedArguments["pipeline"] = pipeline;
                    
                if (!string.IsNullOrEmpty(final_result))
                    parsedArguments["final_result"] = final_result;
                    
                if (!string.IsNullOrEmpty(pipeline_file))
                    parsedArguments["pipeline_file"] = pipeline_file;
                    
                if (user_params != null)
                    parsedArguments["user_params"] = user_params;
                    
                if (context_fields != null)
                    parsedArguments["context_fields"] = context_fields;
                // Create default tool runner for MCP context
                Func<string, Dictionary<string, object>, Task<object>> toolRunner = async (toolName, args) =>
                {
                    // Use reflection to call Run.RunTool method
                    var runType = typeof(Tool).Assembly.GetType("McpDotnet.Run");
                    if (runType != null)
                    {
                        var runToolMethod = runType.GetMethod("RunTool", BindingFlags.NonPublic | BindingFlags.Static);
                        if (runToolMethod != null)
                        {
                            var result = runToolMethod.Invoke(null, new object[] { toolName, args });
                            if (result is Task<object?> task)
                            {
                                return await task ?? new Dictionary<string, object>();
                            }
                        }
                    }
                    throw new NotImplementedException($"Tool '{toolName}' execution not available in static context");
                };
                
                // Create strategy-based pipeline executor
                var executor = new StrategyBasedExecutor(toolRunner);
                
                // Log available strategies (using Console for static context)
                var strategies = executor.GetStrategies();
                Console.WriteLine($"Available strategies: {string.Join(", ", strategies)}");
                
                Dictionary<string, object> mergedArguments;
                // Check if pipeline_file is provided
                if (parsedArguments.ContainsKey("pipeline_file"))
                {
                    var pipelineFile = parsedArguments["pipeline_file"]?.ToString();
                    if (string.IsNullOrEmpty(pipelineFile))
                    {
                        throw new ArgumentException("pipeline_file cannot be null or empty");
                    }
                    Console.WriteLine($"Loading pipeline from file: {pipelineFile}");
                    
                    try
                    {
                        // Read pipeline configuration from file
                        if (!Path.IsPathRooted(pipelineFile))
                        {
                            // Make relative paths relative to the project root
                            var projectRoot = Directory.GetCurrentDirectory();
                            pipelineFile = Path.Combine(projectRoot, pipelineFile);
                        }
                        
                        var json = await File.ReadAllTextAsync(pipelineFile);
                        var pipelineConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(json, new JsonSerializerOptions
                        {
                            Converters = { new McpDotnet.Common.ObjectConverter() }
                        });
                        
                        // Merge file config with arguments, giving priority to direct arguments
                        mergedArguments = new Dictionary<string, object>(pipelineConfig ?? new Dictionary<string, object>());
                        foreach (var kvp in parsedArguments)
                        {
                            if (kvp.Key != "pipeline_file")
                            {
                                mergedArguments[kvp.Key] = kvp.Value;
                            }
                        }
                        
                        Console.WriteLine($"Successfully loaded pipeline from {pipelineFile}");
                    }
                    catch (FileNotFoundException)
                    {
                        return JsonSerializer.Serialize(new Dictionary<string, object>
                        {
                            ["success"] = false,
                            ["error"] = $"Pipeline file not found: {pipelineFile}",
                            ["context"] = new Dictionary<string, object>()
                        }, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch (JsonException e)
                    {
                        return JsonSerializer.Serialize(new Dictionary<string, object>
                        {
                            ["success"] = false,
                            ["error"] = $"Invalid JSON in pipeline file {pipelineFile}: {e.Message}",
                            ["context"] = new Dictionary<string, object>()
                        }, new JsonSerializerOptions { WriteIndented = true });
                    }
                }
                else
                {
                    // Use arguments directly if no file specified
                    mergedArguments = parsedArguments;
                }
                
                // Validate that we have either pipeline or pipeline_file
                if (!mergedArguments.ContainsKey("pipeline") && !parsedArguments.ContainsKey("pipeline_file"))
                {
                    return JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["success"] = false,
                        ["error"] = "Either 'pipeline' parameter or 'pipeline_file' parameter is required",
                        ["context"] = new Dictionary<string, object>()
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                
                // Execute pipeline using the strategy-based system
                var result = await executor.ExecuteAsync(mergedArguments);
                
                // Get result as dictionary
                var resultDict = result.ToDictionary();
                if (parsedArguments.ContainsKey("pipeline_file"))
                {
                    resultDict["pipeline_source"] = parsedArguments["pipeline_file"];
                }
                
                // Filter context fields if specified
                var contextFields = mergedArguments.ContainsKey("context_fields") 
                    ? mergedArguments["context_fields"] as List<string> ?? new List<string>()
                    : new List<string>();
                
                if (resultDict.ContainsKey("context"))
                {
                    var originalContext = resultDict["context"] as Dictionary<string, object> ?? new Dictionary<string, object>();
                    
                    if (contextFields.Count == 0)
                    {
                        // No context_fields specified - hide all context to save tokens
                        resultDict["context"] = new Dictionary<string, object>();
                        resultDict["context_filtered"] = true;
                        resultDict["total_context_fields"] = originalContext.Count;
                        resultDict["shown_context_fields"] = 0;
                    }
                    else if (contextFields.Count == 1 && contextFields[0] == "*")
                    {
                        // Show all context
                        resultDict["context_filtered"] = false;
                    }
                    else
                    {
                        // Show only specified fields
                        var filteredContext = new Dictionary<string, object>();
                        foreach (var field in contextFields)
                        {
                            if (originalContext.ContainsKey(field))
                            {
                                filteredContext[field] = originalContext[field];
                            }
                        }
                        resultDict["context"] = filteredContext;
                        resultDict["context_filtered"] = true;
                        resultDict["total_context_fields"] = originalContext.Count;
                        resultDict["shown_context_fields"] = filteredContext.Count;
                    }
                }
                
                // Return result in the expected format
                return JsonSerializer.Serialize(resultDict, new JsonSerializerOptions { WriteIndented = true });
                
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error in lng_batch_run: {e}");
                return JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = $"Unexpected error: {e.Message}",
                    ["context"] = new Dictionary<string, object>()
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
    }



    /// <summary>
    /// Extensions for PipelineResult to convert to dictionary format.
    /// </summary>
    public static class PipelineResultExtensions
    {
        /// <summary>
        /// Convert PipelineResult to dictionary format for JSON serialization.
        /// </summary>
        public static Dictionary<string, object> ToDictionary(this PipelineResult result)
        {
            var dict = new Dictionary<string, object>
            {
                ["success"] = result.Success,
                ["context"] = result.Context ?? new Dictionary<string, object>()
            };

            if (!string.IsNullOrEmpty(result.Error))
            {
                dict["error"] = result.Error;
            }

            if (result.Result != null)
            {
                dict["result"] = result.Result;
            }

            return dict;
        }
    }
}
