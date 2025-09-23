using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Pipeline.Strategies
{
    /// <summary>
    /// Loop execution strategy.
    /// 
    /// This strategy handles various loop types including forEach, while, and repeat loops
    /// with proper expression evaluation and nested pipeline execution.
    /// </summary>

    public class LoopStrategy : ExecutionStrategy
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<LoopStrategy>();

        /// <summary>
        /// Strategy for handling loop constructs (forEach, while, repeat).
        /// </summary>
        public LoopStrategy()
        {
        }

        public override bool CanHandle(Dictionary<string, object> step)
        {
            /// Handle steps with loop logic.
            return step.GetValueOrDefault("type")?.ToString() == "forEach" ||
                   step.GetValueOrDefault("type")?.ToString() == "while" ||
                   step.GetValueOrDefault("type")?.ToString() == "repeat" ||
                   step.ContainsKey("forEach") ||
                   step.ContainsKey("while") ||
                   step.ContainsKey("repeat");
        }

        public override async Task<PipelineResult> ExecuteAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute loop logic with various loop types.
            try
            {
                // Determine loop type
                if (step.ContainsKey("forEach") || step.GetValueOrDefault("type")?.ToString() == "forEach")
                {
                    return await ExecuteForEachLoopAsync(step, context, executor);
                }
                else if (step.ContainsKey("while") || step.GetValueOrDefault("type")?.ToString() == "while")
                {
                    return await ExecuteWhileLoopAsync(step, context, executor);
                }
                else if (step.ContainsKey("repeat") || step.GetValueOrDefault("type")?.ToString() == "repeat")
                {
                    return await ExecuteRepeatLoopAsync(step, context, executor);
                }
                else
                {
                    return new PipelineResult
                    {
                        Success = false,
                        Error = $"Step {context.StepNumber}: Unknown loop type",
                        Step = context.StepNumber,
                        Context = context.Variables
                    };
                }
            }
            catch (Exception e)
            {
                var errorMsg = $"Step {context.StepNumber} loop failed: {e.Message}";
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

        private async Task<PipelineResult> ExecuteForEachLoopAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute forEach loop over a collection.
            var forEachExpr = step.GetValueOrDefault("forEach")?.ToString() ?? string.Empty;
            var itemVar = step.GetValueOrDefault("item")?.ToString() ?? "item";
            var doSteps = GetStepsArray(step.GetValueOrDefault("do"));

            if (string.IsNullOrEmpty(forEachExpr))
            {
                return new PipelineResult
                {
                    Success = false,
                    Error = $"Step {context.StepNumber}: forEach expression is required",
                    Step = context.StepNumber,
                    Context = context.Variables
                };
            }

            if (doSteps.Count == 0)
            {
                Logger.LogInformation($"Step {context.StepNumber}: forEach loop has no steps to execute");
                return new PipelineResult { Success = true, Context = context.Variables };
            }

            Logger.LogInformation($"Step {context.StepNumber}: Executing forEach loop: {forEachExpr}");

            // Evaluate collection expression
            var collectionResult = ExpressionEvaluator.EvaluateExpression(forEachExpr, context.Variables, "dotnet");
            
            // Convert result to enumerable
            var collection = ConvertToEnumerable(collectionResult);
            
            if (collection == null)
            {
                Logger.LogWarning($"Step {context.StepNumber}: forEach expression did not return a collection: {collectionResult}");
                return new PipelineResult { Success = true, Context = context.Variables };
            }

            Logger.LogInformation($"Step {context.StepNumber}: forEach iterating over {collection.Count()} items");

            // Save original item variable value (if exists)
            var originalItemValue = context.Variables.ContainsKey(itemVar) ? context.Variables[itemVar] : null;

            try
            {
                // Execute loop body for each item
                foreach (var item in collection)
                {
                    // Set current item in context
                    context.Variables[itemVar] = item ?? "null";

                    // Execute loop body
                    var result = await executor.ExecuteSubPipelineAsync(doSteps, context, "FOR_EACH");
                    if (!result.Success)
                        return result;
                }

                return new PipelineResult { Success = true, Context = context.Variables };
            }
            finally
            {
                // Restore original item variable value
                if (originalItemValue != null)
                    context.Variables[itemVar] = originalItemValue;
                else
                    context.Variables.Remove(itemVar);
            }
        }

        private async Task<PipelineResult> ExecuteWhileLoopAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute while loop with condition.
            var whileExpr = step.GetValueOrDefault("while")?.ToString() ?? string.Empty;
            var doSteps = GetStepsArray(step.GetValueOrDefault("do"));

            if (string.IsNullOrEmpty(whileExpr))
            {
                return new PipelineResult
                {
                    Success = false,
                    Error = $"Step {context.StepNumber}: while expression is required",
                    Step = context.StepNumber,
                    Context = context.Variables
                };
            }

            if (doSteps.Count == 0)
            {
                Logger.LogInformation($"Step {context.StepNumber}: while loop has no steps to execute");
                return new PipelineResult { Success = true, Context = context.Variables };
            }

            Logger.LogInformation($"Step {context.StepNumber}: Executing while loop: {whileExpr}");

            const int maxIterations = 1000; // Safety limit
            int iteration = 0;

            while (iteration < maxIterations)
            {
                // Evaluate condition
                var conditionResult = EvaluateCondition(whileExpr, context.Variables);
                
                if (!conditionResult)
                {
                    Logger.LogInformation($"Step {context.StepNumber}: while condition false after {iteration} iterations");
                    break;
                }

                // Execute loop body
                var result = await executor.ExecuteSubPipelineAsync(doSteps, context, "WHILE");
                if (!result.Success)
                    return result;

                iteration++;
            }

            if (iteration >= maxIterations)
            {
                Logger.LogWarning($"Step {context.StepNumber}: while loop hit maximum iterations ({maxIterations})");
            }

            return new PipelineResult { Success = true, Context = context.Variables };
        }

        private async Task<PipelineResult> ExecuteRepeatLoopAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute repeat loop for specified count.
            var repeatExpr = step.GetValueOrDefault("repeat")?.ToString() ?? string.Empty;
            var doSteps = GetStepsArray(step.GetValueOrDefault("do"));

            if (string.IsNullOrEmpty(repeatExpr))
            {
                return new PipelineResult
                {
                    Success = false,
                    Error = $"Step {context.StepNumber}: repeat count is required",
                    Step = context.StepNumber,
                    Context = context.Variables
                };
            }

            if (doSteps.Count == 0)
            {
                Logger.LogInformation($"Step {context.StepNumber}: repeat loop has no steps to execute");
                return new PipelineResult { Success = true, Context = context.Variables };
            }

            // Evaluate repeat count expression
            var countResult = ExpressionEvaluator.EvaluateExpression(repeatExpr, context.Variables, "dotnet");
            
            if (!TryConvertToInt(countResult, out var repeatCount) || repeatCount < 0)
            {
                return new PipelineResult
                {
                    Success = false,
                    Error = $"Step {context.StepNumber}: repeat count must be a non-negative integer, got: {countResult}",
                    Step = context.StepNumber,
                    Context = context.Variables
                };
            }

            Logger.LogInformation($"Step {context.StepNumber}: Executing repeat loop {repeatCount} times");

            // Execute loop body specified number of times
            for (int i = 0; i < repeatCount; i++)
            {
                var result = await executor.ExecuteSubPipelineAsync(doSteps, context, "REPEAT");
                if (!result.Success)
                    return result;
            }

            return new PipelineResult { Success = true, Context = context.Variables };
        }

        private bool EvaluateCondition(string condition, Dictionary<string, object> variables)
        {
            /// Evaluate condition using JavaScript-like expressions.
            try
            {
                var result = ExpressionEvaluator.EvaluateExpression(condition, variables, "dotnet");

                // Convert to boolean using JavaScript-like truthiness
                return result switch
                {
                    bool boolValue => boolValue,
                    int intValue => intValue != 0,
                    double doubleValue => doubleValue != 0,
                    float floatValue => floatValue != 0,
                    string stringValue => !string.IsNullOrEmpty(stringValue) &&
                                        !stringValue.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                                        !stringValue.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                                        !stringValue.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                                        !stringValue.Equals("undefined", StringComparison.OrdinalIgnoreCase),
                    null => false,
                    _ => true // Non-null objects are truthy
                };
            }
            catch (Exception e)
            {
                Logger.LogError($"Condition evaluation error: {e.Message}");
                throw;
            }
        }

        private IEnumerable<object>? ConvertToEnumerable(object? obj)
        {
            /// Convert various object types to enumerable.
            return obj switch
            {
                null => null,
                string str => str.ToCharArray().Cast<object>(),
                IEnumerable<object> enumerable => enumerable,
                System.Collections.IEnumerable nonGenericEnumerable => nonGenericEnumerable.Cast<object>(),
                _ => null
            };
        }

        private bool TryConvertToInt(object? obj, out int result)
        {
            /// Try to convert object to integer.
            result = 0;
            
            return obj switch
            {
                int intValue => (result = intValue) >= 0,
                double doubleValue => double.IsInteger(doubleValue) && (result = (int)doubleValue) >= 0,
                float floatValue => float.IsInteger(floatValue) && (result = (int)floatValue) >= 0,
                string stringValue => int.TryParse(stringValue, out result) && result >= 0,
                _ => false
            };
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

        public override string StrategyName => "Loop";
    }
}
