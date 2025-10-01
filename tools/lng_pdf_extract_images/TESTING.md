# Testing lng_pdf_extract_images Tool

## Quick Tests

### Test 1: Missing Required Parameter
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{}'
```
**Expected Result:** Error message indicating pdf_path is required

### Test 2: File Not Found
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"nonexistent.pdf"}'
```
**Expected Result:** Error message indicating file not found with full path

### Test 3: Invalid Image Format
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"test.pdf","image_format":"gif"}'
```
**Expected Result:** Error message indicating invalid format, must be png or jpeg

### Test 4: Valid PDF with Images (PNG format)
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/document.pdf"}'
```
**Expected Result:** JSON with success=true, list of extracted images with metadata

### Test 5: Valid PDF with Images (JPEG format)
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/document.pdf","image_format":"jpeg"}'
```
**Expected Result:** JSON with success=true, images saved as JPEG

### Test 6: Valid PDF with Custom Output Directory
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/document.pdf","output_dir":"./extracted"}'
```
**Expected Result:** JSON with success=true, images saved to ./extracted directory

### Test 7: PDF with No Images
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"path/to/text-only.pdf"}'
```
**Expected Result:** Error message indicating no images found in PDF

## Integration Tests with Jira Workflow

### Complete Workflow Test
1. Download PDF from Jira ticket:
```bash
dotnet run --project Run.csproj -- run lng_jira_download_attachments '{"ticket_number":"PROJ-123","output_directory":"./work/PROJ-123"}'
```

2. Extract images from PDF:
```bash
dotnet run --project Run.csproj -- run lng_pdf_extract_images '{"pdf_path":"./work/PROJ-123/document.pdf","output_dir":"./work/PROJ-123/images"}'
```

3. Upload images back to Jira:
```bash
dotnet run --project Run.csproj -- run lng_jira_upload_attachment '{"ticket_number":"PROJ-123","file_path":"./work/PROJ-123/images/image_001.png"}'
```

## Test Results Summary

✅ **Test 1 (Missing Parameter):** PASSED - Returns proper error
✅ **Test 2 (File Not Found):** PASSED - Returns file not found error  
✅ **Test 3 (Invalid Format):** PASSED - Returns format validation error
⏸️ **Test 4-7:** Require actual PDF files for testing

## Notes
- All validation and error handling tests passed successfully
- Image extraction functionality requires actual PDF files with embedded images
- Tool is properly integrated into the MCP system
- Both Run.csproj and mcp.csproj build successfully with required dependencies
