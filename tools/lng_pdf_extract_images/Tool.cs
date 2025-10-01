using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace McpDotnet.Tools.LngPdfExtractImages;

/// <summary>
/// PDF Image Extraction tool - .NET version of lng_pdf_extract_images
/// </summary>
[McpServerToolType]
public static class Tool
{
    private static readonly ILogger _logger = LoggingConfig.SetupLogging("lng_pdf_extract_images");

    /// <summary>
    /// Tool name for registration
    /// </summary>
    public static string ToolName => "lng_pdf_extract_images";

    /// <summary>
    /// Initialize the PDF image extraction tool
    /// </summary>
    public static void Init()
    {
        _logger.LogInformation("Initializing lng_pdf_extract_images tool");
    }

    /// <summary>
    /// Extracts images from PDF files
    /// </summary>
    /// <param name="pdf_path">Path to the PDF file to process</param>
    /// <param name="output_dir">Optional: Custom output directory for extracted images</param>
    /// <param name="image_format">Optional: Output format - "png" (default) or "jpeg"</param>
    /// <returns>Extraction result in JSON format with comprehensive metadata</returns>
    [McpServerTool]
    [Description(@"Extracts images from PDF files with support for PNG and JPEG formats.

**Parameters:**
- `pdf_path` (string, required): Path to the PDF file to process.
- `output_dir` (string, optional): Custom output directory for extracted images. Defaults to same directory as PDF.
- `image_format` (string, optional): Output format - ""png"" (default) or ""jpeg"".

**Returns:**
- Image dimensions and properties
- Page numbers where images were found
- Total count of images extracted
- List of extracted image file paths
- Processing summary and metadata

**Error Handling:**
- Password-protected PDFs
- PDFs with no images
- Corrupted or unreadable PDFs
- File not found errors
- Permission errors

**Usage examples:**
- Extract all images as PNG: {""pdf_path"": ""document.pdf""}
- Extract as JPEG: {""pdf_path"": ""document.pdf"", ""image_format"": ""jpeg""}
- Custom output directory: {""pdf_path"": ""document.pdf"", ""output_dir"": ""./images""}")]
    public static async Task<string> lng_pdf_extract_images(
        [Description("Path to the PDF file to process")] 
        string pdf_path,
        [Description("Optional: Custom output directory for extracted images")] 
        string? output_dir = null,
        [Description("Optional: Output format - 'png' (default) or 'jpeg'")] 
        string image_format = "png")
    {
        try
        {
            _logger.LogInformation("Extracting images from PDF: {PdfPath}", pdf_path);

            // Validate required parameters
            if (string.IsNullOrWhiteSpace(pdf_path))
            {
                var errorResult = new
                {
                    success = false,
                    error = "pdf_path parameter is required"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Validate image format
            var validFormats = new[] { "png", "jpeg", "jpg" };
            var format = image_format.ToLower();
            if (!validFormats.Contains(format))
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Invalid image_format '{image_format}'. Must be 'png' or 'jpeg'"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Normalize format (jpg -> jpeg)
            if (format == "jpg") format = "jpeg";

            // Validate PDF file
            var pdfPathExpanded = Path.IsPathRooted(pdf_path) ? pdf_path : Path.GetFullPath(pdf_path);

            if (!File.Exists(pdfPathExpanded))
            {
                var errorResult = new
                {
                    success = false,
                    error = $"PDF file not found: {pdfPathExpanded}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Determine output directory
            string outputDirectory;
            if (!string.IsNullOrWhiteSpace(output_dir))
            {
                outputDirectory = Path.IsPathRooted(output_dir) ? output_dir : Path.GetFullPath(output_dir);
            }
            else
            {
                // Default: same directory as PDF
                outputDirectory = Path.GetDirectoryName(pdfPathExpanded) ?? Directory.GetCurrentDirectory();
            }

            // Create output directory if it doesn't exist
            try
            {
                Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception ex)
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Failed to create output directory: {ex.Message}"
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }

            // Extract images from PDF
            var extractedImages = new List<object>();
            var imageCount = 0;

            try
            {
                using (var document = PdfDocument.Open(pdfPathExpanded))
                {
                    _logger.LogInformation("PDF opened successfully. Total pages: {PageCount}", document.NumberOfPages);

                    // Check if PDF is password-protected (already opened, so this won't happen, but keep for completeness)
                    // UglyToad.PdfPig will throw an exception if password is required

                    for (int pageNum = 1; pageNum <= document.NumberOfPages; pageNum++)
                    {
                        var page = document.GetPage(pageNum);
                        
                        // Get images from the page
                        var images = page.GetImages();

                        foreach (var image in images)
                        {
                            try
                            {
                                imageCount++;
                                var imageFileName = $"image_{imageCount:D3}.{format}";
                                var imageFilePath = Path.Combine(outputDirectory, imageFileName);

                                // Extract raw image bytes
                                var imageBytes = image.RawBytes.ToArray();

                                // Try to get image properties
                                int width = 0;
                                int height = 0;
                                
                                try
                                {
                                    // Try to decode the image to get dimensions
                                    if (image.TryGetPng(out var pngBytes))
                                    {
                                        using (var ms = new MemoryStream(pngBytes))
                                        using (var img = Image.Load(ms))
                                        {
                                            width = img.Width;
                                            height = img.Height;

                                            // Save in requested format
                                            if (format == "png")
                                            {
                                                await img.SaveAsPngAsync(imageFilePath);
                                            }
                                            else // jpeg
                                            {
                                                await img.SaveAsJpegAsync(imageFilePath);
                                            }
                                        }
                                    }
                                    else if (image.TryGetBytes(out var rawBytes))
                                    {
                                        // Try to load as generic image
                                        try
                                        {
                                            using (var ms = new MemoryStream(rawBytes.ToArray()))
                                            using (var img = Image.Load(ms))
                                            {
                                                width = img.Width;
                                                height = img.Height;

                                                // Save in requested format
                                                if (format == "png")
                                                {
                                                    await img.SaveAsPngAsync(imageFilePath);
                                                }
                                                else // jpeg
                                                {
                                                    await img.SaveAsJpegAsync(imageFilePath);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // If we can't decode, try to get bounds from PDF
                                            var bounds = image.Bounds;
                                            width = (int)bounds.Width;
                                            height = (int)bounds.Height;

                                            // Save raw bytes
                                            await File.WriteAllBytesAsync(imageFilePath, rawBytes.ToArray());
                                        }
                                    }
                                    else
                                    {
                                        // Fallback: use bounds from PDF
                                        var bounds = image.Bounds;
                                        width = (int)bounds.Width;
                                        height = (int)bounds.Height;

                                        // Save raw bytes
                                        await File.WriteAllBytesAsync(imageFilePath, imageBytes);
                                    }
                                }
                                catch (Exception imgEx)
                                {
                                    _logger.LogWarning("Failed to decode image {ImageNum}, saving raw data: {Error}", imageCount, imgEx.Message);
                                    
                                    // Last resort: save raw bytes
                                    await File.WriteAllBytesAsync(imageFilePath, imageBytes);
                                    
                                    var bounds = image.Bounds;
                                    width = (int)bounds.Width;
                                    height = (int)bounds.Height;
                                }

                                var imageInfo = new
                                {
                                    file_path = imageFilePath,
                                    file_name = imageFileName,
                                    page_number = pageNum,
                                    image_number = imageCount,
                                    width = width,
                                    height = height,
                                    format = format
                                };

                                extractedImages.Add(imageInfo);
                                _logger.LogInformation("Extracted image {ImageNum} from page {PageNum}: {FileName}", imageCount, pageNum, imageFileName);
                            }
                            catch (Exception imgEx)
                            {
                                _logger.LogError(imgEx, "Error extracting image {ImageNum} from page {PageNum}", imageCount, pageNum);
                                // Continue with next image
                            }
                        }
                    }
                }

                // Check if any images were found
                if (imageCount == 0)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = "No images found in the PDF file",
                        pdf_path = pdfPathExpanded,
                        total_pages = 0
                    };
                    return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
                }

                // Return success result with comprehensive metadata
                var result = new
                {
                    success = true,
                    pdf_path = pdfPathExpanded,
                    output_directory = outputDirectory,
                    image_format = format,
                    total_images = imageCount,
                    images = extractedImages,
                    summary = new
                    {
                        pdf_file = Path.GetFileName(pdfPathExpanded),
                        images_extracted = imageCount,
                        output_location = outputDirectory,
                        format_used = format
                    }
                };

                _logger.LogInformation("Successfully extracted {ImageCount} images from PDF", imageCount);
                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            }
            catch (Exception pdfEx) when (pdfEx.Message.Contains("password") || pdfEx.Message.Contains("encrypted"))
            {
                var errorResult = new
                {
                    success = false,
                    error = "PDF file is password-protected or encrypted. Cannot extract images.",
                    pdf_path = pdfPathExpanded
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception pdfEx) when (pdfEx is IOException || pdfEx is UnauthorizedAccessException)
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Permission error or file access issue: {pdfEx.Message}",
                    pdf_path = pdfPathExpanded
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception pdfEx)
            {
                _logger.LogError(pdfEx, "Error reading PDF file: {PdfPath}", pdfPathExpanded);
                var errorResult = new
                {
                    success = false,
                    error = $"Corrupted or unreadable PDF file: {pdfEx.Message}",
                    pdf_path = pdfPathExpanded
                };
                return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting images from PDF '{PdfPath}': {Error}", pdf_path, ex.Message);
            var errorResult = new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}"
            };
            return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
