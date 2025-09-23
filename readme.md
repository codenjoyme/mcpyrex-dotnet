# MCP .NET Implementation

.NET port of the Model Context Protocol (MCP) server with pipeline architecture and tools.

## 🚀 Two Execution Modes

This implementation provides two distinct ways to run the tools:

1. **📡 MCP Server Mode** (`mcp.csproj`) - Full MCP protocol server for VS Code integration
   - Uses ModelContextProtocol 0.3.0
   - Includes Server.cs for MCP communication
   - Integrates with VS Code Copilot and other MCP clients
   - Provides protocol-compliant tool discovery and execution

2. **🔧 Standalone CLI Mode** (`run.csproj`) - Direct command-line tool execution
   - Uses ModelContextProtocol.Sdk 0.8.0
   - Excludes Server.cs (no protocol overhead)
   - Direct tool execution via Run.cs
   - Perfect for scripting and automation

Both projects share the same core pipeline engine and tool implementations, ensuring identical behavior regardless of execution mode.

## Project Structure

```
.mcp-dotnet/
├── 📋 Project Files
│   ├── mcp.csproj      # MCP Server project (ModelContextProtocol 0.3.0)
│   └── run.csproj              # Standalone CLI project (ModelContextProtocol.Sdk 0.8.0)
│
├── 🚀 Entry Points
│   ├── Program.cs              # MCP Server main entry
│   ├── Server.cs               # MCP protocol implementation (excluded from CLI)
│   └── Run.cs                  # CLI tool runner (excluded from MCP server)
│
├── Pipeline/                    # Pipeline execution engine
│   ├── Models.cs               # Data models and types
│   ├── Expressions.cs          # .NET expression evaluator
│   ├── Pipeline.cs             # Main pipeline wrapper
│   ├── README.md              # Pipeline documentation
│   └── Strategies/            # Execution strategies
│       ├── Base.cs            # Abstract base strategy
│       ├── Tool.cs            # Tool execution
│       ├── Conditional.cs     # If-then-else logic
│       ├── Loop.cs            # Iteration strategies
│       ├── Parallel.cs        # Concurrent execution
│       ├── Delay.cs           # Timing control
│       └── Executor.cs        # Main orchestrator
│
├── tools/                     # MCP tools implementation
│   ├── lng_batch_run/         # Batch pipeline executor
│   │   ├── LngBatchRunTool.cs
│   │   ├── PipelineResultExtensions.cs
│   │   ├── LngBatchRunExample.cs
│   │   ├── LngBatchRunToolTests.cs
│   │   ├── example_pipeline.json
│   │   └── README.md
│   ├── lng_get_tools_info/    # Tool information utility
│   └── lng_math_calculator/   # Mathematical calculator
│
├── Program.cs                 # Main entry point
├── Server.cs                  # MCP server implementation
├── StateManager.cs            # State management
├── ToolRegistry.cs            # Tool registration
├── LoggingConfig.cs           # Logging configuration
└── *.csproj                   # Project files
```

## Key Features

### 🚀 Complete Pipeline System
- **Strategy-based architecture** - Modular and extensible
- **Full Python compatibility** - Same API and JSON schemas
- **.NET expressions** - C# syntax instead of JavaScript
- **Async/await throughout** - Modern .NET patterns

### 🛠️ Tool Implementation
- **lng_batch_run** - Complex pipeline executor with all Python features
- **lng_get_tools_info** - Tool metadata and information
- **lng_math_calculator** - Mathematical expression evaluator
- **Easy extensibility** - Add new tools following established patterns

### 🔧 Expression System
Converts Python/JavaScript expressions to .NET equivalents:

| Python/JS | .NET | Description |
|-----------|------|-------------|
| `variable \|\| 'default'` | `variable ?? "default"` | Null-coalescing |
| `variable.length` | `variable.Length` | Property access |
| `JSON.stringify(obj)` | `JsonSerializer.Serialize(obj)` | Serialization |
| `variable ? a : b` | `variable ? a : b` | Ternary (same) |

### 📊 Pipeline Strategies

#### Tool Execution
```json
{
  "tool": "lng_math_calculator",
  "params": {"expression": "2 + 2"},
  "output": "result"
}
```

#### Conditional Logic
```json
{
  "type": "condition",
  "condition": "{! result.value > 5 !}",
  "then": [...],
  "else": [...]
}
```

#### Loops
```json
{
  "type": "forEach",
  "forEach": "{! collection !}",
  "item": "current",
  "do": [...]
}
```

#### Parallel Execution
```json
{
  "type": "parallel",
  "maxConcurrent": 5,
  "parallel": [...]
}
```

## Getting Started

### Prerequisites
- .NET 8.0 or later
- Visual Studio 2022 or VS Code

### Building Both Projects
```bash
cd .mcp-dotnet

# Build MCP Server (for VS Code integration)
dotnet build mcp.csproj

# Build Standalone CLI (for direct execution)
dotnet build run.csproj
```

### Running

#### 🔧 Standalone CLI Mode
```bash
# Direct tool execution
dotnet run --project run.csproj -- lng_batch_run --pipeline config/example_pipeline.json

# Interactive mode
dotnet run --project run.csproj
```

#### 📡 MCP Server Mode
```bash
# Start MCP server (used by VS Code automatically)
dotnet run --project mcp.csproj
```

### Running Tests
```bash
# Run all tests (includes 50 mirrored Python tests)
dotnet test mcp.csproj --verbosity minimal
```

## Usage Examples

### Basic Pipeline
```csharp
var executor = new StrategyBasedExecutor(toolRunner);
var config = new Dictionary<string, object>
{
    ["pipeline"] = new List<Dictionary<string, object>>
    {
        new()
        {
            ["tool"] = "lng_math_calculator",
            ["params"] = new Dictionary<string, object> { ["expression"] = "5 * 8" },
            ["output"] = "math_result"
        }
    }
};

var result = await executor.ExecuteAsync(config);
```

### Using lng_batch_run Tool
```csharp
var batchTool = new LngBatchRunTool(toolRunner, logger);
var arguments = new Dictionary<string, object>
{
    ["pipeline"] = pipelineSteps,
    ["context_fields"] = new[] { "result", "stats" }
};

var result = await batchTool.RunToolAsync("lng_batch_run", arguments);
```

### File-based Pipelines
```csharp
var arguments = new Dictionary<string, object>
{
    ["pipeline_file"] = "config/my_pipeline.json"
};

var result = await batchTool.RunToolAsync("lng_batch_run", arguments);
```

## Architecture Benefits

### 🏗️ Strategy Pattern
- **Modular design** - Each strategy handles specific functionality
- **Easy extension** - Add new step types without modifying existing code
- **Composition over inheritance** - Flexible strategy combinations

### 🔄 Compatibility
- **API compatible** with Python version
- **Same JSON schemas** for pipeline definitions
- **Identical behavior** for existing pipelines
- **Migration path** from Python to .NET

### ⚡ Performance
- **Async/await** throughout for non-blocking operations
- **Strong typing** with compile-time checks
- **Memory efficient** with proper disposal patterns
- **Concurrent execution** where appropriate

### 🛡️ Reliability
- **Comprehensive error handling** with detailed messages
- **Logging integration** using Microsoft.Extensions.Logging
- **Unit tests** for all major components
- **Type safety** prevents many runtime errors

## Integration with Python Version

The .NET implementation maintains full compatibility:

1. **Pipeline Definitions** - Same JSON structure and schemas
2. **Expression Syntax** - Adapted to .NET with documented changes
3. **Tool Interface** - Compatible parameter and result formats
4. **Error Handling** - Same error message formats and codes

This allows:
- **Gradual migration** from Python to .NET
- **Mixed deployments** with both versions
- **Shared pipeline configurations** between implementations
- **Cross-platform development** and deployment

## Contributing

When adding new tools or strategies:

1. Follow the established patterns in existing code
2. Add comprehensive unit tests
3. Update documentation and examples
4. Maintain compatibility with Python version where possible

## Dependencies

- **System.Text.Json** - JSON serialization
- **Microsoft.Extensions.Logging** - Logging framework
- **Microsoft.Extensions.DependencyInjection** - DI container
- **System.Threading.Tasks** - Async operations

## Performance Considerations

- Uses async/await for I/O operations
- Implements proper disposal patterns
- Minimizes memory allocations in hot paths
- Leverages .NET's JIT compilation advantages

This .NET implementation provides a robust, high-performance alternative to the Python version while maintaining full compatibility and extending the platform's capabilities.
