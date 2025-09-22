using ModelContextProtocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using McpDotnet;
using System.Text.Json;
using System.ComponentModel;

namespace McpDotnet;

/// <summary>
/// Math Calculator Tools for MCP Server
/// Contains mathematical calculation methods exposed as MCP tools
/// </summary>
[McpServerToolType]
public static class MathCalculatorTools
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("math_calculator");

    /// <summary>
    /// Performs basic mathematical calculations
    /// </summary>
    /// <param name="expression">Mathematical expression to calculate</param>
    /// <returns>JSON result with calculation details</returns>
    [McpServerTool, Description("Performs mathematical calculations and operations")]
    public static async Task<string> CalculateAsync(
        [Description("Mathematical expression for calculation")] string expression)
    {
        _logger.LogInformation("Calculating expression: {Expression}", expression);
        
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                var errorResult = new
                {
                    error = "No mathematical expression provided for calculation.",
                    originalExpression = expression ?? ""
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Clean and prepare the expression
            var cleanedExpression = CleanExpression(expression);
            
            // Calculate the result (simple evaluation for now)
            var result = EvaluateExpression(cleanedExpression);
            
            // Create JSON result (matching Python version format)
            var resultObject = new
            {
                originalExpression = expression,
                cleanedExpression = cleanedExpression,
                result = result,
                resultType = result.GetType().Name
            };
            
            var jsonResult = JsonSerializer.Serialize(resultObject, new JsonSerializerOptions { WriteIndented = true });
            
            _logger.LogInformation("Calculation completed: {Expression} = {Result}", expression, result);
            
            return jsonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating expression: {Expression}", expression);
            
            var errorResult = new
            {
                error = $"Error during calculation: {ex.Message}",
                originalExpression = expression
            };
            
            return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Simple greeting tool for testing
    /// </summary>
    /// <param name="name">Name to greet</param>
    /// <returns>Greeting message</returns>
    [McpServerTool, Description("Simple greeting tool for testing MCP functionality")]
    public static async Task<string> GreetAsync(
        [Description("Name of the person to greet")] string name = "World")
    {
        _logger.LogInformation("Greeting: {Name}", name);
        
        var greeting = $"Hello, {name}! This is .NET MCP Server working! 🎉";
        
        var result = new
        {
            message = greeting,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            server = ".NET MCP Server v1.0.0"
        };
        
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Clean and prepare mathematical expression
    /// </summary>
    private static string CleanExpression(string expr)
    {
        if (string.IsNullOrEmpty(expr)) return "";
        
        // Remove extra spaces
        expr = expr.Trim();
        
        // Replace ^ with ** for power operations
        expr = expr.Replace("^", "**");
        
        // Replace constants (simple version)
        expr = expr.Replace("pi", Math.PI.ToString());
        expr = expr.Replace("e", Math.E.ToString());
        
        return expr;
    }

    /// <summary>
    /// Simple expression evaluator (basic operations only)
    /// Note: In production, use a proper expression parser
    /// </summary>
    private static double EvaluateExpression(string expr)
    {
        // Very basic calculator - just for demo
        // In real implementation, use System.Data.DataTable.Compute or proper parser
        
        try
        {
            // Handle simple operations
            if (expr.Contains("+"))
            {
                var parts = expr.Split('+');
                if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out var a) && double.TryParse(parts[1].Trim(), out var b))
                    return a + b;
            }
            
            if (expr.Contains("-") && !expr.StartsWith("-"))
            {
                var parts = expr.Split('-');
                if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out var a) && double.TryParse(parts[1].Trim(), out var b))
                    return a - b;
            }
            
            if (expr.Contains("*") && !expr.Contains("**"))
            {
                var parts = expr.Split('*');
                if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out var a) && double.TryParse(parts[1].Trim(), out var b))
                    return a * b;
            }
            
            if (expr.Contains("/"))
            {
                var parts = expr.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out var a) && double.TryParse(parts[1].Trim(), out var b))
                {
                    if (b == 0) throw new DivideByZeroException("Division by zero");
                    return a / b;
                }
            }
            
            // If no operations found, try to parse as number
            if (double.TryParse(expr, out var number))
                return number;
            
            throw new ArgumentException($"Unsupported expression: {expr}");
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error evaluating expression '{expr}': {ex.Message}", ex);
        }
    }
}
