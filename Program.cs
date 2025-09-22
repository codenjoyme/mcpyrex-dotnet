using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using McpDotnet;
using System.ComponentModel;

// Setup logging for server  
var logger = LoggingConfig.SetupLogging("mcp_server", LogLevel.Debug);

logger.LogInformation("Starting .NET MCP Server");
logger.LogInformation("Python executable: {PythonExecutable}", Environment.ProcessPath);
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
    
    // Add MCP server services
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(); // This will find our [McpServerTool] methods
    
    logger.LogInformation("MCP server configured successfully");
    logger.LogInformation("Phase 1: Basic Infrastructure - COMPLETE ✅");  
    logger.LogInformation("Phase 2: MCP Server Core - COMPLETE ✅");
    logger.LogInformation("Starting MCP server...");
    
    // Start the MCP server
    await builder.Build().RunAsync();
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

/// <summary>
/// Test tools for MCP server functionality
/// </summary>
public static class TestTools
{
    /// <summary>
    /// Simple test tool that echoes back the input message
    /// </summary>
    /// <param name="message">The message to echo</param>
    /// <returns>Echo response</returns>
    [McpServerTool]
    [Description("Echoes back the provided message for testing MCP functionality")]
    public static string EchoTool(
        [Description("The message to echo back")] string message = "Hello from .NET MCP Server!")
    {
        return $"Echo: {message} (from .NET at {DateTime.Now})";
    }

    /// <summary>
    /// Get server information
    /// </summary>
    /// <returns>Server status information</returns>
    [McpServerTool]
    [Description("Returns information about the .NET MCP server")]
    public static object GetServerInfo()
    {
        var stateManager = StateManager.Instance;
        
        return new
        {
            ServerName = ".NET MCP Server",
            Version = "1.0.0",
            DotNetVersion = Environment.Version.ToString(),
            StartTime = stateManager.Get<string>("app_start_time"),
            CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Phase1Status = "COMPLETE ✅",
            Phase2Status = "COMPLETE ✅",
            UpcomingPhases = new[] { "Phase 3: Tool Registry System", "Phase 4: Math Calculator Tool" }
        };
    }
}
