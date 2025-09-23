using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpDotnet.Tools.LngCountWords;

/// <summary>
/// Word count tool - .NET version of lng_count_words
/// </summary>
[McpServerToolType]
public static class Tool
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("lng_count_words");

    /// <summary>
    /// Tool name for registration
    /// </summary>
    public static string ToolName => "lng_count_words";

    /// <summary>
    /// Initialize the word count tool
    /// </summary>
    public static void Init()
    {
        _logger.LogInformation("Initializing lng_count_words tool");
    }

    /// <summary>
    /// Count words in text
    /// </summary>
    /// <param name="input_text">Text to count words in</param>
    /// <returns>Word count result in JSON format</returns>
    [McpServerTool]
    [Description("Counts the number of words in the provided text.")]
    public static Task<string> lng_count_words(
        [Description("Text to count words in")] 
        string input_text)
    {
        try
        {
            _logger.LogInformation("Counting words in text: {Text}", input_text?.Substring(0, Math.Min(50, input_text?.Length ?? 0)));
            
            if (string.IsNullOrWhiteSpace(input_text))
            {
                var errorResult = new
                {
                    error = "No text provided for word counting.",
                    originalText = input_text ?? ""
                };
                return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }

            // Count words by splitting on whitespace (matching Python version)
            var words = input_text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var wordCount = words.Length;
            
            // Count characters
            var charCount = input_text.Length;
            var charCountNoSpaces = input_text.Replace(" ", "").Length;
            
            // Calculate additional statistics (matching Python version)
            var uniqueWords = words.Select(w => w.ToLower()).Distinct().Count();
            var avgWordLength = wordCount > 0 ? words.Average(w => w.Length) : 0.0;
            
            _logger.LogInformation("Word count result: {Count}", wordCount);
            
            // Create JSON result (matching Python version structure)
            var resultDict = new
            {
                wordCount = wordCount,
                uniqueWords = uniqueWords,
                charactersWithSpaces = charCount,
                charactersWithoutSpaces = charCountNoSpaces,
                averageWordLength = Math.Round(avgWordLength, 2)
            };
            
            var jsonResult = System.Text.Json.JsonSerializer.Serialize(resultDict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            return Task.FromResult(jsonResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting words in text '{Text}': {Error}", input_text, ex.Message);
            var errorResult = new
            {
                error = $"Error during word counting: {ex.Message}",
                originalText = input_text
            };
            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
