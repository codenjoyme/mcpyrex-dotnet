using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using System.Reflection;

namespace McpDotnet;

/// <summary>
/// Tool Registry for MCP Server - .NET version of tool_registry.py
/// Handles automatic tool discovery, registration, and settings management
/// </summary>
public static class ToolRegistry
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("mcp_server");
    private static readonly List<string> _registeredTools = new();
    
    /// <summary>
    /// Get count of registered tools
    /// </summary>
    public static int ToolCount => _registeredTools.Count;

    /// <summary>
    /// Settings.yaml structure
    /// </summary>
    public class ToolSettings
    {
        public bool Enabled { get; set; } = true;
        public string Description { get; set; } = "";
        public List<string> Dependencies { get; set; } = new();
    }

    /// <summary>
    /// Check if tool or tool group is disabled via settings.yaml
    /// </summary>
    /// <param name="toolPath">Path to tool directory</param>
    /// <returns>Tuple of (isDisabled, description)</returns>
    public static (bool IsDisabled, string Description) IsToolDisabled(DirectoryInfo toolPath)
    {
        var currentPath = toolPath;
        
        // Check current directory and all parent directories up to tools/
        while (currentPath != null && currentPath.Name != "tools" && currentPath.Parent != null)
        {
            var settingsFile = Path.Combine(currentPath.FullName, "settings.yaml");
            if (File.Exists(settingsFile))
            {
                try
                {
                    var yamlContent = File.ReadAllText(settingsFile);
                    var deserializer = new DeserializerBuilder().Build();
                    var settings = deserializer.Deserialize<ToolSettings>(yamlContent);
                    
                    if (settings != null && !settings.Enabled)
                    {
                        var description = !string.IsNullOrEmpty(settings.Description) 
                            ? settings.Description 
                            : "Tool is disabled via settings.yaml";
                        return (true, description);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error reading settings file {SettingsFile}: {Error}", settingsFile, ex.Message);
                }
            }
            currentPath = currentPath.Parent;
        }
        
        return (false, "");
    }

    /// <summary>
    /// Register a tool by name
    /// </summary>
    /// <param name="toolName">Tool name</param>
    public static void RegisterTool(string toolName)
    {
        _registeredTools.Add(toolName);
        _logger.LogInformation("Registered tool: {ToolName}", toolName);
    }

    /// <summary>
    /// Register all tools by scanning the tools directory (like Python version)
    /// </summary>
    public static void RegisterTools()
    {
        _logger.LogInformation("Starting to register tools");

        var toolsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "tools");
        if (!Directory.Exists(toolsDirectory))
        {
            _logger.LogWarning("Tools directory not found: {ToolsDirectory}", toolsDirectory);
            return;
        }

        _logger.LogInformation("Scanning tools directory: {ToolsDirectory}", toolsDirectory);
        
        var toolCount = 0;
        ScanDirectory(new DirectoryInfo(toolsDirectory), new List<string>(), ref toolCount);

        _logger.LogInformation("Registered a total of {ToolCount} tools", toolCount);
        _logger.LogInformation("Tools are registered via [McpServerTool] attributes and will be automatically discovered by MCP SDK");
    }

    /// <summary>
    /// Recursively scan directory for tools
    /// </summary>
    private static void ScanDirectory(DirectoryInfo directory, List<string> prefixParts, ref int toolCount)
    {
        foreach (var item in directory.GetDirectories())
        {
            if (item.Name.StartsWith("__"))
                continue;

            var currentPrefixParts = new List<string>(prefixParts) { item.Name };
            
            // Check if there's a Tool.cs file in the current directory
            var toolFile = new FileInfo(Path.Combine(item.FullName, "Tool.cs"));
            if (toolFile.Exists)
            {
                // Check if tool is disabled via settings.yaml
                var (isDisabled, description) = IsToolDisabled(item);
                if (isDisabled)
                {
                    _logger.LogInformation("Skipping disabled tool: {ToolName} - {Description}", item.Name, description);
                    continue;
                }

                var toolName = string.Join("_", currentPrefixParts);
                var namespacePath = $"McpDotnet.Tools.{string.Join(".", currentPrefixParts.Select(CamelCase))}";
                
                // Try to initialize the tool
                try
                {
                    InitializeTool(namespacePath, toolName);
                    RegisterTool(toolName);
                    _logger.LogInformation("Registered tool: {ToolName} (namespace: {Namespace})", toolName, namespacePath);
                    toolCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing tool {ToolName}: {Error}", toolName, ex.Message);
                }
            }
            else
            {
                // If no Tool.cs in current directory, continue scanning subdirectories
                var (isDisabled, _) = IsToolDisabled(item);
                if (!isDisabled)
                {
                    ScanDirectory(item, currentPrefixParts, ref toolCount);
                }
            }
        }
    }

    /// <summary>
    /// Initialize a tool by calling its Init method if it exists
    /// </summary>
    private static void InitializeTool(string namespacePath, string toolName)
    {
        try
        {
            // Find the type in the specified namespace
            var assembly = Assembly.GetExecutingAssembly();
            var toolType = assembly.GetTypes()
                .FirstOrDefault(t => t.Namespace == namespacePath && t.Name == "Tool");

            if (toolType != null)
            {
                var initMethod = toolType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
                if (initMethod != null)
                {
                    _logger.LogInformation("Initializing tool: {ToolName}", toolName);
                    initMethod.Invoke(null, null);
                    _logger.LogInformation("Successfully initialized tool: {ToolName}", toolName);
                }
                else
                {
                    _logger.LogDebug("No Init method found for tool: {ToolName}", toolName);
                }
            }
            else
            {
                _logger.LogWarning("Tool type not found for: {NamespacePath}.Tool", namespacePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing tool {ToolName}: {Error}", toolName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Convert string to CamelCase for namespace generation
    /// </summary>
    private static string CamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }




}
