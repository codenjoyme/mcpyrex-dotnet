using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpDotnet.Tools.LngJira.UploadAttachment;

/// <summary>
/// Jira Upload Attachment tool - .NET version of lng_jira_upload_attachment
/// </summary>
[McpServerToolType]
public static class Tool
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("lng_jira_upload_attachment");
    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// Tool name for registration
    /// </summary>
    public static string ToolName => "lng_jira_upload_attachment";

    /// <summary>
    /// Initialize the Jira upload attachment tool
    /// </summary>
    public static void Init()
    {
        _logger.LogInformation("Initializing lng_jira_upload_attachment tool");
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }

    /// <summary>
    /// Uploads a file as an attachment to a Jira ticket with optional comment.
    /// </summary>
    /// <param name="ticket_number">Jira ticket number (e.g., PROJ-123)</param>
    /// <param name="file_path">Path to the file to upload</param>
    /// <param name="comment">Optional comment to add when uploading</param>
    /// <param name="env_file">Optional: Path to .env file (default: .env)</param>
    /// <returns>Upload success confirmation with attachment details in JSON format</returns>
    [McpServerTool]
    [Description("Uploads a file as an attachment to a Jira ticket with optional comment. Parameters: ticket_number (string, required): Jira ticket number (e.g., PROJ-123); file_path (string, required): Path to the file to upload; comment (string, optional): Comment to add when uploading the attachment; env_file (string, optional): Path to .env file (default: .env). Environment Variables Required: JIRA_URL: Base URL of your Jira instance; JIRA_AUTH: Bearer token for Jira API authentication. Functionality: Uploads any file type as attachment to Jira ticket, validates file existence and readability before upload, optionally adds a comment to the ticket explaining the upload, handles various file sizes, returns detailed upload confirmation with attachment metadata.")]
    public static async Task<string> lng_jira_upload_attachment(
        [Description("Jira ticket number (e.g., PROJ-123)")] 
        string ticket_number,
        [Description("Path to the file to upload")] 
        string file_path,
        [Description("Optional comment to add when uploading")] 
        string? comment = null,
        [Description("Optional: Path to .env file (default: .env)")] 
        string env_file = ".env")
    {
        try
        {
            _logger.LogInformation("Uploading attachment to Jira ticket: {TicketNumber}, File: {FilePath}", ticket_number, file_path);

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

            if (string.IsNullOrWhiteSpace(file_path))
            {
                var errorResult = new
                {
                    success = false,
                    error = "file_path parameter is required"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Validate file
            var filePathExpanded = Path.IsPathRooted(file_path) ? file_path : Path.GetFullPath(file_path);

            if (!File.Exists(filePathExpanded))
            {
                var errorResult = new
                {
                    success = false,
                    error = $"File not found: {filePathExpanded}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            if (!File.GetAttributes(filePathExpanded).HasFlag(FileAttributes.Directory))
            {
                // It's a file, continue
            }
            else
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Path is not a file: {filePathExpanded}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get file info
            long fileSize;
            string fileName;
            double fileSizeKb;

            try
            {
                var fileInfo = new FileInfo(filePathExpanded);
                fileSize = fileInfo.Length;
                fileName = fileInfo.Name;
                fileSizeKb = Math.Round(fileSize / 1024.0, 2);
            }
            catch (Exception ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Failed to read file info: {ex.Message}"
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

            // Prepare upload URL
            var uploadUrl = $"{jiraUrl.TrimEnd('/')}/rest/api/2/issue/{ticket_number}/attachments";

            try
            {
                // Upload file
                using var content = new MultipartFormDataContent();
                using var fileStream = new FileStream(filePathExpanded, FileMode.Open, FileAccess.Read);
                using var streamContent = new StreamContent(fileStream);
                
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "file", fileName);

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {jiraAuth}");
                _httpClient.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check"); // Required to prevent CSRF errors

                var uploadResponse = await _httpClient.PostAsync(uploadUrl, content);

                if (uploadResponse.IsSuccessStatusCode)
                {
                    // Parse response
                    try
                    {
                        var responseContent = await uploadResponse.Content.ReadAsStringAsync();
                        var uploadData = JsonSerializer.Deserialize<JsonElement[]>(responseContent);

                        var result = new Dictionary<string, object>
                        {
                            ["success"] = true,
                            ["operation"] = "jira_upload_attachment",
                            ["ticket_number"] = ticket_number,
                            ["file_path"] = filePathExpanded,
                            ["file_name"] = fileName,
                            ["file_size_bytes"] = fileSize,
                            ["file_size_kb"] = fileSizeKb,
                            ["jira_url"] = jiraUrl,
                            ["attachments"] = new List<object>()
                        };

                        // Process uploaded attachments
                        var attachmentsList = (List<object>)result["attachments"];
                        foreach (var attachment in uploadData)
                        {
                            var attachmentInfo = new Dictionary<string, object?>
                            {
                                ["id"] = attachment.TryGetProperty("id", out var idElement) ? idElement.GetString() : null,
                                ["filename"] = attachment.TryGetProperty("filename", out var filenameElement) ? filenameElement.GetString() : null,
                                ["size"] = attachment.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : (long?)null,
                                ["mime_type"] = attachment.TryGetProperty("mimeType", out var mimeElement) ? mimeElement.GetString() : null,
                                ["created"] = attachment.TryGetProperty("created", out var createdElement) ? createdElement.GetString() : null,
                                ["content_url"] = attachment.TryGetProperty("content", out var contentElement) ? contentElement.GetString() : null
                            };

                            // Get author display name
                            if (attachment.TryGetProperty("author", out var authorElement) &&
                                authorElement.TryGetProperty("displayName", out var displayNameElement))
                            {
                                attachmentInfo["author"] = displayNameElement.GetString();
                            }
                            else
                            {
                                attachmentInfo["author"] = null;
                            }

                            attachmentsList.Add(attachmentInfo);
                        }

                        // Add comment if provided
                        if (!string.IsNullOrWhiteSpace(comment))
                        {
                            var commentUrl = $"{jiraUrl.TrimEnd('/')}/rest/api/2/issue/{ticket_number}/comment";
                            var commentData = new { body = comment };

                            try
                            {
                                _httpClient.DefaultRequestHeaders.Clear();
                                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {jiraAuth}");

                                using var commentContent = new StringContent(JsonSerializer.Serialize(commentData), System.Text.Encoding.UTF8, "application/json");
                                var commentResponse = await _httpClient.PostAsync(commentUrl, commentContent);

                                if (commentResponse.StatusCode == System.Net.HttpStatusCode.Created)
                                {
                                    result["comment_added"] = true;
                                    result["comment"] = comment;
                                }
                                else
                                {
                                    result["comment_added"] = false;
                                    result["comment_error"] = $"Failed to add comment: {(int)commentResponse.StatusCode}";
                                }
                            }
                            catch (Exception ex)
                            {
                                result["comment_added"] = false;
                                result["comment_error"] = $"Failed to add comment: {ex.Message}";
                            }
                        }

                        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                    }
                    catch (JsonException)
                    {
                        // Upload succeeded but response parsing failed
                        var responseText = await uploadResponse.Content.ReadAsStringAsync();
                        var result = new
                        {
                            success = true,
                            operation = "jira_upload_attachment",
                            ticket_number = ticket_number,
                            file_path = filePathExpanded,
                            file_name = fileName,
                            file_size_bytes = fileSize,
                            file_size_kb = fileSizeKb,
                            message = "File uploaded successfully but response parsing failed",
                            raw_response = responseText.Length > 1000 ? responseText.Substring(0, 1000) : responseText
                        };
                        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    }
                }
                else if (uploadResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = "Authentication failed. Check your API token.",
                        status_code = 401
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (uploadResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = "Permission denied. Check if you have permission to add attachments to this ticket.",
                        status_code = 403
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (uploadResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = $"Ticket {ticket_number} not found. Check the ticket number.",
                        status_code = 404
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (uploadResponse.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = $"File too large ({fileSizeKb} KB). Check Jira attachment size limits.",
                        status_code = 413
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    var responseText = await uploadResponse.Content.ReadAsStringAsync();
                    var errorResult = new
                    {
                        success = false,
                        error = $"Upload failed with status {(int)uploadResponse.StatusCode}",
                        status_code = (int)uploadResponse.StatusCode,
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
                    error = "Upload timeout. File may be too large or network connection is slow."
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
                    error = $"Upload failed: {ex.Message}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading attachment to Jira ticket '{TicketNumber}': {Error}", ticket_number, ex.Message);
            var errorResult = new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}"
            };
            return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
