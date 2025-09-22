using Microsoft.Extensions.Logging;
using McpDotnet;

// Setup logging for server
var logger = LoggingConfig.SetupLogging("mcp_server", LogLevel.Debug);

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
    
    // TODO: Implement MCP server setup
    logger.LogInformation("Server instance created");
    
    logger.LogInformation("MCP server setup completed - ready for Phase 2 implementation");
    Console.WriteLine("Hello from .NET MCP Server!");
    Console.WriteLine("Phase 1: Basic Infrastructure - COMPLETE ✅");
    Console.WriteLine("Ready for Phase 2: MCP Server Core implementation");
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
