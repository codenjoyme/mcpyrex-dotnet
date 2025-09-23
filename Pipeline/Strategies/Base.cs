using System.Collections.Generic;
using System.Threading.Tasks;

namespace McpDotnet.Pipeline.Strategies
{
    /// <summary>
    /// Base execution strategy interface.
    /// 
    /// This module defines the abstract base class for all pipeline execution strategies.
    /// Each strategy implements specific functionality while following a common interface.
    /// </summary>

    /// <summary>
    /// Base class for all pipeline execution strategies.
    /// 
    /// Each strategy handles a specific type of pipeline step,
    /// enabling modular and extensible pipeline functionality.
    /// </summary>
    public abstract class ExecutionStrategy
    {
        /// <summary>
        /// Check if this strategy can handle the given step.
        /// </summary>
        public abstract bool CanHandle(Dictionary<string, object> step);

        /// <summary>
        /// Execute the step using this strategy.
        /// </summary>
        public abstract Task<PipelineResult> ExecuteAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor);

        /// <summary>
        /// Name of this strategy for logging and debugging.
        /// </summary>
        public abstract string StrategyName { get; }
    }
}
