using Microsoft.Extensions.Logging;
using System.Text;
using System.Reflection;

namespace McpDotnet;

/// <summary>
/// Shared utilities for MCP logging components.
/// </summary>
internal static class ProjectUtils
{
    /// <summary>
    /// Dynamically determine the project root directory (.mcp-dotnet folder).
    /// </summary>
    /// <returns>Absolute path to the .mcp-dotnet directory</returns>
    public static string GetProjectRoot()
    {
        // Get the directory where the current assembly is located
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);
        
        if (string.IsNullOrEmpty(assemblyDir))
        {
            // Fallback: use current directory
            assemblyDir = Directory.GetCurrentDirectory();
        }

        // Look for .mcp-dotnet directory starting from assembly location and going up
        var currentDir = new DirectoryInfo(assemblyDir);
        while (currentDir != null)
        {
            // Check if current directory is .mcp-dotnet or contains .mcp-dotnet subdirectory
            if (currentDir.Name == ".mcp-dotnet" || 
                Directory.Exists(Path.Combine(currentDir.FullName, ".mcp-dotnet")))
            {
                return currentDir.Name == ".mcp-dotnet" 
                    ? currentDir.FullName 
                    : Path.Combine(currentDir.FullName, ".mcp-dotnet");
            }
            
            currentDir = currentDir.Parent;
        }

        // Ultimate fallback: use current directory
        return Directory.GetCurrentDirectory();
    }
}

/// <summary>
/// Logging configuration for MCP server components.
/// Provides unified logging setup for both server and tool runner modes.
/// </summary>
public static class LoggingConfig
{
    private static readonly Dictionary<string, ILogger> _loggers = new();
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Setup logging configuration for MCP server components.
    /// </summary>
    /// <param name="componentName">Name of the component (used for logger name and log file)</param>
    /// <param name="logLevel">Logging level (default: Debug)</param>
    /// <returns>Configured logger instance</returns>
    public static ILogger SetupLogging(string componentName, LogLevel logLevel = LogLevel.Debug)
    {
        if (_loggers.TryGetValue(componentName, out var existingLogger))
        {
            return existingLogger;
        }

        // Ensure logs directory exists in .mcp-dotnet project folder
        // Dynamically determine the project root based on current assembly location
        var projectRoot = ProjectUtils.GetProjectRoot();
        var logsDir = Path.Combine(projectRoot, "logs");
        Directory.CreateDirectory(logsDir);

        // Create logger factory if not exists - FILE ONLY for MCP stdio mode
        _loggerFactory ??= LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(logLevel)
                // NO CONSOLE LOGGING in MCP mode - stdout reserved for JSON-RPC
                .AddProvider(new FileLoggerProvider(logsDir));
        });

        var logger = _loggerFactory.CreateLogger(componentName);
        _loggers[componentName] = logger;

        // Log initialization
        logger.LogInformation("Logger initialized for component: {ComponentName}", componentName);
        logger.LogInformation("Log directory: {LogsDirectory}", logsDir);

        return logger;
    }

    /// <summary>
    /// Setup instance logger for specific tool instances with subdirectory.
    /// </summary>
    /// <param name="instanceName">Instance name</param>
    /// <param name="logSubdir">Log subdirectory</param>
    /// <param name="loggerPrefix">Optional logger prefix</param>
    /// <returns>Configured logger instance</returns>
    public static ILogger SetupInstanceLogger(string instanceName, string logSubdir, string? loggerPrefix = null)
    {
        var loggerName = string.IsNullOrEmpty(loggerPrefix) ? instanceName : $"{loggerPrefix}_{instanceName}";
        
        // Create subdirectory for instance logs in the same location as main logs
        var projectRoot = ProjectUtils.GetProjectRoot();
        var instanceLogsDir = Path.Combine(projectRoot, "logs", logSubdir);
        Directory.CreateDirectory(instanceLogsDir);

        var logger = SetupLogging(loggerName);
        logger.LogInformation("Instance logger setup for: {InstanceName} in {LogSubdir}", instanceName, logSubdir);
        
        return logger;
    }

    /// <summary>
    /// Close instance logger and cleanup resources.
    /// </summary>
    /// <param name="instanceName">Instance name</param>
    /// <param name="logSubdir">Log subdirectory</param>
    /// <param name="loggerPrefix">Optional logger prefix</param>
    public static void CloseInstanceLogger(string instanceName, string logSubdir, string? loggerPrefix = null)
    {
        var loggerName = string.IsNullOrEmpty(loggerPrefix) ? instanceName : $"{loggerPrefix}_{instanceName}";
        
        if (_loggers.TryGetValue(loggerName, out var logger))
        {
            logger.LogInformation("Closing instance logger for: {InstanceName}", instanceName);
            _loggers.Remove(loggerName);
        }
    }

    /// <summary>
    /// Dispose all logging resources.
    /// </summary>
    public static void Dispose()
    {
        _loggerFactory?.Dispose();
        _loggers.Clear();
    }
}

/// <summary>
/// Custom file logger provider for writing logs to files.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logsDirectory;
    private readonly Dictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string logsDirectory)
    {
        _logsDirectory = logsDirectory;
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (!_loggers.TryGetValue(categoryName, out var logger))
        {
            // All loggers write to the same mcp_server.log file for unified logging
            logger = new FileLogger(categoryName, _logsDirectory, "mcp_server.log");
            _loggers[categoryName] = logger;
        }
        return logger;
    }

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }
        _loggers.Clear();
    }
}

/// <summary>
/// File logger implementation for writing structured logs to files.
/// </summary>
public class FileLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly StreamWriter _fileWriter;
    private readonly object _lock = new();

    public FileLogger(string categoryName, string logsDirectory, string? logFileName = null)
    {
        _categoryName = categoryName;
        
        // Ensure we're using the correct logs directory
        var correctLogsDir = Path.Combine(ProjectUtils.GetProjectRoot(), "logs");
        if (logsDirectory != correctLogsDir)
        {
            // Override incorrect directory to prevent creating logs in wrong places
            logsDirectory = correctLogsDir;
        }
        
        // Ensure logs directory exists
        Directory.CreateDirectory(logsDirectory);
        
        // Use provided log file name or create one from category name
        string actualLogFileName;
        if (!string.IsNullOrEmpty(logFileName))
        {
            actualLogFileName = logFileName;
        }
        else
        {
            // Clean category name for file name (remove problematic characters and shorten if needed)
            var cleanCategoryName = categoryName
                .Replace("Microsoft.Extensions.Hosting.Internal.", "Hosting.")
                .Replace("ModelContextProtocol.Server.", "MCP.")
                .Replace("Microsoft.", "MS.")
                .Replace("..", ".");
            
            // Limit file name length to avoid path too long issues
            if (cleanCategoryName.Length > 50)
            {
                cleanCategoryName = cleanCategoryName.Substring(0, 47) + "...";
            }
            
            actualLogFileName = $"{cleanCategoryName}.log";
        }
        
        var logFilePath = Path.Combine(logsDirectory, actualLogFileName);
        
        try
        {
            // Create file stream with UTF-8 encoding and append mode
            // FileShare.ReadWrite allows multiple processes to access the same log file
            var fileStream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            // Fallback: if file creation fails, create a generic log file in correct directory
            var fallbackLogsDir = Path.Combine(ProjectUtils.GetProjectRoot(), "logs");
            Directory.CreateDirectory(fallbackLogsDir);
            var fallbackPath = Path.Combine(fallbackLogsDir, "mcp_fallback.log");
            var fileStream = new FileStream(fallbackPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
            
            // Log the error to the fallback file
            _fileWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - ERROR - Failed to create log for {categoryName ?? "unknown"}: {ex.Message}");
            _fileWriter.Flush();
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelString = logLevel.ToString().ToUpper();
        var message = formatter(state, exception);
        
        // Clean category name for display
        var displayCategory = _categoryName
            .Replace("Microsoft.Extensions.Hosting.Internal.", "Hosting.")
            .Replace("ModelContextProtocol.Server.", "MCP.")
            .Replace("Microsoft.", "MS.");
        
        // Format log entry similar to Python version with category
        var logEntry = $"{timestamp} - {displayCategory} - {levelString} - {message}";
        
        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        lock (_lock)
        {
            _fileWriter.WriteLine(logEntry);
        }
    }

    public void Dispose()
    {
        _fileWriter?.Dispose();
    }
}
