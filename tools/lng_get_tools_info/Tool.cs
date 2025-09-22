using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using McpDotnet;

namespace McpDotnet.Tools.LngGetToolsInfo;

/// <summary>
/// Tool information retrieval - .NET version of lng_get_tools_info
/// </summary>
[McpServerToolType]
public static class Tool
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("lng_get_tools_info");

    /// <summary>
    /// Tool name for registration
    /// </summary>
    public static string ToolName => "lng_get_tools_info";

    /// <summary>
    /// Initialize the tool info tool
    /// </summary>
    public static void Init()
    {
        _logger.LogInformation("Initializing lng_get_tools_info tool");
    }

    /// <summary>
    /// Returns detailed information about specific tools
    /// </summary>
    /// <param name="tools">Comma-separated list of tool names to get info for. If not provided, returns info about all tools.</param>
    /// <param name="format">Output format - 'markdown' (default) or 'json'. JSON format returns structured data for API consumption.</param>
    /// <returns>Tool information in requested format</returns>
    [McpServerTool]
    [Description(@"Returns detailed information about specific tools.

**Parameters:**
- `tools` (string, optional): Comma-separated list of tool names to get info for. If not provided, returns info about all tools.
- `format` (string, optional): Output format - 'markdown' (default) or 'json'. JSON format returns structured data for API consumption.

**Example Usage:**
- Get info for specific tools: `{""tools"": ""lng_file_list,lng_file_read""}`
- Get info for single tool: `{""tools"": ""lng_file_list""}`
- Get info for all tools: `{}` (no parameters)
- Get JSON format: `{""format"": ""json""}` or `{""tools"": ""lng_file_read"", ""format"": ""json""}`

This tool helps you understand the capabilities, parameters, and usage examples of specific tools in the system.")]
    public static Task<string> GetToolsInfo(
        [Description("Comma-separated list of tool names to get information for")] 
        string? tools = null,
        [Description("Output format - 'markdown' (default) or 'json'")]
        string format = "markdown")
    {
        try
        {
            _logger.LogInformation("Getting tools info - tools: {Tools}, format: {Format}", tools, format);
            
            // Get all tools information using reflection (matching Python version approach)
            var toolInfoList = GetAllToolsInfo();
            
            // Filter tools if specific ones are requested
            var filteredTools = new List<ToolInfo>();
            var missingTools = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(tools))
            {
                var requestedTools = tools.Split(',').Select(t => t.Trim()).ToList();
                
                foreach (var toolName in requestedTools)
                {
                    var found = toolInfoList.FirstOrDefault(t => t.Name == toolName);
                    if (found != null)
                    {
                        filteredTools.Add(found);
                    }
                    else
                    {
                        missingTools.Add(toolName);
                    }
                }
            }
            else
            {
                // Use all tools if none specified
                filteredTools = toolInfoList;
            }
            
            // Return JSON format if requested (exact match to Python version)
            if (format == "json")
            {
                var jsonResponse = new
                {
                    api_version = "1.0.0",
                    success = true,
                    total_tools = filteredTools.Count,
                    requested_tools = !string.IsNullOrWhiteSpace(tools) ? filteredTools.Count : toolInfoList.Count,
                    missing_tools = !string.IsNullOrWhiteSpace(tools) ? missingTools : new List<string>(),
                    tools = filteredTools.Select(tool => new
                    {
                        name = tool.Name,
                        description = tool.Description,
                        short_description = tool.Description.Split('\n')[0],
                        category = ExtractCategoryFromName(tool.Name),
                        parameters = tool.Parameters
                    }).ToArray()
                };
                
                var jsonResult = JsonSerializer.Serialize(jsonResponse, new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                return Task.FromResult(jsonResult);
            }
            
            // Original markdown format (exact match to Python version)
            string markdownContent;
            
            if (!string.IsNullOrWhiteSpace(tools))
            {
                // Build markdown for filtered tools
                markdownContent = $"# Langchain MCP Tools (Filtered: {filteredTools.Count} of {toolInfoList.Count} requested)\n\n";
                
                if (missingTools.Any())
                {
                    markdownContent += $"**⚠️ Missing tools:** {string.Join(", ", missingTools)}\n\n";
                }
                
                markdownContent += "This document describes the requested tools available in the Langchain Model Context Protocol (MCP) implementation.\n\n";
                markdownContent += "## Requested Tools\n\n";
                
                // Add each filtered tool's information
                for (int i = 0; i < filteredTools.Count; i++)
                {
                    var tool = filteredTools[i];
                    markdownContent += $"### {i + 1}. `{tool.Name}`\n\n";
                    markdownContent += $"{tool.Description}\n\n";
                }
            }
            else
            {
                // Show all tools
                markdownContent = "# Langchain MCP Tools\n\n";
                markdownContent += "This document describes the tools available in the Langchain Model Context Protocol (MCP) implementation.\n\n";
                markdownContent += $"## Available Tools (Total: {toolInfoList.Count})\n\n";
                
                // Add each tool's information
                for (int i = 0; i < toolInfoList.Count; i++)
                {
                    var tool = toolInfoList[i];
                    markdownContent += $"### {i + 1}. `{tool.Name}`\n\n";
                    markdownContent += $"{tool.Description}\n\n";
                }
            }
            
            return Task.FromResult(markdownContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tools information: {Error}", ex.Message);
            return Task.FromResult($"Error retrieving tools information: {ex.Message}");
        }
    }

    /// <summary>
    /// Get information about all tools using reflection (matching Python version approach)
    /// </summary>
    private static List<ToolInfo> GetAllToolsInfo()
    {
        var toolInfoList = new List<ToolInfo>();
        
        try
        {
            // Get current assembly
            var assembly = Assembly.GetExecutingAssembly();
            
            // Find all methods with [McpServerTool] attribute
            var toolMethods = assembly.GetTypes()
                .SelectMany(type => type.GetMethods())
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() != null)
                .ToList();
            
            _logger.LogInformation("Found {Count} methods with [McpServerTool] attribute", toolMethods.Count);
            
            foreach (var method in toolMethods)
            {
                try
                {
                    var toolName = GetToolNameFromMethod(method);
                    var description = GetDescriptionFromMethod(method);
                    var parameters = GetParametersFromMethod(method);
                    
                    toolInfoList.Add(new ToolInfo
                    {
                        Name = toolName,
                        Description = description,
                        Parameters = parameters
                    });
                    
                    _logger.LogDebug("Added tool info for: {ToolName}", toolName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing method {Method}: {Error}", method.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tools info: {Error}", ex.Message);
        }
        
        return toolInfoList;
    }

    /// <summary>
    /// Extract tool name from method (matching Python tool name convention)
    /// </summary>
    private static string GetToolNameFromMethod(MethodInfo method)
    {
        // Convert method name to snake_case tool name (e.g., GetToolsInfo -> lng_get_tools_info)
        var methodName = method.Name;
        
        // Handle specific known methods
        if (methodName == "GetToolsInfo")
            return "lng_get_tools_info";
        if (methodName == "Calculate")
            return "lng_math_calculator";
        
        // Generic conversion for future tools
        var snakeCase = string.Concat(methodName.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
        
        // Add lng_ prefix if not present
        if (!snakeCase.StartsWith("lng_"))
        {
            snakeCase = "lng_" + snakeCase;
        }
        
        return snakeCase;
    }

    /// <summary>
    /// Extract description from method attribute
    /// </summary>
    private static string GetDescriptionFromMethod(MethodInfo method)
    {
        var descAttribute = method.GetCustomAttribute<DescriptionAttribute>();
        return descAttribute?.Description ?? "No description available.";
    }

    /// <summary>
    /// Extract parameters from method signature (matching Python schema extraction)
    /// </summary>
    private static List<ParameterInfo> GetParametersFromMethod(MethodInfo method)
    {
        var parameters = new List<ParameterInfo>();
        
        foreach (var param in method.GetParameters())
        {
            var paramDescription = param.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description";
            var isOptional = param.HasDefaultValue;
            var paramType = GetParameterTypeName(param.ParameterType);
            
            parameters.Add(new ParameterInfo
            {
                Name = param.Name ?? "unknown",
                Type = paramType,
                Required = !isOptional,
                Description = paramDescription,
                DefaultValue = isOptional ? param.DefaultValue : null
            });
        }
        
        return parameters;
    }

    /// <summary>
    /// Convert .NET type to JSON schema type name
    /// </summary>
    private static string GetParameterTypeName(Type type)
    {
        // Handle nullable types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
        }
        
        return Type.GetTypeCode(type) switch
        {
            TypeCode.String => "string",
            TypeCode.Int32 => "integer",
            TypeCode.Int64 => "integer", 
            TypeCode.Double => "number",
            TypeCode.Single => "number",
            TypeCode.Boolean => "boolean",
            _ => "string"
        };
    }

    /// <summary>
    /// Extract category from tool name (matching Python version logic)
    /// </summary>
    private static string ExtractCategoryFromName(string toolName)
    {
        // Match Python version logic exactly
        if (toolName.StartsWith("lng_"))
        {
            var parts = toolName.Substring(4).Split('_'); // Remove lng_ prefix
            if (parts.Length == 1)
            {
                return FormatCategoryName(parts[0]);
            }
            else if (parts.Length > 1)
            {
                var mainCategory = FormatCategoryName(parts[0]);
                var subCategory = FormatCategoryName(parts[1]);
                return $"{mainCategory} / {subCategory}";
            }
        }
        
        return "Unknown";
    }

    /// <summary>
    /// Format category name (matching Python version)
    /// </summary>
    private static string FormatCategoryName(string rawName)
    {
        // Replace underscores with spaces and title case
        return rawName.Replace("_", " ").ToTitleCase();
    }
}

/// <summary>
/// Tool information structure (matching Python version structure)
/// </summary>
public class ToolInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ParameterInfo> Parameters { get; set; } = new();
}

/// <summary>
/// Parameter information structure (matching Python version structure)  
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public string Description { get; set; } = "";
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Extension method for string title case conversion
/// </summary>
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }
}
