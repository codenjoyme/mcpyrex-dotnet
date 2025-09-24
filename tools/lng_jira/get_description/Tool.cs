using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpDotnet.Tools.LngJira.GetDescription;

/// <summary>
/// Jira Get Description tool - .NET version of lng_jira_get_description
/// </summary>
[McpServerToolType]
public static class Tool
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("lng_jira_get_description");
    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// Tool name for registration
    /// </summary>
    public static string ToolName => "lng_jira_get_description";

    /// <summary>
    /// Initialize the Jira get description tool
    /// </summary>
    public static void Init()
    {
        _logger.LogInformation("Initializing lng_jira_get_description tool");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Retrieves detailed information from a Jira ticket including description, metadata, and status.
    /// </summary>
    /// <param name="ticket_number">Jira ticket number (e.g., PROJ-123)</param>
    /// <param name="output_file">Optional: Path to save the JSON output file</param>
    /// <param name="env_file">Optional: Path to .env file (default: .env)</param>
    /// <returns>Complete ticket information in JSON format</returns>
    [McpServerTool]
    [Description("Retrieves detailed information from a Jira ticket including description, metadata, and status. Parameters: ticket_number (string, required): Jira ticket number (e.g., PROJ-123); output_file (string, optional): Path to save the JSON output file; env_file (string, optional): Path to .env file (default: .env). Environment Variables Required: JIRA_URL: Base URL of your Jira instance; JIRA_AUTH: Bearer token for Jira API authentication. Returns: Complete ticket information including summary, description, status, assignee, reporter, created/updated dates, priority, issue type, JSON formatted response with all field details.")]
    public static async Task<string> lng_jira_get_description(
        [Description("Jira ticket number (e.g., PROJ-123)")] 
        string ticket_number,
        [Description("Optional: Path to save the JSON output file")] 
        string? output_file = null,
        [Description("Optional: Path to .env file (default: .env)")] 
        string env_file = ".env")
    {
        try
        {
            _logger.LogInformation("Getting Jira ticket description for: {TicketNumber}", ticket_number);

            // Validate required parameter
            if (string.IsNullOrWhiteSpace(ticket_number))
            {
                var errorResult = new
                {
                    success = false,
                    error = "ticket_number parameter is required"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Load environment variables
            var envPath = Path.IsPathRooted(env_file) ? env_file : Path.GetFullPath(env_file);
            
            if (!File.Exists(envPath))
            {
                var errorResult = new
                {
                    success = false,
                    error = $".env file not found at {envPath}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Read .env file
            var envVars = new Dictionary<string, string>();
            foreach (var line in await File.ReadAllLinesAsync(envPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    envVars[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Get environment variables
            if (!envVars.TryGetValue("JIRA_URL", out var jiraUrl) || string.IsNullOrWhiteSpace(jiraUrl))
            {
                var errorResult = new
                {
                    success = false,
                    error = "JIRA_URL not found in environment variables"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            if (!envVars.TryGetValue("JIRA_AUTH", out var jiraAuth) || string.IsNullOrWhiteSpace(jiraAuth))
            {
                var errorResult = new
                {
                    success = false,
                    error = "JIRA_AUTH not found in environment variables"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Prepare API request
            var apiUrl = $"{jiraUrl.TrimEnd('/')}/rest/api/2/issue/{ticket_number}";
            var fields = "summary,description,status,assignee,reporter,created,updated,priority,issuetype";
            var fullUrl = $"{apiUrl}?fields={fields}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {jiraAuth}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                // Make API request
                var response = await _httpClient.GetAsync(fullUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var ticketData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    // Save to file if requested
                    string? fileSaveError = null;
                    if (!string.IsNullOrWhiteSpace(output_file))
                    {
                        try
                        {
                            var outputPath = Path.IsPathRooted(output_file) ? output_file : Path.GetFullPath(output_file);
                            
                            // Ensure directory exists
                            var directory = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrEmpty(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }
                            
                            await File.WriteAllTextAsync(outputPath, responseContent, System.Text.Encoding.UTF8);
                        }
                        catch (Exception ex)
                        {
                            // Still return data even if file save fails
                            fileSaveError = $"Warning: Failed to save to file {output_file}: {ex.Message}";
                        }
                    }

                    // Prepare result
                    var result = new Dictionary<string, object>
                    {
                        ["success"] = true,
                        ["operation"] = "jira_get_description",
                        ["ticket_number"] = ticket_number,
                        ["jira_url"] = jiraUrl,
                        ["data"] = ticketData
                    };

                    if (!string.IsNullOrWhiteSpace(output_file))
                    {
                        result["output_file"] = output_file;
                    }

                    if (!string.IsNullOrEmpty(fileSaveError))
                    {
                        // Add file save error to the ticket data (similar to Python version)
                        var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                        dataDict["_file_save_error"] = fileSaveError;
                        result["data"] = dataDict;
                    }

                    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = "Authentication failed. Check your API token.",
                        status_code = 401
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = "Permission denied. Check if you have access to this ticket.",
                        status_code = 403
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = $"Ticket {ticket_number} not found. Check the ticket number.",
                        status_code = 404
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    var errorResult = new
                    {
                        success = false,
                        error = $"API request failed with status {(int)response.StatusCode}",
                        status_code = (int)response.StatusCode,
                        response = responseText.Length > 1000 ? responseText.Substring(0, 1000) : responseText
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
            }
            catch (TaskCanceledException)
            {
                var errorResult = new
                {
                    success = false,
                    error = "Request timeout. Check your network connection or Jira server availability."
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (HttpRequestException ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Connection error. Check if Jira URL is correct: {jiraUrl}. Details: {ex.Message}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Request failed: {ex.Message}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Jira ticket description for '{TicketNumber}': {Error}", ticket_number, ex.Message);
            var errorResult = new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}"
            };
            return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
