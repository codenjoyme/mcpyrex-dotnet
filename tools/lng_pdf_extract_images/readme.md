# PDF Image Extraction Tool

## Overview
The `lng_pdf_extract_images` tool extracts images from PDF files using UglyToad.PdfPig library with support for PNG and JPEG output formats.

## Features
- Extract images from PDF files
- Support for PNG and JPEG output formats (PNG as default)
- Sequential numbering for extracted images (image_001.png, image_002.png, etc.)
- Custom output directory specification
- Comprehensive metadata about extracted images
- Detailed error handling

## Usage

### Basic Usage
Extract all images as PNG (default):
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"document.pdf"}'
```

### Extract as JPEG
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"document.pdf","image_format":"jpeg"}'
```

### Custom Output Directory
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"document.pdf","output_dir":"./extracted_images"}'
```

## Parameters
- `pdf_path` (required): Path to the PDF file to process
- `output_dir` (optional): Custom output directory for extracted images. Defaults to same directory as PDF
- `image_format` (optional): Output format - "png" (default) or "jpeg"

## Return Data
The tool returns a JSON object containing:
- `success`: Boolean indicating if extraction was successful
- `pdf_path`: Full path to the PDF file processed
- `output_directory`: Directory where images were saved
- `image_format`: Format used for extracted images
- `total_images`: Total number of images extracted
- `images`: Array of extracted image information including:
  - `file_path`: Full path to extracted image
  - `file_name`: Name of the image file
  - `page_number`: PDF page number where image was found
  - `image_number`: Sequential number of the image
  - `width`: Image width in pixels
  - `height`: Image height in pixels
  - `format`: Image format (png/jpeg)
- `summary`: Summary information about the extraction

## Error Handling
The tool provides detailed error messages for:
- Missing or invalid `pdf_path` parameter
- File not found errors
- Invalid image format specifications
- Password-protected or encrypted PDFs
- Permission errors
- Corrupted or unreadable PDFs
- PDFs with no images

## Example Output
```json
{
  "success": true,
  "pdf_path": "/path/to/document.pdf",
  "output_directory": "/path/to/output",
  "image_format": "png",
  "total_images": 3,
  "images": [
    {
      "file_path": "/path/to/output/image_001.png",
      "file_name": "image_001.png",
      "page_number": 1,
      "image_number": 1,
      "width": 800,
      "height": 600,
      "format": "png"
    }
  ],
  "summary": {
    "pdf_file": "document.pdf",
    "images_extracted": 3,
    "output_location": "/path/to/output",
    "format_used": "png"
  }
}
```

## Integration with Jira Workflow
This tool is designed to work seamlessly with Jira attachment processing:

1. Use `lng_jira_download_attachments` to download PDF attachments
2. Use `lng_pdf_extract_images` to extract images from the PDFs
3. Use `lng_jira_upload_attachment` to upload the extracted images back to Jira

## Dependencies
- UglyToad.PdfPig (1.7.0-custom-5) - PDF processing and image extraction
- SixLabors.ImageSharp (3.1.11) - Image format conversion and manipulation
