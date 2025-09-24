# Extending GitHub Copilot with .Net via MCP

This project demonstrates how to extend `GitHub Copilot` capabilities using `.Net` and the `Model Context Protocol (MCP)`, allowing for more deterministic and powerful interactions with the LLM.

## Project Overview

As a `GitHub Copilot` trainer, I've observed its impressive evolution. However, I've always wanted to access GitHub Copilot's internals to build more complex transformation chains. This project shows how to achieve that goal by leveraging MCP (Model Context Protocol) and Langchain.

While `GitHub Copilot` [repository custom instructions](https://docs.github.com/en/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot) feature improved customization options, the introduction of the [MCP protocol](https://docs.github.com/en/copilot/using-github-copilot/coding-agent/extending-copilot-coding-agent-with-mcp) opened new possibilities for extending Copilot's functionality through custom `MCP` servers.

## The Problem

Instruction files are not always deterministic - they need to be fine-tuned when new LLM versions are released to reduce hallucinations. LLMs often struggle with precise text manipulations, sometimes creatively reinterpreting tasks and adding unwanted artifacts. What we need is a way to inject deterministic logic into our instructions.

## The Solution

This project demonstrates a solution through:

1. `.Net` scripts installed on the local machine.
2. Custom `MCP` server configuration through `../.vscode/mcp.json`
3. Custom tools defined in the `./tools` directory

With this setup, `GitHub Copilot` gains access to new, well-documented tools that it can see as part of your project. When you ask Copilot to "create a tool that does X", it can generate a solution very close to what you need. You simply accept its changes and restart MCP to get a new deterministic tool for your specific logic.

## Data Security

⚠️ **Important**: Please review the [Security Disclaimer](security-disclaimer.md) before using this tool in production environments.

This document outlines potential data leak scenarios and provides guidance on risk mitigation when working with this tool. 

- **Install file** `./build/install.sh`: 
  - Setup this extension inside project
  - Setup `.Net` 
  - Setup libraties

- **mcp.json**
  ```json
    {
      "servers": {
        "mcpyrex-dotnet": {
            "type": "stdio", 
            "command": "${workspaceFolder}\\.dotnet\\dotnet.exe",
            "args": ["run", "--project", "${workspaceFolder}\\.mcp-dotnet\\mcp.csproj"]
        },
      }
    }
  ```

- **Custom Tools**:
  - `lng_batch_run` - advanced pipeline execution with conditionals, loops, and parallel processing
  - `lng_count_words` - word counting, demonstrates python function calling
  - `lng_get_tools_info` - tools information retrieval, collects all the information about tools in one place, that helps in `Github Copilot`.
  - And more in `./tools/`

## Getting Started

1. Run this command in the root folder of your project and follow instructions:

**PowerShell (Windows) - One-line installation:**
```powershell
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/mcpyrex/mcpyrex-dotnet/main/build/bootstrap.ps1" -OutFile "bootstrap.ps1"; .\bootstrap.ps1; Remove-Item "bootstrap.ps1"
```

**Alternative - Direct execution without downloading:**
```powershell
iex (Invoke-WebRequest -Uri "https://raw.githubusercontent.com/mcpyrex/mcpyrex-dotnet/main/build/bootstrap.ps1").Content
```

This bootstrap script will:
- Download the latest version from GitHub to `./.mcp-dotnet` folder
- Extract all files
- Navigate to the build directory  
- Run the interactive installation script automatically

2. The installation script will automatically:
   - Detect or install .NET SDK 8.0
   - Ask you to choose between Cursor or VSCode
   - Copy appropriate configuration files
   - Restore NuGet packages for both projects
   - Set up your MCP server integration

3. After installation:
   - Your `.vscode/mcp.json` or `.cursor/mcp.json` will be configured
   - Your workspace settings will be updated
   - GitHub Copilot instructions will be in place  
   - Start using enhanced GitHub Copilot capabilities!

### Alternative Installation Methods

**One-line installation (copy-paste friendly):**
```powershell
$work = ".mcp-dotnet"; $url = "https://github.com/mcpyrex/mcpyrex-dotnet/archive/refs/heads/main.zip"; New-Item -ItemType Directory -Force -Path $work; Invoke-WebRequest -Uri $url -OutFile "$work\project.zip"; Expand-Archive -Path "$work\project.zip" -DestinationPath "$work\tmp"; Remove-Item "$work\project.zip"; Move-Item "$work\tmp\mcpyrex-dotnet-main\*" "$work"; Move-Item "$work\tmp\mcpyrex-dotnet-main\.*" "$work" -Force; Remove-Item "$work\tmp" -Recurse; Set-Location "$work\build"; .\install.ps1
```

**Manual installation:**
```powershell
# 1. Download and extract
git clone https://github.com/mcpyrex/mcpyrex-dotnet.git .mcp-dotnet
cd .mcp-dotnet\build

# 2. Run installation
.\install.ps1
```

**Update existing installation:**
```powershell
# Navigate to existing installation and update
cd .mcp-dotnet\build; .\install.ps1
```

## MCP Configuration

### Enabling/Disabling MCP in VS Code

To control whether MCP (Model Context Protocol) is enabled or disabled, you need to modify the `${workspaceFolder}/.vscode/settings.json` file:

#### To Enable MCP:
```json
{
    "chat.mcp.enabled": true,
    "github.copilot.chat.codeGeneration.useInstructionFiles": false
}
```

#### To Disable MCP:
```json
{
    "chat.mcp.enabled": false,
    "github.copilot.chat.codeGeneration.useInstructionFiles": true
}
```

## API Keys

API keys and other credentials should be stored in a `${workspaceFolder}/.mcp-dotnet/.env` file (not included in the repository).

## Original Source

This project is based on concepts from the blog post: [Как расширить GithubCopilot с помощью Python и Langchain через MCP](http://www.apofig.com/2025/06/githubcopilot-python-langchain-mcp.html)

## Repository

The original repository is available at: [https://github.com/mcpyrex/mcpyrex-dotnet.git](https://github.com/mcpyrex/mcpyrex-dotnet.git)

## 🚀 Two Execution Modes

This implementation provides two distinct ways to run the tools:

1. **📡 MCP Server Mode** (`./mcp.csproj`) - Full MCP protocol server for VS Code integration
   - Uses ModelContextProtocol 0.3.0
   - Includes Server.cs for MCP communication
   - Integrates with VS Code Copilot and other MCP clients
   - Provides protocol-compliant tool discovery and execution

2. **🔧 Standalone CLI Mode** (`./run.csproj`) - Direct command-line tool execution
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