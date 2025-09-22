# .NET MCP Server Implementation

## Overview
Create a complete .NET 8 Console Application that mirrors the functionality of the Python MCP server (`mcp_server/`) with identical API compatibility, tool structure, and operational behavior.

## Technical Specification

### Target Platform
- **.NET 8** Console Application
- **Single .csproj file** in `.mcp-dotnet/` root
- **Root Namespace**: `McpDotnet`
- **Cross-platform** compatibility (Windows/Linux/macOS)

### Project Structure
```
.mcp-dotnet/
├── Program.cs                    # Entry point (analog of server.py)
├── ToolRegistry.cs              # Tool registration and management 
├── LoggingConfig.cs             # Logging configuration (analog of logging_config.py)
├── StateManager.cs              # Application state management
├── Run.cs                       # CLI tool testing (analog of run.py)
├── McpDotnet.csproj            # Project file
├── logs/                        # Logs (separate from Python version)
│   ├── mcp_server.log
│   ├── mcp_in.log
│   └── mcp_out.log
└── tools/
    └── lng_math_calculator/
        ├── Tool.cs              # Tool implementation
        └── settings.yaml        # Configuration (identical to Python version)
```

### NuGet Dependencies
```xml
<PackageReference Include="ModelContextProtocol" Version="*-*" />
<PackageReference Include="DotNetEnv" Version="3.1.0" />
<PackageReference Include="YamlDotNet" Version="15.1.2" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />
```

### Key Components

#### 1. Program.cs (analog of server.py)
- **Stdio transport** for MCP protocol only
- **MCP message logging** (incoming and outgoing streams)
- **Tool processing** via ToolRegistry
- **LoggingReadStream/LoggingWriteStream** analogs for message interception
- **Click-style CLI** (basic, no --port, --transport initially)

#### 2. ToolRegistry.cs (analog of tool_registry.py)  
- **Automatic directory scanning** of tools/ folder with recursive traversal
- **settings.yaml support** for enabling/disabling tools and tool groups
- **Reflection-based tool registration** with ITool interface
- **Async methods**: `GetToolInfoAsync()`, `RunToolAsync()`, `ListToolsAsync()`
- **Tool validation** and dependency checking
- **Problem imports handling** for tools with special requirements

#### 3. LoggingConfig.cs (analog of logging_config.py)
- **ILogger integration** with flexible configuration and custom setup
- **File logging** to `.mcp-dotnet/logs/` with UTF-8 encoding
- **Structured logs** with detailed timestamps and context
- **Public methods**: `SetupLogging()`, `SetupInstanceLogger()`, `CloseInstanceLogger()`
- **MCP mode detection** (server vs tool runner) 
- **Console output control** based on execution context

#### 4. lng_math_calculator Tool
- **Identical JSON schema** and API interface to Python version
- **Same mathematical functions**: sin, cos, tan, log, ln, sqrt, abs, floor, ceil, round
- **Same operators**: +, -, *, /, ** (power), ^ (power alias), //, %
- **Same constants**: pi, e with decimal precision
- **Safe expression evaluation** without code injection risks
- **Expression cleaning**: ^ to **, constant substitution, function replacement
- **Error handling** with detailed messages and validation
- **Result formatting**: JSON with original/cleaned expressions and result metadata

### API Compatibility

#### MCP Protocol Compliance
- **Identical JSON-RPC 2.0** message format with Python version
- **Same tool/list response** structure and content
- **Same tool/call request/response** format
- **Compatible error handling** with proper error codes

#### Tool Info Response (JSON)
```json
{
  "name": "lng_math_calculator",
  "description": "Performs mathematical calculations and operations....",
  "schema": {
    "type": "object",
    "properties": {
      "expression": {
        "type": "string", 
        "description": "Mathematical expression for calculation"
      }
    },
    "required": ["expression"]
  }
}
```

#### Tool Execution Result (JSON)
```json
{
  "originalExpression": "2 + 3 * 4",
  "cleanedExpression": "2 + 3 * 4", 
  "result": 14,
  "resultType": "Int32"
}
```

#### 5. StateManager.cs (analog of state_manager.py)
- **Singleton pattern** for shared application state
- **Thread-safe** operations with concurrent access support
- **Methods**: `Get()`, `Set()`, `Delete()`, `Has()`, `GetAll()`, `Clear()`
- **Generic type support** for strongly typed state values
- **Memory-based storage** (not file-based like FileStateManager)

### Configuration and Settings

#### settings.yaml (identical to Python version)
```yaml
enabled: true
description: "Mathematical calculator with support for complex expressions and functions" 
dependencies: []
```

#### .env File Configuration
- **Reading from project root** (shared with Python version)
- **DotNetEnv library** for environment variable loading
- **Same environment variables** as Python version (API keys, configuration, etc.)
- **Automatic .env discovery** from current directory upwards

### Logging System

#### Log Structure
```
.mcp-dotnet/logs/
├── mcp_server.log               # Main server logs
├── mcp_in.log                   # Incoming MCP messages  
└── mcp_out.log                  # Outgoing MCP messages
```

#### Log Format (identical to Python version)
```
2025-09-22 15:30:45.123 - McpServer - INFO - [Program.cs:45] - Main() - Starting MCP server with stdio transport
2025-09-22 15:30:45.456 - McpServer - INFO - [<] {"jsonrpc":"2.0","id":1,"method":"tools/list"}
2025-09-22 15:30:45.789 - McpServer - INFO - [>] {"jsonrpc":"2.0","id":1,"result":{...}}
```

#### Logging Features
- **UTF-8 encoding** for international character support
- **Detailed timestamps** with millisecond precision
- **File and line information** for debugging
- **Automatic log directory** creation
- **Async logging** to prevent I/O blocking

### CLI Interface (Run.cs)

#### Commands (analog of run.py)
```bash
# List all tools
dotnet run -- list

# Show tool schema  
dotnet run -- schema lng_math_calculator

# Run single tool
dotnet run -- run lng_math_calculator "{\"expression\":\"2+3*4\"}"

# Install dependencies (if any)
dotnet run -- install-dependencies

# Batch tool execution
dotnet run -- batch lng_math_calculator "{\"expression\":\"2+3\"}" lng_count_words "{\"input_text\":\"test\"}"

# Daemon mode (future)
dotnet run -- run --daemon lng_math_calculator "{\"expression\":\"sin(pi/2)\"}"
```

### Architectural Principles

#### 1. Python Architecture Mirroring
- **Same class structure** and method signatures
- **Same logging patterns** and error handling approaches  
- **Same tool registration logic** and directory scanning
- **Same MCP protocol implementation** with logging wrappers

#### 2. .NET Best Practices
- **Async/await** for all I/O operations and network calls
- **ILogger** for structured logging with categories
- **Configuration** system integration for settings
- **Dependency Injection** readiness for future extensibility
- **Nullable reference types** for better null safety

#### 3. Full API Compatibility
- **Identical JSON schemas** for all tools and responses
- **Same input parameters** and output formats
- **Compatible configuration files** (settings.yaml, .env)
- **Same error messages** and exception handling behavior

## Implementation Plan

### Phase 1: Basic Infrastructure
1. ☐ Create .NET 8 Console App project
2. ☐ Add NuGet dependencies (ModelContextProtocol, DotNetEnv, YamlDotNet)
3. ☐ Set up basic folder structure (tools/, logs/)
4. ☐ Implement LoggingConfig.cs with ILogger integration
5. ☐ Implement StateManager.cs with singleton pattern

### Phase 2: MCP Server Core  
1. ☐ Implement Program.cs with stdio transport
2. ☐ Basic MCP message logging (LoggingReadStream/LoggingWriteStream)
3. ☐ Integration with ModelContextProtocol SDK
4. ☐ Handle tools/list and tools/call methods
5. ☐ Exception handling and error responses

### Phase 3: Tool Registry System
1. ☐ Implement ToolRegistry.cs with automatic tool discovery
2. ☐ Recursive directory scanning of tools/ folder
3. ☐ settings.yaml parsing with YamlDotNet  
4. ☐ Reflection-based tool loading with ITool interface
5. ☐ Tool validation and dependency checking

### Phase 4: Math Calculator Tool
1. ☐ Create lng_math_calculator/Tool.cs implementing ITool
2. ☐ Implement all mathematical operations (sin, cos, sqrt, etc.)
3. ☐ Safe expression evaluation without code injection
4. ☐ Identical JSON schema to Python version
5. ☐ settings.yaml configuration matching Python

### Phase 5: CLI Runner (Optional)
1. ☐ Implement Run.cs for command-line testing
2. ☐ Commands: list, schema, run, batch, install-dependencies
3. ☐ JSON arguments parsing and validation  
4. ☐ Integration with ToolRegistry for tool execution
5. ☐ Daemon mode support (future enhancement)

### Phase 6: Testing and Validation
1. ☐ Unit tests for all components (NUnit or xUnit)
2. ☐ Integration tests for MCP protocol compliance
3. ☐ Output comparison with Python version
4. ☐ Performance benchmarking and optimization

## Success Criteria

### Functional Requirements
- ☐ **Full API compatibility** with Python MCP server
- ☐ **Identical results** for lng_math_calculator execution  
- ☐ **Stdio MCP transport** working correctly with message logging
- ☐ **Detailed logging** matching Python version format and structure
- ☐ **settings.yaml support** for enabling/disabling tools and groups
- ☐ **Environment configuration** via shared .env file

### Technical Requirements  
- ☐ **.NET 8** compatibility and cross-platform support
- ☐ **Async/await** for all I/O operations and network calls
- ☐ **Structured file logging** with UTF-8 encoding and proper formatting
- ☐ **Exception handling** with detailed error messages and proper MCP error codes
- ☐ **Memory management** without leaks and proper resource disposal
- ☐ **Performance** comparable to Python version (startup time, response time)

### Quality Requirements
- ☐ **Readable code** with comprehensive XML documentation comments
- ☐ **Consistent .NET coding style** following Microsoft conventions
- ☐ **Usage documentation** with examples and integration guides
- ☐ **Test coverage** >80% with unit and integration tests

## Future Enhancements (Phase 7+)

### Functionality Extensions
- **SSE transport** (Server-Sent Events) matching Python version capabilities
- **Additional tools** porting from Python version (lng_batch_run, lng_http_client, etc.)
- **Hot reload** of settings.yaml configuration without server restart
- **Metrics and monitoring** with performance tracking and health checks
- **Native AOT compilation** for faster startup and smaller footprint

### Integration Capabilities
- **Docker container** for deployment with multi-stage builds
- **GitHub Actions** CI/CD pipeline with automated testing
- **NuGet package** for reusable components and tool development
- **Visual Studio Extension** for MCP tool development and debugging
- **gRPC transport** as alternative to stdio/SSE

## System Requirements

### Host Machine Prerequisites

To compile and run this .NET MCP server implementation, install the following:

#### 1. .NET 8 SDK
```bash
# Windows (via winget)
winget install Microsoft.DotNet.SDK.8

# Or download from: https://dotnet.microsoft.com/download/dotnet/8.0
```

#### 2. Visual Studio Code (Recommended)
```bash
# Windows (via winget)
winget install Microsoft.VisualStudioCode
```

#### 3. C# Extension for VS Code
```bash
# Install via VS Code Extensions marketplace
# Extension ID: ms-dotnettools.csharp
```

#### 4. Git (if not already installed)
```bash
# Windows (via winget)
winget install Git.Git
```

### Verification Commands
After installation, verify your setup:
```bash
# Check .NET SDK version (should be 8.0.x or higher)
dotnet --version

# Check available .NET runtimes
dotnet --list-runtimes

# Create test project to verify functionality
dotnet new console -n test-dotnet
cd test-dotnet
dotnet run
cd ..
rmdir /s test-dotnet
```

### IDE Integration Setup
For VS Code MCP integration:
1. Install C# extension in VS Code
2. Configure `.cursor/mcp.json` or `.vscode/settings.json` with:
```json
{
  "mcpServers": {
    "langchain-mcp-dotnet": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "{workspaceFolder}/.mcp-dotnet"]
    }
  }
}
```

---

**Priority:** High  
**Time Estimate:** 6-10 days development  
**Complexity:** Medium-High  
**Dependencies:** ModelContextProtocol C# SDK, .NET 8 Runtime, YamlDotNet, DotNetEnv
