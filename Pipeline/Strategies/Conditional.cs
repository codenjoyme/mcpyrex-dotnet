using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Pipeline.Strategies
{
    /// <summary>
    /// Conditional execution strategy.
    /// 
    /// This strategy handles if-then-else logic with JavaScript-like expression evaluation
    /// for conditions. Supports complex boolean expressions and nested pipeline execution.
    /// </summary>

    public class ConditionalStrategy : ExecutionStrategy
    {
        private static readonly ILogger Logger = McpDotnet.LoggingConfig.SetupLogging("Pipeline.ConditionalStrategy");

        /// <summary>
        /// Strategy for handling conditional logic (if-then-else).
        /// </summary>
        public ConditionalStrategy()
        {
        }

        public override bool CanHandle(Dictionary<string, object> step)
        {
            /// Handle steps with conditional logic.
            return step.GetValueOrDefault("type")?.ToString() == "condition" ||
                   step.ContainsKey("condition") ||
                   step.ContainsKey("if");
        }

        public override async Task<PipelineResult> ExecuteAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute conditional logic with then/else branches.
            try
            {
                // Support multiple condition syntaxes
                var conditionExpr = step.GetValueOrDefault("condition")?.ToString() ??
                                  step.GetValueOrDefault("if")?.ToString() ??
                                  string.Empty;
                
                var thenSteps = GetStepsArray(step.GetValueOrDefault("then"));
                var elseSteps = GetStepsArray(step.GetValueOrDefault("else"));

                if (string.IsNullOrEmpty(conditionExpr))
                {
                    return new PipelineResult
                    {
                        Success = false,
                        Error = $"Step {context.StepNumber}: condition expression is required",
                        Step = context.StepNumber,
                        Context = context.Variables
                    };
                }

                Logger.LogInformation($"Step {context.StepNumber}: Evaluating condition: {conditionExpr}");

                // Evaluate condition using expression handler
                var conditionResult = EvaluateCondition(conditionExpr, context.Variables);
                Logger.LogInformation($"Step {context.StepNumber}: Condition result: {conditionResult}");

                // Execute appropriate branch
                if (conditionResult && thenSteps.Count > 0)
                {
                    Logger.LogInformation($"Step {context.StepNumber}: Executing THEN block with {thenSteps.Count} steps");
                    return await executor.ExecuteSubPipelineAsync(thenSteps, context, "THEN");
                }
                else if (!conditionResult && elseSteps.Count > 0)
                {
                    Logger.LogInformation($"Step {context.StepNumber}: Executing ELSE block with {elseSteps.Count} steps");
                    return await executor.ExecuteSubPipelineAsync(elseSteps, context, "ELSE");
                }
                else
                {
                    Logger.LogInformation($"Step {context.StepNumber}: No applicable branch, skipping");
                    return new PipelineResult
                    {
                        Success = true,
                        Context = context.Variables
                    };
                }
            }
            catch (Exception e)
            {
                var errorMsg = $"Step {context.StepNumber} condition failed: {e.Message}";
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

        private bool EvaluateCondition(string condition, Dictionary<string, object> variables)
        {
            /// Evaluate condition using JavaScript-like expressions.
            try
            {
                // Direct evaluation using new expression system
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

        public override string StrategyName => "Conditional";
    }
}
