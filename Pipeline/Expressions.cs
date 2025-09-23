using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jint; // JavaScript engine for .NET (requires Jint NuGet package)

namespace McpDotnet.Pipeline
{
    /// <summary>
    /// Expression evaluation strategies for pipeline execution.
    /// 
    /// Provides unified expression evaluation with multiple backends:
    /// - JavaScript expressions: {! expression !}
    /// - C# expressions: [! expression !]
    /// 
    /// Context always contains native .NET objects (Dictionary, List, string, int, bool, null).
    /// Results can be returned as .NET objects or JSON strings based on expected_result_type.
    /// 
    /// Built-in variables:
    /// - env.*: Access to environment variables (env.HOME, env.PATH, etc.)
    /// </summary>
    
    public class ExpressionEvaluationException : Exception
    {
        public string StrategyName { get; }
        public string Expression { get; }
        public Dictionary<string, object> StepInfo { get; }
        public Exception OriginalError { get; }

        public ExpressionEvaluationException(string strategyName, string expression, 
            Dictionary<string, object> stepInfo, Exception originalError)
            : base($"Expression evaluation failed in '{strategyName}' at step '{stepInfo.GetValueOrDefault("step", "unknown")}': " +
                   $"'{expression}' -> '{originalError.Message}'")
        {
            StrategyName = strategyName;
            Expression = expression;
            StepInfo = stepInfo;
            OriginalError = originalError;
        }
    }

    public static class ExpressionHelpers
    {
        public static Dictionary<string, object> BuildDefaultContext(Dictionary<string, object>? customContext = null)
        {
            var context = new Dictionary<string, object>
            {
                ["env"] = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .ToDictionary(entry => (string)entry.Key, entry => entry.Value!)
            };

            if (customContext != null)
            {
                foreach (var kvp in customContext)
                {
                    context[kvp.Key] = kvp.Value;
                }
            }

            return context;
        }
    }

    /// <summary>
    /// Base class for expression evaluation strategies.
    /// </summary>
    public abstract class ExpressionStrategy
    {
        /// <summary>
        /// Check if this strategy can handle the given expression.
        /// </summary>
        public abstract bool CanHandle(string expression);

        /// <summary>
        /// Evaluate expression with given context.
        /// 
        /// Args:
        ///     expression: Expression string (e.g., "{! variable !}" or "[! expression !]")
        ///     context: Dictionary of native .NET objects
        ///     expectedResultType: "dotnet" or "json"
        ///     stepInfo: Information about current pipeline step for error reporting
        ///     
        /// Returns:
        ///     Result in format specified by expectedResultType
        /// </summary>
        public abstract object Evaluate(string expression, Dictionary<string, object> context, 
            string expectedResultType, Dictionary<string, object>? stepInfo = null);

        /// <summary>
        /// Extract clean expression content from wrapper syntax.
        /// </summary>
        protected string ExtractExpression(string expression)
        {
            // Remove {! ... !} or [! ... !] wrapper
            if (expression.StartsWith("{! ") && expression.EndsWith(" !}"))
                return expression.Substring(3, expression.Length - 6);
            else if (expression.StartsWith("[! ") && expression.EndsWith(" !]"))
                return expression.Substring(3, expression.Length - 6);
            return expression;
        }

        /// <summary>
        /// Format result according to expected type.
        /// </summary>
        protected object FormatResult(object result, string expectedResultType)
        {
            if (expectedResultType == "dotnet")
                return result;
            else if (expectedResultType == "json")
            {
                try
                {
                    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
                }
                catch (Exception)
                {
                    return result.ToString() ?? "null";
                }
            }
            else
            {
                throw new ArgumentException($"Unknown expected_result_type: {expectedResultType}");
            }
        }
    }

    /// <summary>
    /// Strategy for evaluating JavaScript expressions: {! expression !}
    /// </summary>
    public class JavaScriptExpressionStrategy : ExpressionStrategy
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<JavaScriptExpressionStrategy>();

        public override bool CanHandle(string expression)
        {
            // Must be exactly a single pure JavaScript expression
            expression = expression.Trim();
            if (!(expression.StartsWith("{! ") && expression.EndsWith(" !}") && expression.Length > 6))
                return false;

            // Count occurrences to ensure it's a single pure expression
            return expression.Count(c => c == '{') == 1 && expression.Count(c => c == '}') == 1;
        }

        public override object Evaluate(string expression, Dictionary<string, object> context, 
            string expectedResultType, Dictionary<string, object>? stepInfo = null)
        {
            stepInfo ??= new Dictionary<string, object>();

            try
            {
                var cleanExpression = ExtractExpression(expression);

                // Create JavaScript engine
                var engine = new Engine();

                // Set context variables in JavaScript environment
                SetJsContext(engine, context);

                // Evaluate expression in JavaScript
                var jsResult = engine.Evaluate(cleanExpression);
                
                // Convert JavaScript result back to .NET
                var dotnetResult = ConvertJsToDotNet(jsResult);

                // Format result
                return FormatResult(dotnetResult, expectedResultType);
            }
            catch (Exception e)
            {
                throw new ExpressionEvaluationException("JavaScript", expression, stepInfo, e);
            }
        }

        private void SetJsContext(Engine engine, Dictionary<string, object> context)
        {
            foreach (var kvp in context)
            {
                try
                {
                    engine.SetValue(kvp.Key, kvp.Value);
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Failed to set JavaScript context variable '{kvp.Key}': {e.Message}");
                }
            }
        }

        private object ConvertJsToDotNet(Jint.Native.JsValue jsResult)
        {
            if (jsResult.IsNull() || jsResult.IsUndefined())
                return null!;
            if (jsResult.IsBoolean())
                return jsResult.AsBoolean();
            if (jsResult.IsNumber())
                return jsResult.AsNumber();
            if (jsResult.IsString())
                return jsResult.AsString();
            if (jsResult.IsArray())
            {
                var array = jsResult.AsArray();
                var list = new List<object>();
                for (uint i = 0; i < array.Length; i++)
                {
                    list.Add(ConvertJsToDotNet(array.Get(i)));
                }
                return list;
            }
            if (jsResult.IsObject())
            {
                var obj = jsResult.AsObject();
                var dict = new Dictionary<string, object>();
                foreach (var property in obj.GetOwnProperties())
                {
                    dict[property.Key.AsString()] = ConvertJsToDotNet(property.Value.Value);
                }
                return dict;
            }
            
            return jsResult.ToString();
        }
    }

    /// <summary>
    /// Strategy for evaluating C# expressions: [! expression !]
    /// Note: For security reasons, this is a simplified implementation.
    /// In production, consider using a safe expression evaluator like NCalc or similar.
    /// </summary>
    public class CSharpExpressionStrategy : ExpressionStrategy
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CSharpExpressionStrategy>();

        public override bool CanHandle(string expression)
        {
            // Must be exactly a single pure C# expression
            expression = expression.Trim();
            if (!(expression.StartsWith("[! ") && expression.EndsWith(" !]") && expression.Length > 6))
                return false;

            // Count occurrences to ensure it's a single pure expression
            return expression.Count(c => c == '[') == 1 && expression.Count(c => c == ']') == 1;
        }

        public override object Evaluate(string expression, Dictionary<string, object> context, 
            string expectedResultType, Dictionary<string, object>? stepInfo = null)
        {
            stepInfo ??= new Dictionary<string, object>();

            try
            {
                var cleanExpression = ExtractExpression(expression);

                // This is a simplified implementation for basic expressions
                // In production, you would use a proper C# expression evaluator
                var result = EvaluateSimpleExpression(cleanExpression, context);

                return FormatResult(result, expectedResultType);
            }
            catch (Exception e)
            {
                throw new ExpressionEvaluationException("CSharp", expression, stepInfo, e);
            }
        }

        private object EvaluateSimpleExpression(string expression, Dictionary<string, object> context)
        {
            // This is a very basic implementation for demonstration
            // In production, use a proper expression evaluator like NCalc, DynamicExpresso, etc.
            
            // Handle simple variable access
            if (context.ContainsKey(expression))
                return context[expression];
            
            // Handle property access like "variable.property"
            if (expression.Contains('.'))
            {
                var parts = expression.Split('.');
                var obj = context.GetValueOrDefault(parts[0]);
                
                for (int i = 1; i < parts.Length && obj != null; i++)
                {
                    if (obj is Dictionary<string, object> dict)
                        obj = dict.GetValueOrDefault(parts[i]);
                    else if (obj.GetType().GetProperty(parts[i]) != null)
                        obj = obj.GetType().GetProperty(parts[i])?.GetValue(obj);
                    else
                        return null!;
                }
                return obj!;
            }
            
            // Handle simple boolean expressions
            if (expression == "true") return true;
            if (expression == "false") return false;
            if (expression == "null") return null!;
            
            // Handle numbers
            if (int.TryParse(expression, out var intValue)) return intValue;
            if (double.TryParse(expression, out var doubleValue)) return doubleValue;
            
            // Handle strings in quotes
            if (expression.StartsWith("\"") && expression.EndsWith("\""))
                return expression.Substring(1, expression.Length - 2);
            
            throw new NotSupportedException($"C# expression not supported in this basic implementation: {expression}");
        }
    }

    /// <summary>
    /// Strategy for handling plain text (no expression syntax)
    /// </summary>
    public class PlainTextStrategy : ExpressionStrategy
    {
        public override bool CanHandle(string expression)
        {
            // Handle any expression that is NOT a pure single expression
            return !IsPureSingleExpression(expression);
        }

        public override object Evaluate(string expression, Dictionary<string, object> context, 
            string expectedResultType, Dictionary<string, object>? stepInfo = null)
        {
            // Check if text contains expressions that need processing
            if (ContainsExpressions(expression))
            {
                // Use recursive processing for mixed text
                return ProcessMixedText(expression, context, expectedResultType, stepInfo);
            }
            else
            {
                // Pure plain text - return as-is
                return FormatResult(expression, expectedResultType);
            }
        }

        private bool IsPureSingleExpression(string text)
        {
            text = text.Trim();

            // Check if it's exactly a JS expression
            if (text.StartsWith("{! ") && text.EndsWith(" !}"))
                return text.Length > 6 && text.Count(c => c == '{') == 1 && text.Count(c => c == '}') == 1;

            // Check if it's exactly a C# expression  
            if (text.StartsWith("[! ") && text.EndsWith(" !]"))
                return text.Length > 6 && text.Count(c => c == '[') == 1 && text.Count(c => c == ']') == 1;

            return false;
        }

        private bool ContainsExpressions(string text)
        {
            return (text.Contains("{! ") && text.Contains(" !}")) ||
                   (text.Contains("[! ") && text.Contains(" !]"));
        }

        private object ProcessMixedText(string text, Dictionary<string, object> context, 
            string expectedResultType, Dictionary<string, object>? stepInfo)
        {
            // Create a recursive strategy instance for processing
            var recursiveStrategy = new RecursiveExpressionStrategy();
            var resultText = recursiveStrategy.ProcessRecursive(text, context, stepInfo ?? new Dictionary<string, object>());

            return FormatResult(resultText, expectedResultType);
        }
    }

    /// <summary>
    /// Strategy for processing text with nested expressions recursively.
    /// </summary>
    public class RecursiveExpressionStrategy : ExpressionStrategy
    {
        private readonly JavaScriptExpressionStrategy _jsStrategy = new();
        private readonly CSharpExpressionStrategy _csStrategy = new();

        public override bool CanHandle(string expression)
        {
            if (!ContainsExpressions(expression))
                return false;

            // Handle text with expressions (single or multiple)
            return true;
        }

        public override object Evaluate(string expression, Dictionary<string, object> context, 
            string expectedResultType, Dictionary<string, object>? stepInfo = null)
        {
            stepInfo ??= new Dictionary<string, object>();

            try
            {
                // If it's a single expression without any other text, delegate to sub-strategies
                if (IsSingleExpression(expression))
                {
                    if (_jsStrategy.CanHandle(expression))
                        return _jsStrategy.Evaluate(expression, context, expectedResultType, stepInfo);
                    if (_csStrategy.CanHandle(expression))
                        return _csStrategy.Evaluate(expression, context, expectedResultType, stepInfo);
                }

                // Otherwise process recursively for mixed text with expressions
                var resultText = ProcessRecursive(expression, context, stepInfo);

                // After recursive processing, check if the result became a single expression
                if (IsSingleExpression(resultText))
                {
                    if (_jsStrategy.CanHandle(resultText))
                        return _jsStrategy.Evaluate(resultText, context, expectedResultType, stepInfo);
                    if (_csStrategy.CanHandle(resultText))
                        return _csStrategy.Evaluate(resultText, context, expectedResultType, stepInfo);
                }

                // Otherwise return as plain text (mixed text should not be JSON-encoded)
                return resultText;
            }
            catch (Exception e)
            {
                throw new ExpressionEvaluationException("Recursive", expression, stepInfo, e);
            }
        }

        public string ProcessRecursive(string text, Dictionary<string, object> context, Dictionary<string, object> stepInfo)
        {
            const int maxIterations = 100; // Protection against infinite loops
            int iteration = 0;

            while (ContainsExpressions(text) && iteration < maxIterations)
            {
                // Find ALL expressions (not just innermost)
                var expressions = FindAllExpressions(text);

                if (!expressions.Any())
                    break;

                // Process each expression (from right to left to preserve positions)
                bool processedAny = false;
                foreach (var exprInfo in expressions.OrderByDescending(e => e.Start))
                {
                    var exprText = exprInfo.Text;
                    var startPos = exprInfo.Start;
                    var endPos = exprInfo.End;

                    // Skip if this expression contains other expressions (process inner first)
                    var exprContent = exprText.Substring(3, exprText.Length - 6); // Remove {! !} or [! !]
                    if (ContainsExpressions(exprContent))
                        continue;

                    // Evaluate the expression
                    var result = EvaluateSingleExpression(exprText, context, stepInfo);

                    // Replace in text
                    text = text.Substring(0, startPos) + result?.ToString() + text.Substring(endPos);
                    processedAny = true;
                }

                // If no expressions were processed, break to avoid infinite loop
                if (!processedAny)
                    break;

                iteration++;
            }

            return text;
        }

        private List<ExpressionInfo> FindAllExpressions(string text)
        {
            var expressions = new List<ExpressionInfo>();

            // Find all expressions with proper nesting handling
            expressions.AddRange(FindExpressionsWithNesting(text, "{!", "!}"));
            expressions.AddRange(FindExpressionsWithNesting(text, "[!", "!]"));

            // Sort by position to process from left to right
            return expressions.OrderBy(e => e.Start).ToList();
        }

        private List<ExpressionInfo> FindExpressionsWithNesting(string text, string startMarker, string endMarker)
        {
            var expressions = new List<ExpressionInfo>();
            int i = 0;

            while (i < text.Length)
            {
                var startPos = text.IndexOf(startMarker, i);
                if (startPos == -1)
                    break;

                // Check for required space after start marker
                if (startPos + startMarker.Length >= text.Length || text[startPos + startMarker.Length] != ' ')
                {
                    i = startPos + 1;
                    continue;
                }

                // Find matching end marker with nesting support
                var endPos = FindMatchingEnd(text, startPos, startMarker, endMarker);
                if (endPos == -1)
                {
                    i = startPos + 1;
                    continue;
                }

                // Check for required space before end marker
                if (endPos == 0 || text[endPos - 1] != ' ')
                {
                    i = startPos + 1;
                    continue;
                }

                var exprText = text.Substring(startPos, endPos + endMarker.Length - startPos);
                expressions.Add(new ExpressionInfo
                {
                    Text = exprText,
                    Start = startPos,
                    End = endPos + endMarker.Length
                });

                i = endPos + 1;
            }

            return expressions;
        }

        private int FindMatchingEnd(string text, int startPos, string startMarker, string endMarker)
        {
            int nestingLevel = 0;
            int i = startPos + startMarker.Length;
            bool inString = false;
            char stringChar = '\0';
            bool escapeNext = false;

            while (i < text.Length)
            {
                if (escapeNext)
                {
                    escapeNext = false;
                    i++;
                    continue;
                }

                char currentChar = text[i];

                if (currentChar == '\\')
                {
                    escapeNext = true;
                    i++;
                    continue;
                }

                if (!inString)
                {
                    if (currentChar == '"' || currentChar == '\'' || currentChar == '`')
                    {
                        inString = true;
                        stringChar = currentChar;
                    }
                    else if (text.Substring(i).StartsWith(startMarker))
                    {
                        nestingLevel++;
                        i += startMarker.Length - 1;
                    }
                    else if (text.Substring(i).StartsWith(endMarker))
                    {
                        if (nestingLevel == 0)
                            return i;
                        nestingLevel--;
                        i += endMarker.Length - 1;
                    }
                }
                else
                {
                    if (currentChar == stringChar)
                    {
                        inString = false;
                        stringChar = '\0';
                    }
                }

                i++;
            }

            return -1;
        }

        private object? EvaluateSingleExpression(string exprText, Dictionary<string, object> context, 
            Dictionary<string, object> stepInfo)
        {
            if (_jsStrategy.CanHandle(exprText))
                return _jsStrategy.Evaluate(exprText, context, "dotnet", stepInfo);
            if (_csStrategy.CanHandle(exprText))
                return _csStrategy.Evaluate(exprText, context, "dotnet", stepInfo);

            // Fallback to plain text
            return exprText;
        }

        private bool ContainsExpressions(string text)
        {
            return (text.Contains("{! ") && text.Contains(" !}")) ||
                   (text.Contains("[! ") && text.Contains(" !]"));
        }

        private bool IsSingleExpression(string text)
        {
            text = text.Trim();
            return (text.StartsWith("{! ") && text.EndsWith(" !}")) ||
                   (text.StartsWith("[! ") && text.EndsWith(" !]"));
        }

        private class ExpressionInfo
        {
            public string Text { get; set; } = string.Empty;
            public int Start { get; set; }
            public int End { get; set; }
        }
    }

    /// <summary>
    /// Expression evaluation utilities.
    /// </summary>
    public static class ExpressionEvaluator
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger(typeof(ExpressionEvaluator));

        // Global registry of expression strategies
        private static readonly List<ExpressionStrategy> Strategies = new()
        {
            new JavaScriptExpressionStrategy(), // First - handles pure JS expressions: {! expr !}
            new CSharpExpressionStrategy(),     // Second - handles pure C# expressions: [! expr !]
            new RecursiveExpressionStrategy(),  // Third - handles mixed text with expressions
            new PlainTextStrategy()             // Last - fallback for plain text
        };

        /// <summary>
        /// Universal expression evaluation function.
        /// 
        /// Args:
        ///     expression: Expression to evaluate ("{! js_expr !}", "[! cs_expr !]", or plain text)
        ///     context: Dictionary of native .NET objects 
        ///     expectedResultType: "dotnet" (returns .NET objects) or "json" (returns JSON strings)
        ///     stepInfo: Pipeline step information for error reporting
        ///     
        /// Returns:
        ///     Evaluated result in specified format
        ///     
        /// Throws:
        ///     ExpressionEvaluationException: If evaluation fails
        /// </summary>
        public static object EvaluateExpression(string expression, Dictionary<string, object> context, 
            string expectedResultType = "dotnet", Dictionary<string, object>? stepInfo = null)
        {
            // Build context with built-in variables (env, etc.) + user context
            var fullContext = ExpressionHelpers.BuildDefaultContext(context);

            // Try each strategy
            foreach (var strategy in Strategies)
            {
                if (strategy.CanHandle(expression))
                {
                    Logger.LogDebug($"Using strategy: {strategy.GetType().Name}");
                    return strategy.Evaluate(expression, fullContext, expectedResultType, stepInfo);
                }
            }

            Logger.LogDebug("No strategy could handle expression, using fallback");
            // Fallback for plain text
            if (expectedResultType == "dotnet")
                return null!;
            else if (expectedResultType == "json")
                return "null";
            else
                throw new ArgumentException($"Unknown expected_result_type: {expectedResultType}");
        }

        /// <summary>
        /// Check if text contains any evaluable expressions.
        /// </summary>
        public static bool ContainsExpressions(string text)
        {
            return Regex.IsMatch(text, @"\{! .+ !\}|\[! .+ !\]");
        }

        /// <summary>
        /// Replace all expressions in text with their evaluated values.
        /// 
        /// Args:
        ///     text: Text containing expressions
        ///     context: Dictionary of native .NET objects
        ///     expectedResultType: "dotnet" or "json"  
        ///     stepInfo: Pipeline step information for error reporting
        ///     
        /// Returns:
        ///     Text with all expressions replaced by their evaluated values
        /// </summary>
        public static string SubstituteExpressions(string text, Dictionary<string, object> context, 
            string expectedResultType = "json", Dictionary<string, object>? stepInfo = null)
        {
            if (!ContainsExpressions(text))
                return text;

            // Use the global evaluation function that handles strategy selection properly
            var result = EvaluateExpression(text, context, expectedResultType, stepInfo);

            if (expectedResultType == "json" && result is not string)
                return JsonSerializer.Serialize(result);

            return result?.ToString() ?? "null";
        }

        /// <summary>
        /// Universal function for substituting expressions in objects.
        /// 
        /// Any string goes through the unified expression processing point.
        /// 
        /// Args:
        ///     obj: Object to process (Dictionary, List, string, or primitive)
        ///     context: Context with variables
        ///     stepInfo: Step information for debugging
        ///     preserveObjects: If true, single expressions return .NET objects,
        ///                      if false - all results are converted to strings
        ///     
        /// Returns:
        ///     Object with substituted expressions
        /// </summary>
        public static object SubstituteInObject(object obj, Dictionary<string, object> context, 
            Dictionary<string, object>? stepInfo = null, bool preserveObjects = false)
        {
            return obj switch
            {
                Dictionary<string, object> dict => dict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => SubstituteInObject(kvp.Value, context, stepInfo, preserveObjects)
                ),
                List<object> list => list.Select(item => SubstituteInObject(item, context, stepInfo, preserveObjects)).ToList(),
                string str when str.Contains("{!") || str.Contains("[!") => ProcessStringWithExpressions(str, context, stepInfo, preserveObjects),
                _ => obj
            };
        }

        private static object ProcessStringWithExpressions(string str, Dictionary<string, object> context, 
            Dictionary<string, object>? stepInfo, bool preserveObjects)
        {
            var strTrimmed = str.Trim();

            // Check if it's a single expression
            var isSingleExpression = (strTrimmed.StartsWith("{! ") && strTrimmed.EndsWith(" !}")) ||
                                   (strTrimmed.StartsWith("[! ") && strTrimmed.EndsWith(" !]"));

            if (preserveObjects && isSingleExpression)
            {
                // For single expressions with preserveObjects=true return .NET objects
                return EvaluateExpression(strTrimmed, context, "dotnet", stepInfo);
            }
            else if (isSingleExpression)
            {
                // For single expressions with preserveObjects=false return values without JSON quotes
                var result = EvaluateExpression(strTrimmed, context, "dotnet", stepInfo);
                // If result is string, return as is. If object - convert to JSON string
                if (result is string)
                    return result;
                else
                    return JsonSerializer.Serialize(result);
            }
            else
            {
                // For mixed text - return strings with proper substitution
                return SubstituteExpressions(str, context, "json", stepInfo);
            }
        }

        /// <summary>
        /// Parses a string after expression substitution into a native .NET object.
        /// 
        /// This function is for strategies that want to interpret a string
        /// as a .NET object (e.g., forEach needs a list, not a string "[1,2,3]").
        /// 
        /// Args:
        ///     text: String after expression substitution
        ///     context: Context (for debugging)
        ///     
        /// Returns:
        ///     .NET object or original string if parsing failed
        /// </summary>
        public static object ParseSubstitutedString(string text, Dictionary<string, object>? context = null)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.Trim();

            // Try to parse as JSON
            try
            {
                return JsonSerializer.Deserialize<object>(text) ?? text;
            }
            catch (JsonException)
            {
                // If not JSON, return as string
                return text;
            }
        }
    }
}
