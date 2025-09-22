using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpDotnet.Tools.LngMathCalculator;

/// <summary>
/// Mathematical calculator tool - .NET version of lng_math_calculator
/// Supports complex mathematical expressions, functions, and constants
/// </summary>
[McpServerToolType]
public static class Tool
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("lng_math_calculator");

    /// <summary>
    /// Tool name for registration
    /// </summary>
    public static string ToolName => "lng_math_calculator";

    /// <summary>
    /// Initialize the calculator tool
    /// </summary>
    public static void Init()
    {
        _logger.LogInformation("Initializing lng_math_calculator tool");
    }

    /// <summary>
    /// Calculate mathematical expressions
    /// </summary>
    /// <param name="expression">Mathematical expression for calculation</param>
    /// <returns>Calculation result in JSON format</returns>
    [McpServerTool]
    [Description("Performs mathematical calculations and operations including basic arithmetic, trigonometry, logarithms, and complex expressions")]
    public static Task<string> Calculate(
        [Description("Mathematical expression for calculation (supports +, -, *, /, ^, sin, cos, tan, log, ln, sqrt, abs, pi, e)")] 
        string expression)
    {
        try
        {
            _logger.LogInformation("Calculating expression: {Expression}", expression);
            
            if (string.IsNullOrWhiteSpace(expression))
            {
                var errorResult = new
                {
                    error = "No mathematical expression provided for calculation.",
                    originalExpression = expression ?? ""
                };
                return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }

            // Clean and normalize the expression
            var cleanExpression = NormalizeExpression(expression);
            _logger.LogDebug("Normalized expression: {CleanExpression}", cleanExpression);

            // Calculate the result
            var result = EvaluateExpression(cleanExpression);
            
            _logger.LogInformation("Calculation result: {Result}", result);
            
            // Create JSON result matching Python version
            var resultDict = new
            {
                originalExpression = expression,
                cleanedExpression = cleanExpression,
                result = result,
                resultType = result.GetType().Name.ToLower()
            };
            
            var jsonResult = System.Text.Json.JsonSerializer.Serialize(resultDict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            return Task.FromResult(jsonResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating expression '{Expression}': {Error}", expression, ex.Message);
            var errorResult = new
            {
                error = $"Error during calculation: {ex.Message}",
                originalExpression = expression
            };
            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
    }

    /// <summary>
    /// Normalize mathematical expression for evaluation (matching Python version)
    /// </summary>
    private static string NormalizeExpression(string expression)
    {
        // Remove extra spaces
        expression = expression.Trim();
        
        // Replace ^ with ** for power operations (but we'll use Math.Pow later)
        expression = expression.Replace("^", "**");
        
        // Replace mathematical constants
        expression = expression.Replace("pi", Math.PI.ToString("G17"));
        expression = expression.Replace("e", Math.E.ToString("G17"));
        
        // For .NET, we need to handle functions differently since we can't use eval
        // We'll keep the original approach but make it more robust
        return expression;
    }

    /// <summary>
    /// Evaluate mathematical expression with function support
    /// </summary>
    private static double EvaluateExpression(string expression)
    {
        try
        {
            // Handle mathematical functions first
            expression = ProcessMathFunctions(expression);
            
            // Replace ** with ^ for DataTable.Compute
            expression = expression.Replace("**", "^");
            
            // Use DataTable.Compute for basic arithmetic
            var table = new DataTable();
            var result = table.Compute(expression, null);
            
            if (result == DBNull.Value)
            {
                throw new ArgumentException("Invalid expression");
            }
            
            var doubleResult = Convert.ToDouble(result);
            
            // Round to 10 decimal places like Python version
            return Math.Round(doubleResult, 10);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid mathematical expression: {expression}", ex);
        }
    }
    
    /// <summary>
    /// Process mathematical functions in the expression
    /// </summary>
    private static string ProcessMathFunctions(string expression)
    {
        // Handle nested functions and parentheses properly
        var functions = new Dictionary<string, Func<double, double>>
        {
            ["sin"] = Math.Sin,
            ["cos"] = Math.Cos,
            ["tan"] = Math.Tan,
            ["log"] = Math.Log10,
            ["ln"] = Math.Log,
            ["sqrt"] = Math.Sqrt,
            ["abs"] = Math.Abs,
            ["floor"] = Math.Floor,
            ["ceil"] = Math.Ceiling
        };
        
        foreach (var function in functions)
        {
            var pattern = $@"\b{function.Key}\s*\(([^)]+)\)";
            expression = Regex.Replace(expression, pattern, match =>
            {
                var innerExpression = match.Groups[1].Value;
                var innerResult = EvaluateSimpleExpression(innerExpression);
                var result = function.Value(innerResult);
                return result.ToString("G17");
            });
        }
        
        return expression;
    }
    
    /// <summary>
    /// Evaluate simple expressions for function arguments
    /// </summary>
    private static double EvaluateSimpleExpression(string expression)
    {
        try
        {
            var table = new DataTable();
            var result = table.Compute(expression, null);
            return Convert.ToDouble(result);
        }
        catch
        {
            // If DataTable fails, try to parse as double
            if (double.TryParse(expression, out var value))
                return value;
            throw new ArgumentException($"Cannot evaluate: {expression}");
        }
    }
}
