using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using KmcAiBlazorApp.Models;
using KmcAiBlazorApp.Services;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.Circuits;
using ProcessingStatus = KmcAiBlazorApp.Models.ProcessingStatus;
using ProcessingStep = KmcAiBlazorApp.Models.ProcessingStep;

namespace KmcAiBlazorApp.Components;

public partial class DocumentUpload : ComponentBase, IDisposable
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;
    
    [Inject]
    private IHttpClientFactory HttpFactory { get; set; } = default!;
    
    [Inject]
    private SignalRNotificationService SignalRService { get; set; } = default!;
    
    private InputFile? fileInput;
    private DotNetObjectReference<DocumentUpload>? objRef;
    private bool isProcessing = false;
    
    [Parameter]
    public List<UploadedFile> UploadedFiles { get; set; } = new();
    
    [Parameter]
    public EventCallback<List<UploadedFile>> UploadedFilesChanged { get; set; }
    
    string? PortfolioId = "test-portfolio-123";
    string Status = "Disconnected";
    List<string> Lines = new();
    
    // ProcessingModal reference
    private ProcessingModal? processingModal;
    private bool showProcessingModal = false;

    async Task ConnectAsync()
    {
        try
        {
            Console.WriteLine("Starting SignalR connection...");
            
            // Subscribe to SignalR events
            SignalRService.OnProcessingUpdate += HandleProcessingUpdate;
            SignalRService.OnDocumentValidationUpdate += HandleDocumentValidationUpdate;
            SignalRService.OnPortfolioCompletionUpdate += HandlePortfolioCompletionUpdate;
            SignalRService.OnCompanyHouseValidationUpdate += HandleCompanyHouseValidationUpdate;
            SignalRService.OnProcessingCompleteUpdate += HandleProcessingCompleteUpdate;
            SignalRService.OnConnectionStatusChanged += HandleConnectionStatusChanged;
            
            // Start the connection
            await SignalRService.StartConnectionAsync();
            
            Console.WriteLine("SignalR connection established successfully");
        }
        catch (Exception ex)
        {
            Status = "Failed to connect";
            Console.WriteLine($"SignalR connection failed: {ex.Message}");
            Add("Error", new { ex.Message });
        }
    }

    async Task DisconnectAsync()
    {
        try
        {
            // Unsubscribe from events
            SignalRService.OnProcessingUpdate -= HandleProcessingUpdate;
            SignalRService.OnDocumentValidationUpdate -= HandleDocumentValidationUpdate;
            SignalRService.OnPortfolioCompletionUpdate -= HandlePortfolioCompletionUpdate;
            SignalRService.OnCompanyHouseValidationUpdate -= HandleCompanyHouseValidationUpdate;
            SignalRService.OnProcessingCompleteUpdate -= HandleProcessingCompleteUpdate;
            SignalRService.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
            
            Status = "Disconnected";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting: {ex.Message}");
        }
    }

    async Task JoinGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(PortfolioId)) 
        {
            Console.WriteLine($"Cannot join group: PortfolioId={PortfolioId}");
            return;
        }
        
        try
        {
            Console.WriteLine($"Attempting to join group: {PortfolioId}");
            await SignalRService.JoinPortfolioGroupAsync(PortfolioId);
            Add("System", new { message = $"Joined group {PortfolioId}" });
            Console.WriteLine($"Successfully joined group: {PortfolioId}");
        }
        catch (Exception ex) 
        { 
            Console.WriteLine($"Join failed: {ex.Message}");
            Add("Error", new { message = $"Join failed: {ex.Message}" }); 
        }
    }

    async Task LeaveGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(PortfolioId)) return;
        try
        {
            await SignalRService.LeavePortfolioGroupAsync(PortfolioId);
            Add("System", new { message = $"Left group {PortfolioId}" });
        }
        catch (Exception ex) { Add("Error", new { message = $"Leave failed: {ex.Message}" }); }
    }

    void Add(string title, object payload)
    {
                    var json = System.Text.Json.JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            Lines.Add($"{title}\n{json}");
            _ = InvokeAsync(StateHasChanged);
    }

    // Event handlers for SignalR updates
    private void HandleProcessingUpdate(ProcessingUpdate update)
    {
        try
        {
            Console.WriteLine($"SignalR Processing Update Received: {JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true })}");
            
            // Update status with progress label
            Status = $"🔄 Processing: {update.Message} ({update.Progress}%)";
            
            // Update ProcessingModal based on the ProcessingStep
            if (processingModal != null)
            {
                Console.WriteLine($"Updating ProcessingModal - Progress: {update.Progress}%, Step: {update.ProcessingStep}, Status: {update.ProcessingStatus}");
                
                // Wrap all modal updates in InvokeAsync to ensure UI thread execution
                _ = InvokeAsync(async () =>
                {
                    processingModal.UpdateProgress(update.Progress);
                    
                                    // Map ProcessingStep to modal step descriptions
                var stepDescription = update.ProcessingStep switch
                {
                    KmcAiBlazorApp.Models.ProcessingStep.DocumentValidation => "Document Validation",
                    KmcAiBlazorApp.Models.ProcessingStep.PortfolioCompletion => "Portfolio Completion",
                    KmcAiBlazorApp.Models.ProcessingStep.CompanyHouseValidation => "Company House Validation",
                    KmcAiBlazorApp.Models.ProcessingStep.ProcessingComplete => "Final Processing & Completion",
                    _ => "Processing..."
                };
                // Handle the "Next Step" notification (when backend sends "Starting..." or "Finalizing...")
                if (update.Message.StartsWith("Starting") || update.Message.StartsWith("Finalizing"))
                {
                    // This is a "next step" notification - set current step to InProgress
                    foreach (var step in processingModal.ProcessingSteps)
                    {
                        if (step.Description == stepDescription)
                        {
                            step.Status = ProcessingStatus.InProgress;
                        }
                        else if (step.Status == ProcessingStatus.InProgress)
                        {
                            // Reset other in-progress steps back to Alert
                            step.Status = ProcessingStatus.Alert;
                        }
                    }
                    
                    // Mark previous steps as Success
                    var stepIndex = processingModal.ProcessingSteps.FindIndex(s => s.Description == stepDescription);
                    if (stepIndex > 0)
                    {
                        for (int i = 0; i < stepIndex; i++)
                        {
                            if (processingModal.ProcessingSteps[i].Status == ProcessingStatus.Alert)
                            {
                                processingModal.ProcessingSteps[i].Status = ProcessingStatus.Success;
                            }
                        }
                    }
                    
                    Console.WriteLine($"Next Step Notification - Step: {stepDescription}, Message: {update.Message}");
                }
                else
                {
                    // This is a completion notification - set current step to Success/Failure
                    var modalStatus = update.ProcessingStatus switch
                    {
                        ProcessingStatus.Success => ProcessingStatus.Success,
                        ProcessingStatus.Failure => ProcessingStatus.Failure,
                        ProcessingStatus.Alert => ProcessingStatus.Alert,
                        _ => ProcessingStatus.InProgress
                    };
                    
                    foreach (var step in processingModal.ProcessingSteps)
                    {
                        if (step.Description == stepDescription)
                        {
                            step.Status = modalStatus;
                        }
                    }
                    
                    Console.WriteLine($"Completion Notification - Step: {stepDescription}, Status: {modalStatus}, Message: {update.Message}");
                }
                
                Console.WriteLine($"Modal Update - Step: {stepDescription}, Message: {update.Message}");
                    
                    processingModal.UpdateValidationMessage(stepDescription, update.Message);
                    
                    StateHasChanged();
                });
            }
            else
            {
                Console.WriteLine("ProcessingModal is null - cannot update modal");
            }
            
            Add("ProcessingUpdate", update);
            _ = InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling processing update: {ex.Message}");
        }
    }

    private void HandleDocumentValidationUpdate(DocumentValidationUpdate update)
    {
        try
        {
            Console.WriteLine($"Document Validation Update: {JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true })}");
            
            // Update status with document validation progress
            var validCount = update.ValidDocuments;
            var invalidCount = update.InvalidDocuments;
            var totalCount = update.TotalDocuments;
            Status = $"📋 Document Validation: {validCount} valid, {invalidCount} invalid out of {totalCount} total ({update.Progress}%)";
            
            // Update ProcessingModal
            if (processingModal != null)
            {
                _ = InvokeAsync(async () =>
                {
                    processingModal.UpdateProgress(update.Progress);
                    processingModal.UpdateStepStatus("Document Validation", ProcessingStatus.Success);
                    var validationMessage = $"Valid: {validCount}, Invalid: {invalidCount}, Total: {totalCount}";
                    processingModal.UpdateValidationMessage("Document Validation", validationMessage);
                    StateHasChanged();
                });
            }
            
            Add("DocumentValidationUpdate", update);
            _ = InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling document validation update: {ex.Message}");
        }
    }

    private void HandlePortfolioCompletionUpdate(PortfolioCompletionUpdate update)
    {
        try
        {
            Console.WriteLine($"Portfolio Completion Update: {JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true })}");
            
            // Update status with portfolio completion progress
            var hasData = update.HasPortfolioData ? "Found" : "Not found";
            var companyName = !string.IsNullOrEmpty(update.CompanyName) ? update.CompanyName : "N/A";
            Status = $"📊 Portfolio Validation: {hasData} portfolio data, Company: {companyName}, Properties: {update.PropertyCount} ({update.Progress}%)";
            
            // Update ProcessingModal
            if (processingModal != null)
            {
                _ = InvokeAsync(async () =>
                {
                    processingModal.UpdateProgress(update.Progress);
                    processingModal.UpdateStepStatus("Portfolio Completion", ProcessingStatus.Success);
                    var validationMessage = $"Portfolio data: {hasData}, Company: {companyName}, Properties: {update.PropertyCount}";
                    processingModal.UpdateValidationMessage("Portfolio Completion", validationMessage);
                    StateHasChanged();
                });
            }
            
            Add("PortfolioCompletionUpdate", update);
            _ = InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling portfolio completion update: {ex.Message}");
        }
    }

    private void HandleCompanyHouseValidationUpdate(CompanyHouseValidationUpdate update)
    {
        try
        {
            Console.WriteLine($"Company House Validation Update: {JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true })}");
            
            // Update status with company house validation progress
            var hasData = update.HasCompanyData ? "Found" : "Not found";
            var companyNumber = !string.IsNullOrEmpty(update.CompanyNumber) ? update.CompanyNumber : "N/A";
            Status = $"🏢 Company House Validation: {hasData} company data, Company Number: {companyNumber}, Charges: {update.ChargeCount} ({update.Progress}%)";
            
            // Update ProcessingModal
            if (processingModal != null)
            {
                _ = InvokeAsync(async () =>
                {
                    processingModal.UpdateProgress(update.Progress);
                    processingModal.UpdateStepStatus("Company House Validation", ProcessingStatus.Success);
                    var validationMessage = $"Company data: {hasData}, Company Number: {companyNumber}, Charges: {update.ChargeCount}";
                    processingModal.UpdateValidationMessage("Company House Validation", validationMessage);
                    StateHasChanged();
                });
            }
            
            Add("CompanyHouseValidationUpdate", update);
            _ = InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling company house validation update: {ex.Message}");
        }
    }

    private void HandleProcessingCompleteUpdate(ProcessingCompleteUpdate update)
    {
        try
        {
            Console.WriteLine($"Processing Complete Update: {JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true })}");
            
            // Update status with processing completion
            var successStatus = update.Success ? "✅ Successfully completed" : "❌ Failed";
            var processingTime = !string.IsNullOrEmpty(update.ProcessingTime) ? update.ProcessingTime : "N/A";
            Status = $"🎯 Processing Complete: {successStatus} in {processingTime} ({update.Progress}%)";
            
            // Update ProcessingModal
            if (processingModal != null)
            {
                _ = InvokeAsync(async () =>
                {
                    processingModal.UpdateProgress(update.Progress);
                    
                    // Mark all steps as Success when processing is complete
                    foreach (var step in processingModal.ProcessingSteps)
                    {
                        if (step.Status == ProcessingStatus.InProgress || step.Status == ProcessingStatus.Alert)
                        {
                            step.Status = ProcessingStatus.Success;
                        }
                    }
                    
                    // Update the final step with completion message
                    processingModal.UpdateStepStatus("Final Processing & Completion", ProcessingStatus.Success);
                    var validationMessage = $"Processing {successStatus} in {processingTime}";
                    processingModal.UpdateValidationMessage("Final Processing & Completion", validationMessage);
                    
                    // Mark processing as complete
                    processingModal.IsProcessingComplete = true;
                    
                    StateHasChanged();
                    
                    // Keep modal open - don't close automatically
                    // User can close manually or click "See Results" button
                });
            }
            
            Add("ProcessingCompleteUpdate", update);
            _ = InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling processing complete update: {ex.Message}");
        }
    }

    private void HandleConnectionStatusChanged(string status)
    {
        try
        {
            // Add emoji and better formatting for connection status
            var statusWithEmoji = status switch
            {
                "Connected" => "🔗 Connected to SignalR",
                "Reconnecting" => "🔄 Reconnecting to SignalR...",
                "Reconnected" => "✅ Reconnected to SignalR",
                "Disconnected" => "❌ Disconnected from SignalR",
                "Failed to connect" => "❌ Failed to connect to SignalR",
                _ => $"📡 {status}"
            };
            
            Status = statusWithEmoji;
            Console.WriteLine($"Connection status changed: {status}");
            _ = InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling connection status change: {ex.Message}");
        }
    }

    // Method to update portfolio ID and join SignalR group
    private async Task UpdatePortfolioIdAndJoinGroup(string portfolioId)
    {
        try
        {
            PortfolioId = portfolioId;
            Console.WriteLine($"Updated Portfolio ID: {PortfolioId}");
            
            // Join the new group
            await JoinGroupAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating portfolio ID: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
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
        MultipartFormDataContent? formData = null;
        try
        {
            if (!UploadedFiles.Any())
            {
                Console.WriteLine("No files to process");
                await ShowErrorSafe("No files to process. Please upload files and try again.");
                return;
            }

            isProcessing = true;
            showProcessingModal = true;
            Console.WriteLine($"Modal visibility set to: {showProcessingModal}");
            _ = InvokeAsync(StateHasChanged);

            // Disconnect any existing SignalR connection
            await DisconnectAsync();
            Console.WriteLine("Disconnected from previous SignalR connection");

            // Generate new portfolio ID for each processing session
            PortfolioId = Guid.NewGuid().ToString();
            Console.WriteLine($"Generated new portfolio ID: {PortfolioId}");

            // Clear previous SignalR messages for fresh start
            Lines.Clear();
            Status = "Disconnected";
            _ = InvokeAsync(StateHasChanged);

            Console.WriteLine($"Starting processing for {UploadedFiles.Count} files...");
            
            // Debug: Log all uploaded files
            foreach (var file in UploadedFiles)
            {
                Console.WriteLine($"File: {file.Name}, Size: {file.Size}, ContentType: {file.ContentType}, HasContent: {file.File != null}");
            }

            // Create FormData to send files
            formData = new MultipartFormDataContent();
            var processedFiles = 0;
            var streamContents = new List<StreamContent>(); // Keep reference to dispose later
            
            // Add portfolio ID to form data (same ID used for SignalR)
            formData.Add(new StringContent(PortfolioId), "portfolioId");
            Console.WriteLine($"Added portfolio ID to form data: {PortfolioId}");
            
            foreach (var uploadedFile in UploadedFiles)
            {
                if (uploadedFile.File != null)
                {
                    try
                    {
                        Console.WriteLine($"Processing file: {uploadedFile.Name}, Size: {uploadedFile.Size}");
                        
                        // Validate file size (100MB limit)
                        if (uploadedFile.Size > 100 * 1024 * 1024)
                        {
                            Console.WriteLine($"Warning: File {uploadedFile.Name} exceeds 100MB limit ({uploadedFile.Size} bytes)");
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
                        
                        // Read the file content into a byte array first to avoid stream issues
                        Console.WriteLine($"Reading file content for: {uploadedFile.Name}");
                        var stream = uploadedFile.File.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB limit
                        
                        // Read stream content into memory
                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        var fileBytes = memoryStream.ToArray();
                        
                        Console.WriteLine($"File read into memory: {uploadedFile.Name}, Bytes: {fileBytes.Length}");
                        
                        // Create ByteArrayContent from the byte array
                        var byteArrayContent = new ByteArrayContent(fileBytes);
                        
                        // Set the content type
                        if (!string.IsNullOrEmpty(uploadedFile.ContentType))
                        {
                            byteArrayContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(uploadedFile.ContentType);
                        }
                        
                        // Add the file to form data with the correct parameter name
                        // Use "files" as the field name for each file - ASP.NET Core will bind this to List<IFormFile> files
                        formData.Add(byteArrayContent, "files", uploadedFile.Name);
                        Console.WriteLine($"File added to form data: {uploadedFile.Name}");
                        
                        processedFiles++;
                        Console.WriteLine($"Added file: {uploadedFile.Name} ({fileBytes.Length} bytes) - ContentType: {uploadedFile.ContentType}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {uploadedFile.Name}: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: File {uploadedFile.Name} has no content");
                }
            }
            
            // Check if we have any files to send
            if (processedFiles == 0)
            {
                Console.WriteLine("No valid files to process");
                await ShowErrorSafe("No valid files to process. Please upload files and try again.");
                return;
            }

            Console.WriteLine($"Form data ready with {processedFiles} files");
            
            // Debug: Log form data information
            Console.WriteLine("=== FORM DATA DETAILS ===");
            Console.WriteLine($"Total form data parts: {formData.Count()}");
            Console.WriteLine($"Portfolio ID: {PortfolioId}");
            var partIndex = 0;
            foreach (var content in formData)
            {
                Console.WriteLine($"Part {partIndex++}:");
                Console.WriteLine($"  Content Type: {content.Headers.ContentType}");
                Console.WriteLine($"  Content-Disposition: {content.Headers.ContentDisposition}");
                Console.WriteLine($"  Form Field Name: {content.Headers.ContentDisposition?.Name?.Trim('"')}");
                Console.WriteLine($"  Filename: {content.Headers.ContentDisposition?.FileName?.Trim('"')}");
                Console.WriteLine($"  Content Length: {content.Headers.ContentLength}");
                Console.WriteLine("  ---");
            }
            Console.WriteLine("=== END FORM DATA DETAILS ===");

            // Connect to SignalR and join portfolio group
            await ConnectAsync();
            await JoinGroupAsync();

            // Call the backend API
            using var httpClient = HttpFactory.CreateClient("BackendAPI");
            Console.WriteLine($"Sending POST request to: /api/DocumentUpload/upload2");
            Console.WriteLine($"Request URI: {httpClient.BaseAddress}api/DocumentUpload/upload2");
            
            // Set a longer timeout for file uploads
            httpClient.Timeout = TimeSpan.FromMinutes(30);
            
            Console.WriteLine($"Starting API call with 30-minute timeout...");
            Console.WriteLine($"Complete URL: {httpClient.BaseAddress}api/DocumentUpload/upload2");
            Console.WriteLine($"Portfolio ID being sent: {PortfolioId}");
            Console.WriteLine($"Form data parts: {formData.Count()}");
            
            // Make the API call
            var response = await httpClient.PostAsync("/api/DocumentUpload/upload2", formData);
            
            Console.WriteLine($"API Response Status: {response.StatusCode}");
            Console.WriteLine($"API Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response content length: {responseContent.Length}");
                Console.WriteLine($"Response content (first 1000 chars): {responseContent.Substring(0, Math.Min(1000, responseContent.Length))}");
                
                try
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    Console.WriteLine("API call successful!");
                    
                    // Extract basic response information
                    if (result.TryGetProperty("PortfolioId", out var portfolioIdElement))
                    {
                        var portfolioId = portfolioIdElement.GetString();
                        Console.WriteLine($"Portfolio ID: {portfolioId}");
                        
                        // Update portfolio ID and join new group if needed
                        if (!string.IsNullOrEmpty(portfolioId) && portfolioId != PortfolioId)
                        {
                            await UpdatePortfolioIdAndJoinGroup(portfolioId);
                        }
                    }
                    
                    // Connect to SignalR and join the new portfolio group
                    await ConnectAsync();
                    await JoinGroupAsync();
                    Console.WriteLine($"Connected to SignalR and joined group: {PortfolioId}");
                    
                    if (result.TryGetProperty("Status", out var statusElement))
                    {
                        var status = statusElement.GetString();
                        Console.WriteLine($"Status: {status}");
                    }
                    
                    // Success - no alert needed, modal will show progress
                    Console.WriteLine("Files uploaded successfully! Processing has started.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing response: {ex.Message}");
                    await ShowErrorSafe($"Error parsing response: {ex.Message}");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API call failed: {response.StatusCode}");
                Console.WriteLine($"Response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                Console.WriteLine($"Error content: {errorContent}");
                
                // Build error message for user
                var errorMessage = $"❌ PROCESSING FAILED\n\n";
                errorMessage += $"Status Code: {response.StatusCode}\n";
                errorMessage += $"Error Details: {errorContent}\n\n";
                errorMessage += "Please check your files and try again.";
                
                // Show error notification to user
                await ShowErrorSafe(errorMessage);
            }
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"API call timed out: {ex.Message}");
            await ShowErrorSafe("API call timed out. The request took too long to complete. Please try with fewer files or check your API server.");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP request error: {ex.Message}");
            await ShowErrorSafe($"HTTP request error: {ex.Message}. Please check if your API server is running.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StartProcessing: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Show error notification to user
            await ShowErrorSafe($"Error: {ex.Message}");
        }
        finally
        {
            // Clean up form data
            formData?.Dispose();
            
            isProcessing = false;
            
            // Ensure progress bar reaches 100% and mark all steps as success
            if (processingModal != null)
            {
                _ = InvokeAsync(async () =>
                {
                    // Set progress to 100% if not already there
                    processingModal.UpdateProgress(100);
                    
                    // Mark all remaining steps as Success
                    foreach (var step in processingModal.ProcessingSteps)
                    {
                        if (step.Status == ProcessingStatus.InProgress || step.Status == ProcessingStatus.Alert)
                        {
                            step.Status = ProcessingStatus.Success;
                        }
                    }
                    
                    // Mark processing as complete
                    processingModal.IsProcessingComplete = true;
                    
                    StateHasChanged();
                    
                    // Keep modal open - don't close automatically
                    // User can close manually or click "See Results" button
                });
            }
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

    private async Task ShowErrorSafe(string message)
    {
        try
        {
            // Check if the circuit is still connected before calling JS interop
            if (JSRuntime is not null)
            {
                await JSRuntime.InvokeVoidAsync("alert", message);
            }
            else
            {
                // Fallback: log to console if JS interop is not available
                Console.WriteLine($"Error (JS interop not available): {message}");
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit has disconnected, just log to console
            Console.WriteLine($"Error (circuit disconnected): {message}");
        }
        catch (Exception ex)
        {
            // Any other error, log to console
            Console.WriteLine($"Error showing message: {ex.Message}");
            Console.WriteLine($"Original message: {message}");
        }
    }
    
    // Modal event handlers
    private async Task HandleCancelProcessing()
    {
        showProcessingModal = false;
        isProcessing = false;
        await DisconnectAsync();
        StateHasChanged();
        await Task.CompletedTask;
    }
    
    private async Task HandleViewResults()
    {
        showProcessingModal = false;
        StateHasChanged();
        await Task.CompletedTask;
    }

}