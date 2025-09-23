using McpDotnet.Pipeline.Strategies;

namespace McpDotnet.Pipeline
{
    /// <summary>
    /// Modern pipeline architecture for MCP tool chaining.
    /// 
    /// This module provides a comprehensive pipeline system with:
    /// - Strategy-based architecture for extensibility  
    /// - Support for conditionals, loops, parallel execution
    /// - Advanced expression evaluation with ternary operators
    /// - Clean separation of concerns
    /// 
    /// Usage:
    ///     var executor = new PipelineExecutor(toolRunner);
    ///     var result = await executor.ExecuteAsync(pipelineConfig);
    /// </summary>

    // Main executor (strategy-based) - aliases for consistency with Python version
    public class PipelineExecutor : StrategyBasedExecutor
    {
        public PipelineExecutor(Func<string, Dictionary<string, object>, Task<object>> toolRunner)
            : base(toolRunner)
        {
        }
    }
}
