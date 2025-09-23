using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Pipeline.Strategies
{
    /// <summary>
    /// Parallel execution strategy.
    /// 
    /// This strategy handles parallel execution of multiple pipeline steps
    /// with configurable concurrency limits and proper error handling.
    /// </summary>

    public class ParallelStrategy : ExecutionStrategy
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ParallelStrategy>();

        /// <summary>
        /// Strategy for handling parallel execution of steps.
        /// </summary>
        public ParallelStrategy()
        {
        }

        public override bool CanHandle(Dictionary<string, object> step)
        {
            /// Handle steps with parallel execution.
            return step.GetValueOrDefault("type")?.ToString() == "parallel" ||
                   step.ContainsKey("parallel");
        }

        public override async Task<PipelineResult> ExecuteAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute multiple steps in parallel with concurrency control.
            try
            {
                var parallelSteps = GetStepsArray(step.GetValueOrDefault("parallel"));
                var maxConcurrent = GetMaxConcurrent(step);

                if (parallelSteps.Count == 0)
                {
                    Logger.LogInformation($"Step {context.StepNumber}: No parallel steps to execute");
                    return new PipelineResult { Success = true, Context = context.Variables };
                }

                Logger.LogInformation($"Step {context.StepNumber}: Executing {parallelSteps.Count} steps in parallel " +
                                    $"with max concurrency {maxConcurrent}");

                // Create semaphore to limit concurrency
                using var semaphore = new System.Threading.SemaphoreSlim(maxConcurrent, maxConcurrent);

                // Create tasks for all parallel steps
                var tasks = parallelSteps.Select(async (parallelStep, index) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        // Create a copy of the context for this parallel execution
                        var parallelContext = CreateParallelContext(context, index);
                        
                        Logger.LogDebug($"Step {context.StepNumber}.{index + 1}: Starting parallel execution");
                        
                        // Execute the step
                        var result = await executor.ExecuteStepAsync(parallelStep, parallelContext);
                        
                        Logger.LogDebug($"Step {context.StepNumber}.{index + 1}: Completed parallel execution, " +
                                      $"success: {result.Success}");
                        
                        return new ParallelStepResult
                        {
                            Index = index,
                            Result = result,
                            Context = parallelContext
                        };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                // Wait for all tasks to complete
                var results = await Task.WhenAll(tasks);

                // Check for failures
                var failures = results.Where(r => !r.Result.Success).ToList();
                if (failures.Any())
                {
                    var firstFailure = failures.First();
                    var errorMsg = $"Step {context.StepNumber}: Parallel execution failed at step " +
                                 $"{firstFailure.Index + 1}: {firstFailure.Result.Error}";
                    Logger.LogError(errorMsg);
                    
                    return new PipelineResult
                    {
                        Success = false,
                        Error = errorMsg,
                        Step = context.StepNumber,
                        Context = context.Variables
                    };
                }

                // Merge contexts from all parallel executions
                MergeParallelContexts(context, results);

                Logger.LogInformation($"Step {context.StepNumber}: All {parallelSteps.Count} parallel steps completed successfully");

                return new PipelineResult { Success = true, Context = context.Variables };
            }
            catch (Exception e)
            {
                var errorMsg = $"Step {context.StepNumber} parallel execution failed: {e.Message}";
                Logger.LogError(errorMsg);
                return new PipelineResult
                {
                    Success = false,
                    Error = errorMsg,
                    Step = context.StepNumber,
                    Context = context.Variables
                };
            }
        }

        private int GetMaxConcurrent(Dictionary<string, object> step)
        {
            /// Get maximum concurrent executions from step configuration.
            var maxConcurrentValue = step.GetValueOrDefault("maxConcurrent");
            
            if (maxConcurrentValue == null)
                return 10; // Default value

            if (int.TryParse(maxConcurrentValue.ToString(), out var intValue) && intValue > 0)
                return intValue;

            return 10; // Fallback default
        }

        private ExecutionContext CreateParallelContext(ExecutionContext originalContext, int index)
        {
            /// Create a copy of the execution context for parallel execution.
            var parallelContext = new ExecutionContext(
                new Dictionary<string, object>(originalContext.Variables))
            {
                StepNumber = $"{originalContext.StepNumber}.{index + 1}",
                Templates = originalContext.Templates != null 
                    ? new Dictionary<string, object>(originalContext.Templates) 
                    : null
            };

            return parallelContext;
        }

        private void MergeParallelContexts(ExecutionContext mainContext, ParallelStepResult[] results)
        {
            /// Merge variable changes from parallel executions back to main context.
            /// 
            /// Note: This is a simple merge strategy. In case of conflicts,
            /// the result with the highest index wins. For more sophisticated
            /// merge strategies, this method can be extended.
            
            foreach (var result in results.OrderBy(r => r.Index))
            {
                if (result.Result.Success && result.Context != null)
                {
                    foreach (var kvp in result.Context.Variables)
                    {
                        // Only merge new variables or variables that weren't in original context
                        if (!mainContext.Variables.ContainsKey(kvp.Key) || 
                            !mainContext.Variables[kvp.Key].Equals(kvp.Value))
                        {
                            mainContext.Variables[kvp.Key] = kvp.Value;
                            Logger.LogDebug($"Merged variable '{kvp.Key}' from parallel step {result.Index + 1}");
                        }
                    }
                }
            }
        }

        private List<Dictionary<string, object>> GetStepsArray(object? stepsObject)
        {
            /// Convert various step representations to List<Dictionary<string, object>>
            if (stepsObject == null)
                return new List<Dictionary<string, object>>();

            if (stepsObject is List<Dictionary<string, object>> stepsList)
                return stepsList;

            if (stepsObject is List<object> objectList)
            {
                var result = new List<Dictionary<string, object>>();
                foreach (var item in objectList)
                {
                    if (item is Dictionary<string, object> stepDict)
                        result.Add(stepDict);
                }
                return result;
            }

            return new List<Dictionary<string, object>>();
        }

        public override string StrategyName => "Parallel";

        /// <summary>
        /// Result of a single parallel step execution.
        /// </summary>
        private class ParallelStepResult
        {
            public int Index { get; set; }
            public PipelineResult Result { get; set; } = null!;
            public ExecutionContext? Context { get; set; }
        }
    }
}
