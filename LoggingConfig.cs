using Microsoft.Extensions.Logging;
using System.Text;

namespace McpDotnet;

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

        // Ensure logs directory exists
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        Directory.CreateDirectory(logsDir);

        // Create logger factory if not exists
        _loggerFactory ??= LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(logLevel)
                .AddConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                })
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
        
        // Create subdirectory for instance logs
        var instanceLogsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs", logSubdir);
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
            logger = new FileLogger(categoryName, _logsDirectory);
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

    public FileLogger(string categoryName, string logsDirectory)
    {
        _categoryName = categoryName;
        
        // Create log file path
        var logFileName = $"{categoryName}.log";
        var logFilePath = Path.Combine(logsDirectory, logFileName);
        
        // Create file stream with UTF-8 encoding and append mode
        var fileStream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
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
        
        // Format log entry similar to Python version
        var logEntry = $"{timestamp} - {_categoryName} - {levelString} - {message}";
        
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
