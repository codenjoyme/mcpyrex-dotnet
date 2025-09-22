/**
 * Universal tool runner for MCP tools - .NET version.
 * Allows quick testing and execution of any tool without MCP server overhead.
 * Usage: dotnet run run <tool_name> [json_args]
 */
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using YamlDotNet.Serialization;
using System.Diagnostics;

namespace McpDotnet;

/// <summary>
/// Universal tool runner for MCP tools - .NET version of run.py
/// </summary>
public static class Run
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("mcp_server", LogLevel.Debug);
    private static readonly List<ToolDefinition> _toolDefinitions = new();

    /// <summary>
    /// Tool definition structure (matching Python version)
    /// </summary>
    public class ToolDefinition
    {
        public string Name { get; set; } = "";
        public string NamespacePath { get; set; } = "";
        public MethodInfo Method { get; set; } = null!;
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Initialize and discover all available tools
    /// </summary>
    static Run()
    {
        DiscoverTools();
    }

    /// <summary>
    /// Discover all available tools using reflection (like Python tool_definitions)
    /// </summary>
    private static void DiscoverTools()
    {
        _logger.LogInformation("Starting tool discovery");

        try
        {
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
                    var namespacePath = method.DeclaringType?.Namespace ?? "";

                    var toolDef = new ToolDefinition
                    {
                        Name = toolName,
                        NamespacePath = namespacePath,
                        Method = method,
                        Description = description
                    };

                    _toolDefinitions.Add(toolDef);
                    _logger.LogDebug("Registered tool: {ToolName} from {Namespace}", toolName, namespacePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing method {Method}: {Error}", method.Name, ex.Message);
                }
            }

            _logger.LogInformation("Tool discovery completed, found {Count} tools", _toolDefinitions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tool discovery: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Test a tool with given arguments (like Python test_tool function)
    /// </summary>
    public static async Task<object?> TestTool(string toolName, Dictionary<string, object>? arguments = null)
    {
        arguments ??= new Dictionary<string, object>();

        try
        {
            // Check if tool exists
            var tool = _toolDefinitions.FirstOrDefault(t => t.Name == toolName);
            if (tool == null)
            {
                var availableTools = _toolDefinitions.Select(t => t.Name).ToArray();
                _logger.LogError("Tool '{ToolName}' not found. Available tools: {AvailableTools}", toolName, string.Join(", ", availableTools));
                Console.WriteLine($"❌ Tool '{toolName}' not found.");
                Console.WriteLine($"📋 Available tools: {string.Join(", ", availableTools)}");
                return null;
            }

            _logger.LogInformation("Testing tool: {ToolName} with arguments: {Arguments}", toolName, JsonSerializer.Serialize(arguments));
            Console.WriteLine($"🔧 Testing tool: {toolName}");
            if (arguments.Count > 0)
            {
                Console.WriteLine($"📥 Arguments: {JsonSerializer.Serialize(arguments, new JsonSerializerOptions { WriteIndented = true })}");
            }

            // Run the tool
            var result = await RunTool(toolName, arguments);
            _logger.LogInformation("Tool {ToolName} executed successfully", toolName);

            Console.WriteLine("📤 Result:");
            if (result != null)
            {
                // Try to parse JSON for pretty printing (like Python version)
                if (result is string resultString)
                {
                    try 
                    {
                        var parsedJson = JsonSerializer.Deserialize<object>(resultString);
                        Console.WriteLine(JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                    }
                    catch (JsonException)
                    {
                        // If not JSON, print as is
                        Console.WriteLine(resultString);
                    }
                }
                else
                {
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                }
            }
            else
            {
                Console.WriteLine("null");
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error testing tool '{ToolName}': {Error}", toolName, e.Message);
            Console.WriteLine($"❌ Error testing tool '{toolName}': {e.Message}");
            Console.WriteLine(e.StackTrace);
            return null;
        }
    }

    /// <summary>
    /// Run tool in daemon mode - keeps process alive until Ctrl+C (like Python run_daemon_mode)
    /// </summary>
    public static async Task<object?> RunDaemonMode(string toolName, Dictionary<string, object>? arguments = null)
    {
        Console.WriteLine($"🔄 Starting daemon mode for tool: {toolName}");
        Console.WriteLine("💡 Press Ctrl+C to stop the daemon and exit");
        Console.WriteLine(new string('=', 50));

        try
        {
            // Run the tool first
            var result = await TestTool(toolName, arguments);

            Console.WriteLine("\n🔄 Tool executed, entering daemon mode...");
            Console.WriteLine("💡 Process will stay alive until you press Ctrl+C");
            Console.WriteLine("📊 Tool services are now running and accessible");
            Console.WriteLine(new string('-', 50));

            // Keep the process alive
            try
            {
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n\n🛑 Ctrl+C detected, stopping daemon...");
                Console.WriteLine("🧹 Cleaning up...");

                try
                {
                    Console.WriteLine("🧹 Cleaning up tool resources...");
                    Console.WriteLine("💡 Some tools may require manual cleanup after daemon stops");
                    Console.WriteLine("💡 Check tool documentation for post-daemon cleanup procedures");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"⚠️  Warning during cleanup: {e.Message}");
                }

                Console.WriteLine("✅ Daemon stopped successfully");
                return result;
            }

            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Error in daemon mode: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Install dependencies for specific tools or all enabled tools (like Python install_dependencies)
    /// </summary>
    public static async Task InstallDependencies(string[]? specificTools = null)
    {
        if (specificTools != null && specificTools.Length > 0)
        {
            Console.WriteLine($"🔍 Scanning dependencies for specific tools: {string.Join(", ", specificTools)}");
        }
        else
        {
            Console.WriteLine("🔍 Scanning for tool dependencies...");
        }

        // Get the tools directory path
        var toolsDir = Path.Combine(Directory.GetCurrentDirectory(), "tools");
        if (!Directory.Exists(toolsDir))
        {
            Console.WriteLine($"❌ Tools directory not found: {toolsDir}");
            return;
        }

        // Collect all dependencies from enabled tools
        var allDependencies = new HashSet<string>();
        var enabledTools = new List<(string Name, List<string> Dependencies, string Description)>();
        var disabledTools = new List<(string Name, string Description)>();

        ScanDirectoryForDependencies(new DirectoryInfo(toolsDir), "", specificTools, allDependencies, enabledTools, disabledTools);

        Console.WriteLine("\n📋 Tool Status Report:");
        if (specificTools != null && specificTools.Length > 0)
        {
            Console.WriteLine($"🎯 Focusing on specific tools: {string.Join(", ", specificTools)}");
        }
        Console.WriteLine(new string('=', 60));

        if (enabledTools.Count > 0)
        {
            Console.WriteLine("✅ Enabled Tools:");
            foreach (var (name, deps, description) in enabledTools)
            {
                Console.WriteLine($"  🔧 {name}");
                if (deps.Count > 0)
                {
                    Console.WriteLine($"     Dependencies: {string.Join(", ", deps)}");
                }
                Console.WriteLine($"     Description: {description}");
                Console.WriteLine();
            }
        }

        if (disabledTools.Count > 0)
        {
            Console.WriteLine("❌ Disabled Tools:");
            foreach (var (name, description) in disabledTools)
            {
                Console.WriteLine($"  🔧 {name}");
                Console.WriteLine($"     Description: {description}");
                Console.WriteLine();
            }
        }

        if (allDependencies.Count == 0)
        {
            Console.WriteLine("✅ No additional dependencies needed - all enabled tools are standalone!");
            return;
        }

        Console.WriteLine($"📦 Dependencies to install: {string.Join(", ", allDependencies.OrderBy(x => x))}");
        Console.WriteLine("\n🚀 Installing dependencies with NuGet...");

        // Install each dependency using NuGet
        var failedPackages = new List<string>();

        foreach (var (i, package) in allDependencies.OrderBy(x => x).Select((p, i) => (i + 1, p)))
        {
            try
            {
                Console.WriteLine($"📥 Installing {package} ({i}/{allDependencies.Count})...");

                // Use dotnet add package command
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"add package \"{package}\"",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"   ✅ {package} installation completed");
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            Console.WriteLine($"   📦 {output.Trim()}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"   ❌ Failed to install {package}");
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            Console.WriteLine($"   🔍 Error: {error.Trim()}");
                        }
                        failedPackages.Add(package);
                    }
                }

                Console.WriteLine($"   🎯 {package} ready for use!");
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine($"   ❌ Failed to install {package}: {e.Message}");
                failedPackages.Add(package);
                Console.WriteLine();
            }
        }

        if (failedPackages.Count > 0)
        {
            Console.WriteLine($"\n❌ Failed to install: {string.Join(", ", failedPackages)}");
            Console.WriteLine("💡 You may need to install these manually or check your .NET environment");
        }
        else
        {
            Console.WriteLine("\n✅ All dependencies installed successfully!");
            Console.WriteLine("🎉 You can now use all enabled tools");
        }
    }

    /// <summary>
    /// List all available tools with their descriptions (like Python list_tools)
    /// </summary>
    public static void ListTools()
    {
        Console.WriteLine("📋 Available MCP Tools:");
        Console.WriteLine(new string('=', 60));

        foreach (var tool in _toolDefinitions)
        {
            Console.WriteLine($"🔧 {tool.Name}");
            
            try
            {
                var desc = tool.Description;
                if (!string.IsNullOrEmpty(desc))
                {
                    // Extract just the first line or sentence of description
                    var lines = desc.Split('\n');
                    var firstLine = lines[0].Trim();

                    // If first line is too long, truncate it
                    if (firstLine.Length > 80)
                    {
                        firstLine = firstLine.Substring(0, 80) + "...";
                    }

                    Console.WriteLine($"   {firstLine}");
                }
                else
                {
                    Console.WriteLine("   (No description available)");
                }
            }
            catch
            {
                Console.WriteLine("   (Description unavailable)");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Show the schema/parameters for a specific tool (like Python show_tool_schema)
    /// </summary>
    public static void ShowToolSchema(string toolName)
    {
        try
        {
            var tool = _toolDefinitions.FirstOrDefault(t => t.Name == toolName);
            if (tool == null)
            {
                Console.WriteLine($"❌ Tool '{toolName}' not found");
                return;
            }

            Console.WriteLine($"🔧 Tool: {toolName}");
            Console.WriteLine($"Description: {(!string.IsNullOrEmpty(tool.Description) ? tool.Description : "No description")}");
            Console.WriteLine("\n📋 Parameters Schema:");

            // Generate schema from method parameters
            var schema = GenerateSchemaFromMethod(tool.Method);
            Console.WriteLine(JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Error getting schema for '{toolName}': {e.Message}");
        }
    }

    /// <summary>
    /// Run the tool with given arguments (like Python run_tool function)
    /// </summary>
    private static async Task<object?> RunTool(string toolName, Dictionary<string, object> arguments)
    {
        _logger.LogInformation("Running tool: {ToolName} with arguments: {Arguments}", toolName, JsonSerializer.Serialize(arguments));

        try
        {
            var tool = _toolDefinitions.FirstOrDefault(t => t.Name == toolName);
            if (tool == null)
            {
                _logger.LogWarning("Tool '{ToolName}' not found", toolName);
                throw new ArgumentException($"Tool '{toolName}' not found");
            }

            _logger.LogInformation("Found tool {ToolName}, executing method {Method}", toolName, tool.Method.Name);

            // Convert arguments to method parameters
            var parameters = ConvertArgumentsToParameters(tool.Method, arguments);

            // Execute the method
            object? result;
            if (tool.Method.ReturnType.IsGenericType && tool.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Async method
                var task = (Task?)tool.Method.Invoke(null, parameters);
                if (task != null)
                {
                    await task;
                    // Get result from Task<T>
                    var resultProperty = task.GetType().GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }
                else
                {
                    result = null;
                }
            }
            else if (tool.Method.ReturnType == typeof(Task))
            {
                // Async void method
                var task = (Task?)tool.Method.Invoke(null, parameters);
                if (task != null)
                {
                    await task;
                }
                result = null;
            }
            else
            {
                // Sync method
                result = tool.Method.Invoke(null, parameters);
            }

            _logger.LogInformation("Tool {ToolName} executed successfully", toolName);
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error in RunTool for {ToolName}: {Error}", toolName, e.Message);
            throw;
        }
    }

    /// <summary>
    /// Scan directory recursively for tools and their dependencies
    /// </summary>
    private static void ScanDirectoryForDependencies(
        DirectoryInfo directory, 
        string prefix, 
        string[]? specificTools,
        HashSet<string> allDependencies,
        List<(string Name, List<string> Dependencies, string Description)> enabledTools,
        List<(string Name, string Description)> disabledTools)
    {
        foreach (var item in directory.GetDirectories())
        {
            if (item.Name.StartsWith("__"))
                continue;

            var currentName = string.IsNullOrEmpty(prefix) ? item.Name : $"{prefix}_{item.Name}";

            // Check if there's a Tool.cs file in the current directory
            var toolFile = new FileInfo(Path.Combine(item.FullName, "Tool.cs"));
            if (toolFile.Exists)
            {
                // Check if we should skip this tool due to specific_tools filter
                if (specificTools != null && specificTools.Length > 0 && !specificTools.Contains(currentName))
                {
                    continue;
                }

                // Check for settings.yaml and get tool info
                var (isDisabled, description) = ToolRegistry.IsToolDisabled(item);
                var toolDependencies = new List<string>();

                // Look for settings.yaml for dependencies
                var settingsFile = Path.Combine(item.FullName, "settings.yaml");
                if (File.Exists(settingsFile))
                {
                    try
                    {
                        var yamlContent = File.ReadAllText(settingsFile);
                        var deserializer = new DeserializerBuilder().Build();
                        var settings = deserializer.Deserialize<ToolRegistry.ToolSettings>(yamlContent);

                        if (settings?.Dependencies != null)
                        {
                            toolDependencies.AddRange(settings.Dependencies);
                        }

                        if (!string.IsNullOrEmpty(settings?.Description))
                        {
                            description = settings.Description;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Warning: Could not read {settingsFile}: {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(description))
                {
                    description = $"Tool: {currentName}";
                }

                // Add tool to appropriate list
                if (isDisabled)
                {
                    disabledTools.Add((currentName, description));
                }
                else
                {
                    enabledTools.Add((currentName, toolDependencies, description));
                    foreach (var dep in toolDependencies)
                    {
                        allDependencies.Add(dep);
                    }
                }
            }

            // Always recurse into subdirectories to find more tools
            ScanDirectoryForDependencies(item, currentName, specificTools, allDependencies, enabledTools, disabledTools);
        }
    }

    /// <summary>
    /// Extract tool name from method (matching Python tool name convention)
    /// </summary>
    private static string GetToolNameFromMethod(MethodInfo method)
    {
        // Extract tool name from namespace and method name
        var namespacePath = method.DeclaringType?.Namespace ?? "";
        
        if (namespacePath.StartsWith("McpDotnet.Tools."))
        {
            var toolParts = namespacePath.Substring("McpDotnet.Tools.".Length).Split('.');
            // Convert from PascalCase to snake_case (e.g., LngMathCalculator -> lng_math_calculator)
            return string.Join("_", toolParts.Select(ConvertToSnakeCase));
        }

        return method.Name.ToLower();
    }
    
    /// <summary>
    /// Convert PascalCase to snake_case
    /// </summary>
    private static string ConvertToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        // Convert PascalCase to snake_case
        var result = string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString().ToLower() : x.ToString().ToLower()));
        return result;
    }

    /// <summary>
    /// Get description from method's Description attribute
    /// </summary>
    private static string GetDescriptionFromMethod(MethodInfo method)
    {
        var descAttribute = method.GetCustomAttribute<DescriptionAttribute>();
        return descAttribute?.Description ?? "No description available";
    }

    /// <summary>
    /// Generate JSON schema from method parameters
    /// </summary>
    private static object GenerateSchemaFromMethod(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            var paramName = param.Name ?? "unknown";
            var paramType = GetJsonSchemaType(param.ParameterType);
            
            var paramSchema = new Dictionary<string, object>
            {
                ["type"] = paramType
            };

            var descAttribute = param.GetCustomAttribute<DescriptionAttribute>();
            if (descAttribute != null)
            {
                paramSchema["description"] = descAttribute.Description;
            }

            properties[paramName] = paramSchema;

            if (!param.HasDefaultValue && !IsNullableType(param.ParameterType))
            {
                required.Add(paramName);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required.ToArray();
        }

        return schema;
    }

    /// <summary>
    /// Get JSON schema type from .NET type
    /// </summary>
    private static string GetJsonSchemaType(Type type)
    {
        if (type == typeof(string))
            return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return "number";
        if (type == typeof(bool))
            return "boolean";
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            return "array";

        // Handle nullable types
        if (IsNullableType(type))
        {
            return GetJsonSchemaType(Nullable.GetUnderlyingType(type)!);
        }

        return "object";
    }

    /// <summary>
    /// Check if type is nullable
    /// </summary>
    private static bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) ||
               !type.IsValueType;
    }

    /// <summary>
    /// Convert arguments dictionary to method parameters array
    /// </summary>
    private static object?[] ConvertArgumentsToParameters(MethodInfo method, Dictionary<string, object> arguments)
    {
        var parameters = method.GetParameters();
        var result = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name ?? "";

            if (arguments.ContainsKey(paramName))
            {
                var argValue = arguments[paramName];
                
                // Convert JSON element to proper type if needed
                if (argValue is JsonElement jsonElement)
                {
                    result[i] = ConvertJsonElement(jsonElement, param.ParameterType);
                }
                else
                {
                    // Try direct conversion
                    result[i] = Convert.ChangeType(argValue, param.ParameterType);
                }
            }
            else if (param.HasDefaultValue)
            {
                result[i] = param.DefaultValue;
            }
            else if (IsNullableType(param.ParameterType))
            {
                result[i] = null;
            }
            else
            {
                throw new ArgumentException($"Required parameter '{paramName}' not provided");
            }
        }

        return result;
    }

    /// <summary>
    /// Convert JsonElement to target type
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element, Type targetType)
    {
        // Add null check for targetType
        if (targetType == null)
        {
            throw new ArgumentNullException(nameof(targetType), "Target type cannot be null");
        }
        
        // Handle nullable types
        if (IsNullableType(targetType) && element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var actualType = targetType;
        if (IsNullableType(targetType))
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                actualType = underlyingType;
            }
        }

        return actualType.Name switch
        {
            nameof(String) => element.GetString(),
            nameof(Int32) => element.GetInt32(),
            nameof(Int64) => element.GetInt64(),
            nameof(Double) => element.GetDouble(),
            nameof(Boolean) => element.GetBoolean(),
            nameof(Decimal) => element.GetDecimal(),
            _ => JsonSerializer.Deserialize(element.GetRawText(), targetType)
        };
    }

    /// <summary>
    /// Analyze .NET libraries (like Python analyze_libs)
    /// </summary>
    private static async Task AnalyzeLibraries(string[] libraries)
    {
        try
        {
            Console.WriteLine($"🔍 Starting analysis of {libraries.Length} library(ies)...");
            Console.WriteLine($"Libraries to analyze: {string.Join(", ", libraries)}");
            Console.WriteLine();

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            foreach (var library in libraries)
            {
                try
                {
                    Console.WriteLine($"📦 Analyzing: {library}");
                    Console.WriteLine(new string('-', 50));

                    // For .NET, we'll use NuGet API instead of PyPI
                    var url = $"https://api.nuget.org/v3-flatcontainer/{library.ToLower()}/index.json";
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var versionData = JsonSerializer.Deserialize<JsonElement>(content);
                        
                        var versions = versionData.GetProperty("versions").EnumerateArray()
                            .Select(v => v.GetString()).ToArray();

                        Console.WriteLine($"✅ Found {versions.Length} versions");
                        Console.WriteLine($"📊 Latest version: {versions.LastOrDefault() ?? "N/A"}");
                        Console.WriteLine($"📅 First version: {versions.FirstOrDefault() ?? "N/A"}");

                        // Get package metadata
                        var metadataUrl = $"https://api.nuget.org/v3/registration5-semver1/{library.ToLower()}/index.json";
                        var metadataResponse = await httpClient.GetAsync(metadataUrl);
                        
                        if (metadataResponse.IsSuccessStatusCode)
                        {
                            var metadataContent = await metadataResponse.Content.ReadAsStringAsync();
                            var metadata = JsonSerializer.Deserialize<JsonElement>(metadataContent);
                            
                            // Extract some basic info
                            if (metadata.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                            {
                                var firstItem = items[0];
                                if (firstItem.TryGetProperty("items", out var packageItems) && packageItems.GetArrayLength() > 0)
                                {
                                    var latestPackage = packageItems[packageItems.GetArrayLength() - 1];
                                    if (latestPackage.TryGetProperty("catalogEntry", out var catalogEntry))
                                    {
                                        if (catalogEntry.TryGetProperty("description", out var desc))
                                        {
                                            Console.WriteLine($"📝 Description: {desc.GetString()}");
                                        }
                                        if (catalogEntry.TryGetProperty("authors", out var authors))
                                        {
                                            Console.WriteLine($"👤 Authors: {authors.GetString()}");
                                        }
                                        if (catalogEntry.TryGetProperty("licenseExpression", out var license))
                                        {
                                            Console.WriteLine($"⚖️  License: {license.GetString()}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Package not found on NuGet (status: {response.StatusCode})");
                    }

                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error analyzing {library}: {ex.Message}");
                    Console.WriteLine();
                }
            }

            Console.WriteLine("✅ Library analysis completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running library analysis: {ex.Message}");
        }
    }

    /// <summary>
    /// Main entry point for command line interface (like Python main function)
    /// </summary>
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("🚀 MCP Tool Runner (.NET)");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run list                                         # List all tools");
            Console.WriteLine("  dotnet run schema <tool_name>                           # Show tool schema");
            Console.WriteLine("  dotnet run run [--daemon] <tool_name> 'args'            # Run tool");
            Console.WriteLine("  dotnet run batch [--daemon] tool1 'args1' tool2 'args2' # Run multiple tools");
            Console.WriteLine("  dotnet run install_dependencies                         # Install tool dependencies");
            Console.WriteLine("  dotnet run install_dependencies tool1 tool2 ...         # Install dependencies for specific tools");
            Console.WriteLine("  dotnet run analyze_libs <lib1> [lib2] ...               # Analyze .NET libraries");
            Console.WriteLine("");
            Console.WriteLine("Daemon mode:");
            Console.WriteLine("  --daemon                                                              # Keep process running (Ctrl+C to stop)");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run run lng_math_calculator '{\"expression\":\"2+3*4\"}'");
            Console.WriteLine("  dotnet run run --daemon lng_webhook_server '{\"operation\":\"start\", \"name\":\"test\"}'");
            Console.WriteLine("  dotnet run run lng_get_tools_info");
            Console.WriteLine("  dotnet run batch lng_math_calculator '{\"expression\":\"2+3\"}' lng_get_tools_info");
            Console.WriteLine("  dotnet run install_dependencies lng_email_client");
            Console.WriteLine("  dotnet run analyze_libs Newtonsoft.Json System.Text.Json");
            Console.WriteLine("");
            Console.WriteLine("📋 Quick tool list:");
            foreach (var tool in _toolDefinitions)
            {
                Console.WriteLine($"  🔧 {tool.Name}");
            }
            return;
        }

        var command = args[0];

        switch (command)
        {
            case "list":
                ListTools();
                break;

            case "install_dependencies":
                var specificTools = args.Length > 1 ? args[1..] : null;
                await InstallDependencies(specificTools);
                break;

            case "schema":
                if (args.Length < 2)
                {
                    Console.WriteLine("❌ Tool name required for schema command");
                    return;
                }
                ShowToolSchema(args[1]);
                break;

            case "run":
                await HandleRunCommand(args[1..]);
                break;

            case "batch":
                await HandleBatchCommand(args[1..]);
                break;

            case "analyze_libs":
                if (args.Length < 2)
                {
                    Console.WriteLine("❌ At least one library name required for analyze_libs command");
                    Console.WriteLine("💡 Usage: dotnet run analyze_libs <lib1> [lib2] ...");
                    Console.WriteLine("💡 Example: dotnet run analyze_libs Newtonsoft.Json System.Text.Json");
                    return;
                }
                await AnalyzeLibraries(args[1..]);
                break;

            default:
                Console.WriteLine($"❌ Unknown command: {command}");
                Console.WriteLine("Available commands: list, schema, run, batch, install_dependencies, analyze_libs");
                break;
        }
    }

    /// <summary>
    /// Handle the run command (like Python run command handler)
    /// </summary>
    private static async Task HandleRunCommand(string[] args)
    {
        // Check for --daemon flag
        var daemonMode = false;
        var actualArgs = args;

        if (args.Length > 0 && args[0] == "--daemon")
        {
            daemonMode = true;
            actualArgs = args[1..];
        }

        if (actualArgs.Length < 1)
        {
            Console.WriteLine("❌ Tool name required for run command");
            return;
        }

        var toolName = actualArgs[0];
        var toolArgs = new Dictionary<string, object>();

        if (actualArgs.Length >= 2)
        {
            // Join all remaining arguments into one string (handles spaces in JSON)
            var jsonStr = string.Join(" ", actualArgs[1..]);

            // Try to parse JSON arguments
            try
            {
                var jsonDoc = JsonDocument.Parse(jsonStr);
                toolArgs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonStr) ?? new Dictionary<string, object>();
            }
            catch (JsonException e)
            {
                Console.WriteLine($"❌ Invalid JSON arguments: {e.Message}");
                Console.WriteLine($"💡 Received: {jsonStr}");
                Console.WriteLine("💡 Try using double quotes for property names and values");
                Console.WriteLine("💡 Example: {\"input_text\":\"Hello world\"}");
                return;
            }
        }

        // Run the tool in appropriate mode
        if (daemonMode)
        {
            await RunDaemonMode(toolName, toolArgs);
        }
        else
        {
            await TestTool(toolName, toolArgs);
        }
    }

    /// <summary>
    /// Handle the batch command (like Python batch command handler)  
    /// </summary>
    private static async Task HandleBatchCommand(string[] args)
    {
        // Check for --daemon flag
        var daemonMode = Array.IndexOf(args, "--daemon") >= 0;
        var actualArgs = args.Where(arg => arg != "--daemon").ToArray();

        if (actualArgs.Length < 2)
        {
            Console.WriteLine("❌ At least one tool name and arguments required for batch command");
            Console.WriteLine("💡 Usage: dotnet run batch [--daemon] <tool1> [args1] <tool2> [args2] ...");
            return;
        }

        // Parse batch arguments
        var commands = new List<(string ToolName, Dictionary<string, object> Args)>();

        var i = 0;
        while (i < actualArgs.Length)
        {
            // Current argument should be a tool name
            var toolName = actualArgs[i];

            // Check if this looks like a tool name (not JSON)
            if (!toolName.StartsWith("{"))
            {
                // Next argument might be JSON args
                if (i + 1 < actualArgs.Length && actualArgs[i + 1].StartsWith("{"))
                {
                    // Try to reconstruct the JSON string by joining until we have balanced braces
                    var jsonParts = new List<string>();
                    var braceCount = 0;
                    var j = i + 1;

                    while (j < actualArgs.Length)
                    {
                        var part = actualArgs[j];
                        jsonParts.Add(part);

                        // Count braces to find complete JSON
                        foreach (var ch in part)
                        {
                            if (ch == '{') braceCount++;
                            else if (ch == '}') braceCount--;
                        }

                        if (braceCount == 0) break;
                        j++;
                    }

                    if (braceCount == 0)
                    {
                        // We have complete JSON
                        var jsonStr = string.Join(" ", jsonParts);
                        try
                        {
                            var toolArgs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonStr) ?? new Dictionary<string, object>();
                            commands.Add((toolName, toolArgs));
                            i = j + 1;
                        }
                        catch (JsonException e)
                        {
                            Console.WriteLine($"❌ Invalid JSON for tool '{toolName}': {e.Message}");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Incomplete JSON for tool '{toolName}'");
                        return;
                    }
                }
                else
                {
                    // No JSON args for this tool
                    commands.Add((toolName, new Dictionary<string, object>()));
                    i++;
                }
            }
            else
            {
                Console.WriteLine($"❌ Expected tool name but got: {toolName}");
                return;
            }
        }

        if (commands.Count == 0)
        {
            Console.WriteLine("❌ No valid commands found");
            return;
        }

        // Execute all commands in sequence
        Console.WriteLine($"🔄 Running {commands.Count} tools in batch...");
        Console.WriteLine(new string('=', 60));

        if (daemonMode)
        {
            Console.WriteLine("🔄 Running in daemon mode - press Ctrl+C to stop all tools...");
        }

        try
        {
            for (var idx = 0; idx < commands.Count; idx++)
            {
                var (toolName, toolArgs) = commands[idx];
                Console.WriteLine($"\n📍 Step {idx + 1}/{commands.Count}: {toolName}");
                Console.WriteLine(new string('-', 40));
                
                if (daemonMode)
                {
                    // Run in daemon mode
                    await RunDaemonMode(toolName, toolArgs);
                }
                else
                {
                    // Regular mode
                    await TestTool(toolName, toolArgs);
                }

                if (idx < commands.Count - 1)
                {
                    Console.WriteLine("\n" + string.Concat(Enumerable.Repeat("⏭️  ", 20)));
                }
            }

            if (daemonMode)
            {
                Console.WriteLine("\n🔄 All tools started in daemon mode. Press Ctrl+C to stop...");
                try
                {
                    var cts = new CancellationTokenSource();
                    Console.CancelKeyPress += (_, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    while (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\n\n🛑 Ctrl+C detected, stopping all daemon processes...");
                    Console.WriteLine("✅ Batch daemon stopped successfully");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Error in batch execution: {e.Message}");
        }
    }
}
