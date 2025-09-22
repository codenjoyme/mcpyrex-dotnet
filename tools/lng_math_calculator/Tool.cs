using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpDotnet.Tools.LngMathCalculator;

/// <summary>
/// Mathematical calculator tool - .NET version of lng_math_calculator
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
    [Description(@"Performs mathematical calculations and operations.

**Parameters:**
- `expression` (string, required): Mathematical expression for calculation.

**Supported operations:**
- Basic arithmetic operations: +, -, *, /
- Power: ** or ^
- Parentheses for grouping: ()
- Mathematical functions: sin, cos, tan, log, ln, sqrt, abs
- Constants: pi, e
- Integer division: //
- Modulo: %

**Usage examples:**
- ""2 + 3 * 4"" → 14
- ""sqrt(16) + 2^3"" → 12
- ""sin(pi/2)"" → 1
- ""(10 + 5) * 2"" → 30
- ""log(100)"" → 2

This tool is useful for performing complex mathematical calculations.")]
    public static Task<string> Calculate(
        [Description(@"Mathematical expression for calculation. 
        Supports: +, -, *, /, ^, sin, cos, tan, log, ln, sqrt, abs, floor, ceil, pi, e.
        Examples: '2+3*4', 'sqrt(25)', 'sin(pi/2)', 'log(100)', '2^3+1'")] 
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
    /// Cleans and prepares the mathematical expression (matching Python version)
    /// </summary>
    private static string NormalizeExpression(string expression)
    {
        // Remove extra spaces
        expression = expression.Trim();
        
        // Replace ^ with ** (we'll handle ** as power later)
        expression = expression.Replace("^", "**");
        
        // Replace constants
        expression = expression.Replace("pi", Math.PI.ToString("G17"));
        expression = expression.Replace("e", Math.E.ToString("G17"));
        
        // Replace mathematical functions with placeholders for later processing
        // This matches Python's approach of replacing functions before evaluation
        var mathFunctions = new Dictionary<string, string>
        {
            ["sin"] = "MATH_SIN",
            ["cos"] = "MATH_COS", 
            ["tan"] = "MATH_TAN",
            ["log"] = "MATH_LOG10",
            ["ln"] = "MATH_LOG",
            ["sqrt"] = "MATH_SQRT",
            ["abs"] = "MATH_ABS",
            ["floor"] = "MATH_FLOOR",
            ["ceil"] = "MATH_CEIL"
        };
        
        foreach (var func in mathFunctions)
        {
            // Use regex for precise function replacement (matching Python's approach)
            var pattern = @"\b" + func.Key + @"\b";
            expression = Regex.Replace(expression, pattern, func.Value);
        }
        
        return expression;
    }

    /// <summary>
    /// Safely evaluates a mathematical expression (matching Python version)
    /// </summary>
    private static double EvaluateExpression(string expression)
    {
        try
        {
            // Check for forbidden elements (similar to Python safety checks)
            var forbidden = new[] { "import", "__", "exec", "eval", "open", "file", "input" };
            foreach (var item in forbidden)
            {
                if (expression.Contains(item))
                {
                    throw new ValueError($"Forbidden element '{item}' in expression");
                }
            }
            
            // Process the expression recursively (similar to Python's eval approach)
            var result = ProcessExpression(expression);
            
            // Round float to 10 decimal places like Python
            if (result is double doubleResult)
            {
                return Math.Round(doubleResult, 10);
            }
            
            return Convert.ToDouble(result);
        }
        catch (DivideByZeroException)
        {
            throw new ValueError("Division by zero");
        }
        catch (OverflowException)
        {
            throw new ValueError("Result too large");
        }
        catch (Exception ex)
        {
            throw new ValueError($"Calculation error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Process expression recursively (similar to Python's eval)
    /// </summary>
    private static double ProcessExpression(string expression)
    {
        // Handle mathematical functions
        expression = ProcessMathFunctions(expression);
        
        // Handle power operations (**) 
        expression = ProcessPowerOperations(expression);
        
        // Use DataTable.Compute for remaining arithmetic
        var table = new DataTable();
        var result = table.Compute(expression, null);
        
        if (result == DBNull.Value)
        {
            throw new ArgumentException("Invalid expression");
        }
        
        return Convert.ToDouble(result);
    }
    
    /// <summary>
    /// Process mathematical functions (similar to Python's function replacement)
    /// </summary>
    private static string ProcessMathFunctions(string expression)
    {
        var functions = new Dictionary<string, Func<double, double>>
        {
            ["MATH_SIN"] = Math.Sin,
            ["MATH_COS"] = Math.Cos,
            ["MATH_TAN"] = Math.Tan,
            ["MATH_LOG10"] = Math.Log10,
            ["MATH_LOG"] = Math.Log,
            ["MATH_SQRT"] = Math.Sqrt,
            ["MATH_ABS"] = Math.Abs,
            ["MATH_FLOOR"] = Math.Floor,
            ["MATH_CEIL"] = Math.Ceiling
        };
        
        foreach (var function in functions)
        {
            var pattern = $@"{function.Key}\s*\(([^)]+)\)";
            expression = Regex.Replace(expression, pattern, match =>
            {
                var innerExpression = match.Groups[1].Value;
                var innerResult = ProcessExpression(innerExpression); // Recursive evaluation
                var result = function.Value(innerResult);
                return result.ToString("G17");
            });
        }
        
        return expression;
    }
    
    /// <summary>
    /// Process power operations (**) similar to Python
    /// </summary>
    private static string ProcessPowerOperations(string expression)
    {
        // Handle ** operations (right-associative like Python)
        var pattern = @"([0-9.]+|\([^)]+\))\s*\*\*\s*([0-9.]+|\([^)]+\))";
        while (Regex.IsMatch(expression, pattern))
        {
            expression = Regex.Replace(expression, pattern, match =>
            {
                var baseStr = match.Groups[1].Value;
                var expStr = match.Groups[2].Value;
                
                var baseVal = baseStr.StartsWith("(") ? 
                    ProcessExpression(baseStr.Trim('(', ')')) : 
                    double.Parse(baseStr);
                    
                var expVal = expStr.StartsWith("(") ? 
                    ProcessExpression(expStr.Trim('(', ')')) : 
                    double.Parse(expStr);
                
                var result = Math.Pow(baseVal, expVal);
                return result.ToString("G17");
            });
        }
        
        return expression;
    }
}

/// <summary>
/// Custom exception class for value errors (similar to Python ValueError)
/// </summary>
public class ValueError : Exception
{
    public ValueError(string message) : base(message) { }
    public ValueError(string message, Exception innerException) : base(message, innerException) { }
}
