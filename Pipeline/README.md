# Pipeline Package (.NET)

Modern pipeline architecture for MCP tool chaining, ported from Python to .NET.

## Architecture

The pipeline system uses a **strategy-based pattern** for maximum modularity and extensibility:

- **Composition over inheritance** - Strategies are composed together
- **Modular design** - Each strategy handles specific functionality
- **Easy extensibility** - Add new strategies without modifying existing code
- **Full compatibility** - Maintains API compatibility with Python version

## Core Components

### Models (`Models.cs`)
- `PipelineResult` - Execution result container
- `ExecutionContext` - Runtime context and variables
- `StepType` - Legacy step type enumeration

### Expressions (`Expressions.cs`)
- **Expression Evaluator** - Handles `{! !}` expression substitution
- **Variable Resolution** - Context-aware variable lookup
- **Type Conversion** - Safe type conversions and fallbacks
- **.NET Expressions** - Uses C# expression syntax instead of JavaScript

### Strategies (`Strategies/`)

#### Base Strategy (`Base.cs`)
- Abstract base class for all execution strategies
- Defines common interface and shared functionality

#### Tool Strategy (`Tool.cs`)
- Executes MCP tools with parameter substitution
- Handles result storage and variable assignment
- Debug logging support

#### Conditional Strategy (`Conditional.cs`)
- If-then-else logic with expression evaluation
- Nested pipeline execution for branches
- Complex boolean expressions

#### Loop Strategy (`Loop.cs`)
- **forEach** - Iterate over collections
- **while** - Conditional loops with break conditions
- **repeat** - Fixed iteration count loops

#### Parallel Strategy (`Parallel.cs`)
- Concurrent execution of multiple steps
- Configurable concurrency limits
- Result aggregation and error handling

#### Delay Strategy (`Delay.cs`)
- Sleep/wait functionality
- Configurable delay durations
- Expression-based timing

#### Executor (`Executor.cs`)
- Main orchestrator for all strategies
- Strategy registration and dispatch
- File-based pipeline loading
- Error handling and logging

## Key Differences from Python Version

### Expression System
- **Python**: Uses JavaScript expressions with `{! !}` and Python with `[! !]`
- **.NET**: Uses C# expressions with `{! !}` only

### Syntax Changes
- `variable || 'default'` → `variable ?? "default"` (null-coalescing)
- `variable.length` → `variable.Length` or `variable.Count`
- `JSON.stringify(obj)` → `JsonSerializer.Serialize(obj)`

### Type System
- Strong typing with proper null handling
- Generic collections (`List<T>`, `Dictionary<K,V>`)
- Async/await pattern throughout

## Usage Examples

### Basic Tool Execution
```json
{
  "pipeline": [
    {
      "tool": "lng_math_calculator",
      "params": {
        "expression": "2 + 2"
      },
      "output": "result"
    }
  ]
}
```

### Conditional Logic
```json
{
  "pipeline": [
    {
      "type": "condition",
      "condition": "{! result.value > 5 !}",
      "then": [
        {"tool": "success_handler", "params": {}}
      ],
      "else": [
        {"tool": "failure_handler", "params": {}}
      ]
    }
  ]
}
```

### Loops
```json
{
  "pipeline": [
    {
      "type": "forEach",
      "forEach": "{! items !}",
      "item": "current",
      "do": [
        {"tool": "process_item", "params": {"data": "{! current !}"}}
      ]
    }
  ]
}
```

### Parallel Execution
```json
{
  "pipeline": [
    {
      "type": "parallel",
      "maxConcurrent": 5,
      "parallel": [
        {"tool": "task1", "params": {}},
        {"tool": "task2", "params": {}},
        {"tool": "task3", "params": {}}
      ]
    }
  ]
}
```

## API Compatibility

The .NET version maintains full API compatibility with the Python version:

- Same JSON schema for pipeline definitions
- Identical step types and parameters
- Compatible expression syntax (with .NET adaptations)
- Same result format and error handling

## Integration

To use the pipeline system:

1. **Register Strategies**:
```csharp
var executor = new StrategyBasedExecutor(toolRunner);
// Strategies are auto-registered by default
```

2. **Execute Pipeline**:
```csharp
var config = LoadPipelineConfig();
var result = await executor.ExecuteAsync(config);
```

3. **Handle Results**:
```csharp
if (result.Success)
{
    var finalResult = result.Result;
    var context = result.Context;
}
else
{
    var error = result.Error;
}
```

## Dependencies

- `System.Text.Json` - JSON serialization/deserialization
- `Microsoft.Extensions.Logging` - Logging infrastructure
- `System.Threading.Tasks` - Async execution
- `System.Collections.Generic` - Collection types

## Files Structure

```
Pipeline/
├── Models.cs              # Data models and types
├── Expressions.cs         # Expression evaluation system
├── Pipeline.cs            # Main pipeline executor wrapper
└── Strategies/
    ├── Base.cs            # Abstract base strategy
    ├── Tool.cs            # Tool execution strategy
    ├── Conditional.cs     # If-then-else logic
    ├── Loop.cs            # Iteration strategies
    ├── Parallel.cs        # Concurrent execution
    ├── Delay.cs           # Timing control
    └── Executor.cs        # Main strategy orchestrator
```

This architecture ensures the .NET version maintains full feature parity with the Python version while leveraging .NET's type safety and performance characteristics.
