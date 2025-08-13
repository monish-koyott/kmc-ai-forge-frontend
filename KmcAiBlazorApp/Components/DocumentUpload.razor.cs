using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using KmcAiBlazorApp.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace KmcAiBlazorApp.Components;

public partial class DocumentUpload : ComponentBase, IDisposable
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;
    
    [Inject]
    private IHttpClientFactory HttpFactory { get; set; } = default!;
    
    private InputFile? fileInput;
    private DotNetObjectReference<DocumentUpload>? objRef;
    private bool isProcessing = false;
    
    [Parameter]
    public List<UploadedFile> UploadedFiles { get; set; } = new();
    
    [Parameter]
    public EventCallback<List<UploadedFile>> UploadedFilesChanged { get; set; }
    
    private void HandleDragEnter()
    {
        // Visual feedback handled by CSS
    }
    
    private void HandleDragLeave()
    {
        // Visual feedback handled by CSS
    }
    
    private async Task HandleDrop()
    {
        // Will be implemented with JavaScript interop
    }
    
    private async Task HandleClick()
    {
        try
        {
            // Trigger the file input
            await TriggerFileInput();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleClick: {ex.Message}");
        }
    }
    
    private async Task TriggerFileInput()
    {
        try
        {
            if (fileInput != null)
            {
                // Use JavaScript to trigger the file input click
                await JSRuntime.InvokeVoidAsync("triggerFileInput");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error triggering file input: {ex.Message}");
        }
    }
    
    private async Task HandleFileSelection(InputFileChangeEventArgs e)
    {
        try
        {
            var newFiles = new List<UploadedFile>();
            
            foreach (var file in e.GetMultipleFiles())
            {
                var uploadedFile = new UploadedFile
                {
                    Name = file.Name,
                    Size = file.Size,
                    ContentType = file.ContentType,
                    File = file
                };
                newFiles.Add(uploadedFile);
            }
            
            // Create a new list with existing files + new files
            var updatedFiles = new List<UploadedFile>(UploadedFiles);
            updatedFiles.AddRange(newFiles);
            
            // Notify parent component of the change with the updated list
            await UploadedFilesChanged.InvokeAsync(updatedFiles);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleFileSelection: {ex.Message}");
        }
    }
    
    private async Task StartProcessing()
    {
        try
        {
            if (!UploadedFiles.Any())
            {
                Console.WriteLine("No files to process");
                return;
            }

            isProcessing = true;
            StateHasChanged();

            Console.WriteLine($"Starting processing for {UploadedFiles.Count} files...");
            
            // Debug: Log all uploaded files
            foreach (var file in UploadedFiles)
            {
                Console.WriteLine($"File: {file.Name}, Size: {file.Size}, ContentType: {file.ContentType}, HasContent: {file.File != null}");
            }

            // Create FormData to send files
            using var formData = new MultipartFormDataContent();
            var processedFiles = 0;
            
            foreach (var uploadedFile in UploadedFiles)
            {
                if (uploadedFile.File != null)
                {
                    try
                    {
                        // Validate file object
                        if (uploadedFile.File == null)
                        {
                            Console.WriteLine($"Warning: File {uploadedFile.Name} has null File object");
                            continue;
                        }
                        
                        // Validate file size (50MB limit as per your backend)
                        if (uploadedFile.Size > 50 * 1024 * 1024)
                        {
                            Console.WriteLine($"Warning: File {uploadedFile.Name} exceeds 50MB limit ({uploadedFile.Size} bytes)");
                            continue;
                        }
                        
                        // Validate file has content
                        if (uploadedFile.Size == 0)
                        {
                            Console.WriteLine($"Warning: File {uploadedFile.Name} has no content (0 bytes)");
                            continue;
                        }
                        
                        // Validate file name
                        if (string.IsNullOrEmpty(uploadedFile.Name))
                        {
                            Console.WriteLine($"Warning: File has no name");
                            continue;
                        }
                        
                        // Read the file content - IBrowserFile.OpenReadStream() can only be called once
                        Console.WriteLine($"Opening stream for file: {uploadedFile.Name}");
                        var stream = uploadedFile.File.OpenReadStream();
                        Console.WriteLine($"Stream opened successfully for: {uploadedFile.Name}");
                        
                        // Create StreamContent directly from the stream
                        var streamContent = new StreamContent(stream);
                        Console.WriteLine($"StreamContent created for: {uploadedFile.Name}");
                        
                        // Set the content type
                        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(uploadedFile.ContentType);
                        Console.WriteLine($"Content type set for: {uploadedFile.Name}");
                        
                        // Add the file to form data with the correct parameter name
                        // The parameter name should match your backend: [FromForm] List<IFormFile> files
                        // For IFormFile, the first parameter is the form field name, second is the filename
                        formData.Add(streamContent, "files", uploadedFile.Name);
                        Console.WriteLine($"File added to form data: {uploadedFile.Name}");
                        
                        processedFiles++;
                        Console.WriteLine($"Added file: {uploadedFile.Name} ({uploadedFile.Size} bytes) - ContentType: {uploadedFile.ContentType}");
                        Console.WriteLine($"Form field name: files, Filename: {uploadedFile.Name}");
                        Console.WriteLine($"Stream length: {uploadedFile.Size} bytes");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {uploadedFile.Name}: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        
                        // Try to get more details about the error
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: File {uploadedFile.Name} has no content (drag & drop file)");
                }
            }
            
            // Check if we have any files to send
            if (processedFiles == 0)
            {
                Console.WriteLine("No valid files to process");
                await JSRuntime.InvokeVoidAsync("alert", "No valid files to process. Please upload files and try again.");
                return;
            }

            // Debug: Log the form data content
            Console.WriteLine($"Form data contains {formData.Count()} parts");
            
            foreach (var content in formData)
            {
                Console.WriteLine($"Content: {content.Headers.ContentType}");
                Console.WriteLine($"Content-Disposition: {content.Headers.ContentDisposition}");
                Console.WriteLine($"Form Field Name: {content.Headers.ContentDisposition?.Name}");
                Console.WriteLine($"Filename: {content.Headers.ContentDisposition?.FileName}");
                Console.WriteLine($"Content Length: {content.Headers.ContentLength}");
            }

            // Call the backend API
            using var httpClient = HttpFactory.CreateClient("BackendAPI");
            Console.WriteLine($"Sending POST request to: /api/documentupload/upload2");
            
            // Log the request details
            Console.WriteLine($"Request URI: {httpClient.BaseAddress}api/documentupload/upload2");
            Console.WriteLine($"Form data parts count: {formData.Count()}");
            
            var response = await httpClient.PostAsync("/api/documentupload/upload2", formData);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response content: {responseContent}");
                
                try
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    Console.WriteLine("API call successful!");
                    
                    // Extract validation results
                    var portfolioId = result.GetProperty("PortfolioId").GetString();
                    var status = result.GetProperty("Status").GetString();
                    
                    // Get summary information
                    var summary = result.GetProperty("Summary");
                    var totalDocuments = summary.GetProperty("TotalDocuments").GetInt32();
                    var validDocuments = summary.GetProperty("ValidDocuments").GetInt32();
                    var invalidDocuments = summary.GetProperty("InvalidDocuments").GetInt32();
                    
                    // Build validation message
                    var validationMessage = $"Portfolio ID: {portfolioId}\n";
                    validationMessage += $"Status: {status}\n\n";
                    validationMessage += $"Total Documents: {totalDocuments}\n";
                    validationMessage += $"Valid Documents: {validDocuments}\n";
                    validationMessage += $"Invalid Documents: {invalidDocuments}\n\n";
                    
                    // Add valid documents details
                    if (validDocuments > 0)
                    {
                        validationMessage += "✅ VALID DOCUMENTS:\n";
                        var validDocs = result.GetProperty("UploadedDocuments");
                        foreach (var doc in validDocs.EnumerateArray())
                        {
                            var fileName = doc.GetProperty("FileName").GetString();
                            var documentType = doc.GetProperty("DocumentType").GetString();
                            validationMessage += $"• {fileName} ({documentType})\n";
                        }
                        validationMessage += "\n";
                    }
                    
                    // Add invalid documents details
                    if (invalidDocuments > 0)
                    {
                        validationMessage += "❌ INVALID DOCUMENTS:\n";
                        var invalidDocs = result.GetProperty("InvalidDocuments");
                        foreach (var doc in invalidDocs.EnumerateArray())
                        {
                            var fileName = doc.GetProperty("FileName").GetString();
                            var reason = doc.GetProperty("Reason").GetString();
                            var identifiedType = doc.TryGetProperty("IdentifiedType", out var idType) ? idType.GetString() : "Unknown";
                            validationMessage += $"• {fileName} ({identifiedType}) - {reason}\n";
                        }
                    }
                    
                    // Show validation results in alert
                    await JSRuntime.InvokeVoidAsync("alert", validationMessage);
                    
                    Console.WriteLine($"Portfolio ID: {portfolioId}");
                    Console.WriteLine($"Status: {status}");
                    Console.WriteLine($"Valid: {validDocuments}, Invalid: {invalidDocuments}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing response: {ex.Message}");
                    await JSRuntime.InvokeVoidAsync("alert", $"Error parsing response: {ex.Message}");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API call failed: {response.StatusCode}");
                Console.WriteLine($"Response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                Console.WriteLine($"Error: {errorContent}");
                
                // Build error message for user
                var errorMessage = $"❌ PROCESSING FAILED\n\n";
                errorMessage += $"Status Code: {response.StatusCode}\n";
                errorMessage += $"Error Details: {errorContent}\n\n";
                errorMessage += "Please check your files and try again.";
                
                // Show error notification to user
                await JSRuntime.InvokeVoidAsync("alert", errorMessage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StartProcessing: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Show error notification to user
            await JSRuntime.InvokeVoidAsync("alert", $"Error: {ex.Message}");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            objRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("setupDragAndDrop", "drop-zone", objRef);
        }
    }

    public void Dispose()
    {
        objRef?.Dispose();
    }
}
