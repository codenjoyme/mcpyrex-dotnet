````markdown
# Jira Integration Tools (.NET)

This category provides comprehensive Jira integration tools for ticket management and file operations.

## Tools Available

### 1. `lng_jira_get_description`
Retrieves detailed information from a Jira ticket including description, metadata, and status.

**Features:**
- Gets complete ticket information (summary, description, status, assignee, etc.)
- Optional JSON file output
- Comprehensive error handling
- Authentication validation

### 2. `lng_jira_download_attachments`
Downloads all attachments from a Jira ticket to a specified directory.

**Features:**
- Downloads all ticket attachments
- Safe filename sanitization
- Progress tracking and detailed reporting
- Handles various file types and sizes
- Creates directories as needed

### 3. `lng_jira_upload_attachment`
Uploads a file as an attachment to a Jira ticket with optional comment.

**Features:**
- Upload any file type to Jira tickets
- Optional comment addition
- File validation before upload
- Detailed upload confirmation
- Handles large files efficiently

## Configuration

All tools require environment variables in a `.env` file:

```bash
# Jira Configuration
JIRA_URL=https://yourcompany.atlassian.net
JIRA_AUTH=your_bearer_token_here
```

### Getting Your Jira Bearer Token

1. Go to your Jira account settings
2. Create an API token
3. Use the token as your `JIRA_AUTH` value

## Usage Examples

### Get Ticket Information
```bash
dotnet run --project .mcp-dotnet/run.csproj -- run lng_jira_get_description '{\"ticket_number\": \"PROJ-123\"}'
```

### Download All Attachments
```bash
dotnet run --project .mcp-dotnet/run.csproj -- run lng_jira_download_attachments '{\"ticket_number\": \"PROJ-123\", \"output_directory\": \"./work/PROJ-123\"}'
```

### Upload File to Ticket
```bash
dotnet run --project .mcp-dotnet/run.csproj -- run lng_jira_upload_attachment '{\"ticket_number\": \"PROJ-123\", \"file_path\": \"document.pdf\", \"comment\": \"Updated documentation\"}'
```

## Integration with PDF Processing

These tools work seamlessly with the `lng_pdf_extract_images` tool for the complete Jira PDF workflow:

1. Use `lng_jira_download_attachments` to get PDF files from tickets
2. Use `lng_pdf_extract_images` to extract images from PDFs
3. Use `lng_jira_upload_attachment` to upload extracted images back to tickets

## Error Handling

All tools provide comprehensive error handling for:
- Authentication failures
- Permission issues
- Network connectivity problems
- File system errors
- Jira API rate limits

## Dependencies

- `System.Net.Http`: HTTP client for API calls
- `System.Text.Json`: JSON serialization
- `System.Text.RegularExpressions`: Filename sanitization

````
