using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using McpDotnet;
using System.ComponentModel;
using DotNetEnv;
using System.Linq;
using System.Reflection;

// Ensure we're in the right directory (for .mcp-dotnet project)
var projectDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
if (!string.IsNullOrEmpty(projectDir))
{
    var mcpDotnetDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(projectDir))); // Go up from bin/Debug/net8.0
    if (mcpDotnetDir != null && Directory.Exists(mcpDotnetDir) && Path.GetFileName(mcpDotnetDir) == ".mcp-dotnet")
    {
        Directory.SetCurrentDirectory(mcpDotnetDir);
    }
}

// Load environment variables
Env.Load();

// Setup logging for server  
var logger = LoggingConfig.SetupLogging("mcp_server", LogLevel.Debug);

logger.LogInformation("Starting .NET MCP Server");
logger.LogInformation("Executable: {ExecutablePath}", Environment.ProcessPath);
logger.LogInformation(".NET version: {DotNetVersion}", Environment.Version);
logger.LogInformation("Current directory: {CurrentDirectory}", Environment.CurrentDirectory);
logger.LogInformation("Project root: {ProjectRoot}", Directory.GetCurrentDirectory());

try
{
    logger.LogInformation("All modules imported successfully");
    
    // Initialize the shared state with default values if needed
    var stateManager = StateManager.Instance;
    stateManager.Set("app_start_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    
    logger.LogInformation("Starting MCP server with stdio transport");
    
    // Create MCP server using Host builder (as per documentation)
    var builder = Host.CreateApplicationBuilder(args);
    
    // Configure logging - NO CONSOLE OUTPUT for clean MCP protocol
    builder.Logging.ClearProviders();
    // Only use our file-based logging in .mcp-dotnet/logs/ directory
    var mcpDotnetLogsDir = Path.Combine(Directory.GetCurrentDirectory(), ".mcp-dotnet", "logs");
    builder.Logging.AddProvider(new McpDotnet.FileLoggerProvider(mcpDotnetLogsDir));
    
    // Register and initialize tools using ToolRegistry
    logger.LogInformation("Scanning and registering tools...");
    ToolRegistry.RegisterTools();
    
    // Add MCP server services - use current assembly to find [McpServerTool] methods
    var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(currentAssembly);
    
    logger.LogInformation("MCP server configured successfully");
    
    // Log assembly scanning info
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    var toolMethods = assembly.GetTypes()
        .SelectMany(type => type.GetMethods())
        .Where(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0)
        .ToList();
    
    logger.LogInformation("Found {Count} methods with [McpServerTool] attribute:", toolMethods.Count);
    foreach (var method in toolMethods)
    {
        logger.LogInformation("  - {Type}.{Method}", method.DeclaringType?.Name, method.Name);
    }
    
    // Add explicit MCP handlers like in Python version
    builder.Services.Configure<Microsoft.Extensions.Logging.LoggerFilterOptions>(options =>
    {
        // Enable debug logging for MCP to see all requests
        options.AddFilter("ModelContextProtocol", LogLevel.Debug);
        options.AddFilter("Microsoft.Extensions.Hosting", LogLevel.Information);
    });
    logger.LogInformation("Phase 1: Basic Infrastructure - COMPLETE ✅");  
    logger.LogInformation("Phase 2: MCP Server Core - COMPLETE ✅");
    logger.LogInformation("Phase 3: Tool Registry System - COMPLETE ✅");
    logger.LogInformation("Starting MCP server...");
    
    // Build and start the MCP server
    using var host = builder.Build();
    logger.LogInformation("MCP server built successfully, starting async run...");
    await host.RunAsync();
}
catch (Exception e)
{
    logger.LogError(e, "Unhandled exception in main: {ErrorMessage}", e.Message);
    throw;
}
finally
{
    LoggingConfig.Dispose();
}