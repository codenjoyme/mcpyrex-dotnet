# lng_batch_run Tool (.NET)

Executes a batch pipeline of tool calls with variable substitution using .NET expressions.

## Features

- **Strategy-Based Architecture**: Modular system for different step types
- **Tool Execution**: Sequential tool calls with variable passing
- **Conditional Logic**: if-then-else with .NET expressions  
- **Loops**: forEach, while, repeat iterations
- **Parallel Execution**: Run multiple steps concurrently
- **Timing Control**: Delays and sleep functionality
- **File-based Pipelines**: External configuration support
- **Debug Logging**: Optional step output logging
- **Context Filtering**: Control output verbosity

## Usage

### Basic Pipeline

```json
{
  "pipeline": [
    {
      "tool": "lng_count_words",
      "params": {
        "input_text": "Hello world"
      },
      "output": "word_count"
    },
    {
      "tool": "lng_math_calculator",
      "params": {
        "expression": "{! word_count.count * 2 !}"
      }
    }
  ]
}
```

### Conditional Logic

```json
{
  "pipeline": [
    {
      "tool": "lng_count_words",
      "params": {
        "input_text": "{! user.text !}"
      },
      "output": "stats"
    },
    {
      "type": "condition",
      "condition": "{! stats.count > 10 !}",
      "then": [
        {"tool": "lng_file_write", "params": {"content": "Long text", "file_path": "long.txt"}}
      ],
      "else": [
        {"tool": "lng_file_write", "params": {"content": "Short text", "file_path": "short.txt"}}
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
      "tool": "lng_file_list",
      "params": {
        "directory_path": "/path/to/files"
      },
      "output": "files"
    },
    {
      "type": "forEach",
      "forEach": "{! files.files !}",
      "item": "current_file",
      "do": [
        {
          "tool": "lng_file_read",
          "params": {
            "file_path": "{! current_file !}"
          },
          "output": "file_content"
        }
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
      "maxConcurrent": 3,
      "parallel": [
        {"tool": "lng_count_words", "params": {"input_text": "Text 1"}, "output": "count1"},
        {"tool": "lng_count_words", "params": {"input_text": "Text 2"}, "output": "count2"},
        {"tool": "lng_count_words", "params": {"input_text": "Text 3"}, "output": "count3"}
      ]
    }
  ]
}
```

## Expression System

The .NET version uses C#-style expressions within `{! !}` markers:

- `{! variable !}` - Direct variable access
- `{! variable.Property !}` - Property access
- `{! variable ?? "default" !}` - Null-coalescing operator
- `{! variable ? value1 : value2 !}` - Ternary operator
- `{! JsonSerializer.Serialize(variable) !}` - JSON serialization

## File Structure

- `LngBatchRunTool.cs` - Main tool implementation
- `PipelineResultExtensions.cs` - Extensions for result conversion
- `README.md` - This documentation

## Dependencies

- `McpDotnet.Pipeline` - Pipeline execution engine
- `System.Text.Json` - JSON serialization
- `Microsoft.Extensions.Logging` - Logging support
