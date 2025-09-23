using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Pipeline.Strategies
{
    /// <summary>
    /// Delay execution strategy.
    /// 
    /// This strategy handles delay/sleep functionality in pipelines,
    /// allowing for timed pauses between operations.
    /// </summary>

    public class DelayStrategy : ExecutionStrategy
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<DelayStrategy>();

        /// <summary>
        /// Strategy for handling delay/sleep operations.
        /// </summary>
        public DelayStrategy()
        {
        }

        public override bool CanHandle(Dictionary<string, object> step)
        {
            /// Handle steps with delay functionality.
            return step.GetValueOrDefault("type")?.ToString() == "delay" ||
                   step.ContainsKey("delay");
        }

        public override async Task<PipelineResult> ExecuteAsync(Dictionary<string, object> step, 
            ExecutionContext context, StrategyBasedExecutor executor)
        {
            /// Execute delay operation.
            try
            {
                var delayExpr = step.GetValueOrDefault("delay")?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(delayExpr))
                {
                    return new PipelineResult
                    {
                        Success = false,
                        Error = $"Step {context.StepNumber}: delay duration is required",
                        Step = context.StepNumber,
                        Context = context.Variables
                    };
                }

                Logger.LogInformation($"Step {context.StepNumber}: Processing delay: {delayExpr}");

                // Evaluate delay expression
                var delayResult = ExpressionEvaluator.EvaluateExpression(delayExpr, context.Variables, "dotnet");
                
                if (!TryConvertToDouble(delayResult, out var delaySeconds) || delaySeconds < 0)
                {
                    return new PipelineResult
                    {
                        Success = false,
                        Error = $"Step {context.StepNumber}: delay duration must be a non-negative number in seconds, got: {delayResult}",
                        Step = context.StepNumber,
                        Context = context.Variables
                    };
                }

                Logger.LogInformation($"Step {context.StepNumber}: Delaying for {delaySeconds} seconds");

                // Convert to milliseconds and execute delay
                var delayMilliseconds = (int)(delaySeconds * 1000);
                await Task.Delay(delayMilliseconds);

                Logger.LogInformation($"Step {context.StepNumber}: Delay completed");

                return new PipelineResult 
                { 
                    Success = true, 
                    Context = context.Variables 
                };
            }
            catch (Exception e)
            {
                var errorMsg = $"Step {context.StepNumber} delay failed: {e.Message}";
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

        private bool TryConvertToDouble(object? obj, out double result)
        {
            /// Try to convert object to double.
            result = 0.0;
            
            return obj switch
            {
                double doubleValue => (result = doubleValue) >= 0,
                float floatValue => (result = floatValue) >= 0,
                int intValue => (result = intValue) >= 0,
                string stringValue => double.TryParse(stringValue, out result) && result >= 0,
                _ => false
            };
        }

        public override string StrategyName => "Delay";
    }
}
