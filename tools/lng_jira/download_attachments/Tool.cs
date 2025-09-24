using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpDotnet.Tools.LngJira.DownloadAttachments;

/// <summary>
/// Jira Download Attachments tool - .NET version of lng_jira_download_attachments
/// </summary>
[McpServerToolType]
public static class Tool
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("lng_jira_download_attachments");
    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// Tool name for registration
    /// </summary>
    public static string ToolName => "lng_jira_download_attachments";

    /// <summary>
    /// Initialize the Jira download attachments tool
    /// </summary>
    public static void Init()
    {
        _logger.LogInformation("Initializing lng_jira_download_attachments tool");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Sanitize filename to be safe for filesystem
    /// </summary>
    /// <param name="filename">Original filename</param>
    /// <returns>Sanitized filename</returns>
    private static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "unnamed_file";

        // Remove or replace invalid characters
        var invalidChars = "<>:\"/\\|?*";
        foreach (char c in invalidChars)
        {
            filename = filename.Replace(c, '_');
        }

        // Remove control characters
        filename = Regex.Replace(filename, @"[\x00-\x1f\x7f]", "");

        // Limit length and strip whitespace
        filename = filename.Trim();
        if (filename.Length > 255)
        {
            filename = filename.Substring(0, 255);
        }

        // Ensure it's not empty
        if (string.IsNullOrWhiteSpace(filename))
        {
            filename = "unnamed_file";
        }

        return filename;
    }

    /// <summary>
    /// Downloads all attachments from a Jira ticket to a specified directory.
    /// </summary>
    /// <param name="ticket_number">Jira ticket number (e.g., PROJ-123)</param>
    /// <param name="output_directory">Directory to save downloaded attachments</param>
    /// <param name="env_file">Optional: Path to .env file (default: .env)</param>
    /// <returns>List of successfully downloaded files with metadata in JSON format</returns>
    [McpServerTool]
    [Description("Downloads all attachments from a Jira ticket to a specified directory. Parameters: ticket_number (string, required): Jira ticket number (e.g., PROJ-123); output_directory (string, required): Directory to save downloaded attachments; env_file (string, optional): Path to .env file (default: .env). Environment Variables Required: JIRA_URL: Base URL of your Jira instance; JIRA_AUTH: Bearer token for Jira API authentication. Functionality: Downloads all attachments from the specified ticket, sanitizes filenames to be filesystem-safe, creates output directory if it doesn't exist, provides detailed download progress and summary, handles various file types and sizes.")]
    public static async Task<string> lng_jira_download_attachments(
        [Description("Jira ticket number (e.g., PROJ-123)")] 
        string ticket_number,
        [Description("Directory to save downloaded attachments")] 
        string output_directory,
        [Description("Optional: Path to .env file (default: .env)")] 
        string env_file = ".env")
    {
        try
        {
            _logger.LogInformation("Downloading attachments from Jira ticket: {TicketNumber}", ticket_number);

            // Validate required parameters
            if (string.IsNullOrWhiteSpace(ticket_number))
            {
                var errorResult = new
                {
                    success = false,
                    error = "ticket_number parameter is required"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.IsNullOrWhiteSpace(output_directory))
            {
                var errorResult = new
                {
                    success = false,
                    error = "output_directory parameter is required"
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

            // Prepare output directory
            var outputDir = Path.IsPathRooted(output_directory) ? output_directory : Path.GetFullPath(output_directory);

            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Failed to create output directory {outputDir}: {ex.Message}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get ticket information with attachments
            var apiUrl = $"{jiraUrl.TrimEnd('/')}/rest/api/2/issue/{ticket_number}";
            var fields = "attachment,summary";
            var fullUrl = $"{apiUrl}?fields={fields}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {jiraAuth}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                // Get ticket data
                var response = await _httpClient.GetAsync(fullUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg;
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        errorMsg = "Authentication failed. Check your API token.";
                    else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        errorMsg = "Permission denied. Check if you have access to this ticket.";
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        errorMsg = $"Ticket {ticket_number} not found. Check the ticket number.";
                    else
                        errorMsg = $"API request failed with status {(int)response.StatusCode}";

                    var errorResult = new
                    {
                        success = false,
                        error = errorMsg,
                        status_code = (int)response.StatusCode
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var ticketData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                // Check if ticket has attachments
                var attachments = new List<JsonElement>();
                if (ticketData.TryGetProperty("fields", out var fieldsElement) && 
                    fieldsElement.TryGetProperty("attachment", out var attachmentArray))
                {
                    foreach (var attachment in attachmentArray.EnumerateArray())
                    {
                        attachments.Add(attachment);
                    }
                }

                var ticketSummary = "";
                if (fieldsElement.TryGetProperty("summary", out var summaryElement))
                {
                    ticketSummary = summaryElement.GetString() ?? "";
                }

                if (attachments.Count == 0)
                {
                    var result = new
                    {
                        success = true,
                        operation = "jira_download_attachments",
                        ticket_number = ticket_number,
                        ticket_summary = ticketSummary,
                        output_directory = outputDir,
                        message = "No attachments found for this ticket",
                        downloaded_files = new object[0]
                    };
                    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                }

                // Download each attachment
                var downloadedFiles = new List<object>();
                var failedDownloads = new List<object>();

                foreach (var attachment in attachments)
                {
                    var originalFilename = attachment.GetProperty("filename").GetString() ?? "unknown";
                    var filename = SanitizeFilename(originalFilename);
                    var filePath = Path.Combine(outputDir, filename);

                    try
                    {
                        var contentUrl = attachment.GetProperty("content").GetString();
                        if (string.IsNullOrEmpty(contentUrl))
                        {
                            failedDownloads.Add(new
                            {
                                filename = originalFilename,
                                error = "No content URL found"
                            });
                            continue;
                        }

                        // Download attachment
                        using var downloadResponse = await _httpClient.GetAsync(contentUrl);
                        
                        if (downloadResponse.IsSuccessStatusCode)
                        {
                            using var fileStream = new FileStream(filePath, FileMode.Create);
                            await downloadResponse.Content.CopyToAsync(fileStream);

                            // Verify file was created and get size
                            if (File.Exists(filePath))
                            {
                                var actualSize = new FileInfo(filePath).Length;
                                var expectedSize = attachment.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;

                                var authorName = "Unknown";
                                if (attachment.TryGetProperty("author", out var authorElement) &&
                                    authorElement.TryGetProperty("displayName", out var displayNameElement))
                                {
                                    authorName = displayNameElement.GetString() ?? "Unknown";
                                }

                                var mimeType = attachment.TryGetProperty("mimeType", out var mimeElement) ? mimeElement.GetString() : "unknown";
                                var created = attachment.TryGetProperty("created", out var createdElement) ? createdElement.GetString() : "";

                                var fileInfo = new
                                {
                                    filename = filename,
                                    original_filename = originalFilename,
                                    file_path = filePath,
                                    size_bytes = actualSize,
                                    expected_size_bytes = expectedSize,
                                    mime_type = mimeType ?? "unknown",
                                    author = authorName,
                                    created = created ?? "",
                                    size_match = actualSize == expectedSize
                                };

                                downloadedFiles.Add(fileInfo);
                            }
                            else
                            {
                                failedDownloads.Add(new
                                {
                                    filename = originalFilename,
                                    error = "File was not created after download"
                                });
                            }
                        }
                        else
                        {
                            failedDownloads.Add(new
                            {
                                filename = originalFilename,
                                error = $"Download failed with status {(int)downloadResponse.StatusCode}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        failedDownloads.Add(new
                        {
                            filename = originalFilename,
                            error = $"Download error: {ex.Message}"
                        });
                    }
                }

                // Prepare final result
                var finalResult = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["operation"] = "jira_download_attachments",
                    ["ticket_number"] = ticket_number,
                    ["ticket_summary"] = ticketSummary,
                    ["jira_url"] = jiraUrl,
                    ["output_directory"] = outputDir,
                    ["total_attachments"] = attachments.Count,
                    ["downloaded_count"] = downloadedFiles.Count,
                    ["failed_count"] = failedDownloads.Count,
                    ["downloaded_files"] = downloadedFiles
                };

                if (failedDownloads.Count > 0)
                {
                    finalResult["failed_downloads"] = failedDownloads;
                }

                return JsonSerializer.Serialize(finalResult, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
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
            _logger.LogError(ex, "Error downloading attachments from Jira ticket '{TicketNumber}': {Error}", ticket_number, ex.Message);
            var errorResult = new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}"
            };
            return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
