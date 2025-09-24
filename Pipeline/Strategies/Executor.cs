using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Pipeline.Strategies
{
    /// <summary>
    /// Strategy-based pipeline executor.
    /// 
    /// This module contains the main executor that orchestrates all pipeline strategies
    /// using composition pattern for maximum flexibility and extensibility.
    /// </summary>

    /// <summary>
    /// Pipeline executor using strategy pattern for modular functionality.
    /// 
    /// This executor uses composition instead of inheritance, allowing
    /// easy addition of new features through strategy plugins.
    /// 
    /// Features:
    /// - ✅ Modular strategy system
    /// - ✅ Tool execution (basic functionality)
    /// - ✅ Conditional logic (if-then-else)
    /// - ✅ Loop support (forEach, while, repeat)
    /// - ✅ Parallel execution
    /// - ✅ Delay/timing control
    /// - 🚧 Template system (coming soon)
    /// - 🚧 Error handling strategies (coming soon)
    /// 
    /// Usage:
    ///     var executor = new StrategyBasedExecutor(toolRunner);
    ///     
    ///     // Add custom strategies
    ///     executor.AddStrategy(new CustomStrategy());
    ///     
    ///     var result = await executor.ExecuteAsync(pipelineConfig);
    /// </summary>
    public class StrategyBasedExecutor
    {
        private static readonly ILogger Logger = McpDotnet.LoggingConfig.SetupLogging("Pipeline.StrategyBasedExecutor");

        private readonly Func<string, Dictionary<string, object>, Task<object>> _toolRunner;
        private readonly List<ExecutionStrategy> _strategies = new();

        public StrategyBasedExecutor(Func<string, Dictionary<string, object>, Task<object>> toolRunner)
        {
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            RegisterDefaultStrategies();
        }

        private void RegisterDefaultStrategies()
        {
            /// Register all default execution strategies.
            // Order matters: more specific strategies should come first
            AddStrategy(new ConditionalStrategy());
            AddStrategy(new LoopStrategy());
            AddStrategy(new ParallelStrategy());
            AddStrategy(new DelayStrategy());
            AddStrategy(new ToolStrategy(_toolRunner)); // Fallback strategy
        }

        /// <summary>
        /// Add a new execution strategy.
        /// </summary>
        public void AddStrategy(ExecutionStrategy strategy)
        {
            _strategies.Add(strategy);
            Logger.LogInformation($"Added strategy: {strategy.StrategyName}");
        }

        /// <summary>
        /// Remove a strategy by name.
        /// </summary>
        public void RemoveStrategy(string strategyName)
        {
            _strategies.RemoveAll(s => s.StrategyName == strategyName);
            Logger.LogInformation($"Removed strategy: {strategyName}");
        }

        /// <summary>
        /// Get list of registered strategy names.
        /// </summary>
        public List<string> GetStrategies()
        {
            return _strategies.Select(s => s.StrategyName).ToList();
        }

        /// <summary>
        /// Execute a pipeline using the registered strategies.
        /// </summary>
        public async Task<PipelineResult> ExecuteAsync(Dictionary<string, object> config)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Initialize execution context
                var context = new ExecutionContext();

                // Add user parameters to context if provided
                if (config.ContainsKey("user_params") && config["user_params"] is Dictionary<string, object> userParams)
                {
                    context.Variables["user"] = userParams;
                    Logger.LogInformation($"Added user parameters to context: {string.Join(", ", userParams.Keys)}");
                }

                var pipeline = GetPipelineSteps(config);
                var finalResultExpr = config.GetValueOrDefault("final_result", "ok").ToString() ?? "ok";

                if (pipeline.Count == 0)
                {
                    // Empty pipeline is valid - just calculate final result
                    Logger.LogInformation("Empty pipeline provided, calculating final result only");
                    try
                    {
                        var finalResult = ExpressionEvaluator.EvaluateExpression(finalResultExpr, context.Variables, "dotnet");
                        return new PipelineResult
                        {
                            Success = true,
                            Result = finalResult,
                            Context = context.Variables,
                            ExecutionTime = stopwatch.Elapsed.TotalSeconds
                        };
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"Final result evaluation failed: {e.Message}, using default");
                        return new PipelineResult
                        {
                            Success = true,
                            Result = "ok",
                            Context = context.Variables,
                            ExecutionTime = stopwatch.Elapsed.TotalSeconds
                        };
                    }
                }

                Logger.LogInformation($"Starting pipeline execution with {pipeline.Count} steps");
                Logger.LogInformation($"Registered strategies: {string.Join(", ", GetStrategies())}");

                // Execute pipeline steps
                for (int i = 0; i < pipeline.Count; i++)
                {
                    context.StepNumber = i + 1;
                    var step = pipeline[i];

                    var result = await ExecuteStepAsync(step, context);
                    if (!result.Success)
                    {
                        result.ExecutionTime = stopwatch.Elapsed.TotalSeconds;
                        return result;
                    }
                }

                // Calculate final result
                object finalResultValue;
                try
                {
                    finalResultValue = ExpressionEvaluator.EvaluateExpression(finalResultExpr, context.Variables, "dotnet");
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Final result evaluation failed: {e.Message}, using default");
                    finalResultValue = "ok";
                }

                var executionTime = stopwatch.Elapsed.TotalSeconds;
                Logger.LogInformation($"Pipeline completed successfully in {executionTime:F4}s");

                return new PipelineResult
                {
                    Success = true,
                    Result = finalResultValue,
                    Context = context.Variables,
                    ExecutionTime = executionTime
                };
            }
            catch (Exception e)
            {
                var executionTime = stopwatch.Elapsed.TotalSeconds;
                var errorMsg = $"Pipeline execution failed: {e.Message}";
                Logger.LogError(errorMsg);
                return new PipelineResult
                {
                    Success = false,
                    Error = errorMsg,
                    ExecutionTime = executionTime
                };
            }
        }

        /// <summary>
        /// Execute a single step using the appropriate strategy.
        /// </summary>
        public async Task<PipelineResult> ExecuteStepAsync(Dictionary<string, object> step, ExecutionContext context)
        {
            // Find strategy that can handle this step
            foreach (var strategy in _strategies)
            {
                if (strategy.CanHandle(step))
                {
                    Logger.LogDebug($"Step {context.StepNumber}: Using {strategy.StrategyName} strategy");
                    return await strategy.ExecuteAsync(step, context, this);
                }
            }

            // No strategy found
            return new PipelineResult
            {
                Success = false,
                Error = $"Step {context.StepNumber}: No strategy found to handle step: {JsonSerializer.Serialize(step)}",
                Step = context.StepNumber,
                Context = context.Variables
            };
        }

        /// <summary>
        /// Execute a sub-pipeline (for conditional branches, loops, etc.).
        /// </summary>
        public async Task<PipelineResult> ExecuteSubPipelineAsync(List<Dictionary<string, object>> steps, 
            ExecutionContext context, string blockType = "SUB")
        {
            if (steps.Count == 0)
                return new PipelineResult { Success = true, Context = context.Variables };

            var originalStep = context.StepNumber;

            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    context.StepNumber = $"{originalStep}.{i + 1}";
                    var step = steps[i];

                    var result = await ExecuteStepAsync(step, context);
                    if (!result.Success)
                        return result;
                }

                context.StepNumber = originalStep;
                return new PipelineResult { Success = true, Context = context.Variables };
            }
            catch (Exception e)
            {
                context.StepNumber = originalStep;
                var errorMsg = $"{blockType} sub-pipeline execution failed: {e.Message}";
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

        private List<Dictionary<string, object>> GetPipelineSteps(Dictionary<string, object> config)
        {
            /// Extract pipeline steps from configuration.
            if (!config.ContainsKey("pipeline"))
                return new List<Dictionary<string, object>>();

            var pipeline = config["pipeline"];
            
            if (pipeline is List<Dictionary<string, object>> stepsList)
                return stepsList;

            if (pipeline is List<object> objectList)
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

        /// <summary>
        /// Load pipeline configuration from file.
        /// </summary>
        public async Task<Dictionary<string, object>> LoadPipelineFromFileAsync(string pipelineFile)
        {
            Logger.LogInformation($"Loading pipeline from file: {pipelineFile}");

            try
            {
                if (!Path.IsPathRooted(pipelineFile))
                {
                    // Make relative paths relative to the current directory
                    pipelineFile = Path.Combine(Directory.GetCurrentDirectory(), pipelineFile);
                }

                var json = await File.ReadAllTextAsync(pipelineFile);
                var pipelineConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                Logger.LogInformation($"Successfully loaded pipeline from {pipelineFile}");
                return pipelineConfig ?? new Dictionary<string, object>();
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException($"Pipeline file not found: {pipelineFile}");
            }
            catch (JsonException e)
            {
                throw new JsonException($"Invalid JSON in pipeline file {pipelineFile}: {e.Message}", e);
            }
        }

        /// <summary>
        /// Execute pipeline from file with optional argument overrides.
        /// </summary>
        public async Task<PipelineResult> ExecuteFromFileAsync(string pipelineFile, 
            Dictionary<string, object>? argumentOverrides = null)
        {
            try
            {
                var pipelineConfig = await LoadPipelineFromFileAsync(pipelineFile);

                // Merge file config with argument overrides, giving priority to overrides
                if (argumentOverrides != null)
                {
                    foreach (var kvp in argumentOverrides)
                    {
                        if (kvp.Key != "pipeline_file") // Don't override the file parameter itself
                        {
                            pipelineConfig[kvp.Key] = kvp.Value;
                        }
                    }
                }

                var result = await ExecuteAsync(pipelineConfig);
                
                // Add source information to result
                var resultDict = result.ToDict();
                resultDict["pipeline_source"] = pipelineFile;

                return new PipelineResult
                {
                    Success = result.Success,
                    Result = resultDict,
                    Error = result.Error,
                    Step = result.Step,
                    Tool = result.Tool,
                    Context = result.Context,
                    ExecutionTime = result.ExecutionTime
                };
            }
            catch (Exception e)
            {
                return new PipelineResult
                {
                    Success = false,
                    Error = $"Failed to execute pipeline from file {pipelineFile}: {e.Message}",
                    Context = new Dictionary<string, object>()
                };
            }
        }
    }
}
