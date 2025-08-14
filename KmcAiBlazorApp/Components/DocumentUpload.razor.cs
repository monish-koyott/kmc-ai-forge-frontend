using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using KmcAiBlazorApp.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

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
    
    HubConnection? _conn;
    string? PortfolioId = "test-portfolio-123";
    bool IsConnected;
    string Status = "Disconnected";
    List<string> Lines = new();

    string HubUrl => "http://localhost:5001/documentProcessingHub";

    async Task ConnectAsync()
    {
        if (IsConnected) return;

        _conn = new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                // Force JSON protocol instead of binary
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                options.SkipNegotiation = false;
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                // Force JSON protocol and disable blazorpack
                options.PayloadSerializerOptions.PropertyNamingPolicy = null;
                options.PayloadSerializerOptions.WriteIndented = true;
            })
            .Build();
            


        // Wire up server-to-client events (match your backend names)
        _conn.On<object>("ProcessingUpdate", payload => HandleProcessingUpdate(payload));
        _conn.On<object>("ReceiveDocumentValidationUpdate", payload => Add("DocumentValidationUpdate", payload));
        _conn.On<object>("ReceivePortfolioCompletionUpdate", payload => Add("PortfolioCompletionUpdate", payload));
        _conn.On<object>("ReceiveCompanyHouseValidationUpdate", payload => Add("CompanyHouseValidationUpdate", payload));
        _conn.On<object>("ReceiveProcessingCompleteUpdate", payload => Add("ProcessingCompleteUpdate", payload));
        _conn.On<object>("JoinedGroup", payload => Add("JoinedGroup", payload));
        _conn.On<object>("Connected", payload => Add("Connected", payload));

        _conn.Reconnecting += _ => { Status = "Reconnecting…"; StateHasChanged(); return Task.CompletedTask; };
        _conn.Reconnected  += _ => { Status = "Reconnected"; StateHasChanged(); return Task.CompletedTask; };
        _conn.Closed       += _ => { IsConnected = false; Status = "Disconnected"; StateHasChanged(); return Task.CompletedTask; };

        try
        {
            Console.WriteLine("Starting SignalR connection...");
            Console.WriteLine($"Hub URL: {HubUrl}");
            Console.WriteLine($"Connection configuration: Transports=WebSockets, SkipNegotiation=false");
            
            await _conn.StartAsync();
            
            // Wait a moment to ensure connection is fully established
            await Task.Delay(100);
            
            // Double-check connection state
            if (_conn.State == HubConnectionState.Connected)
            {
                IsConnected = true;
                Status = "Connected";
                Add("System", new { message = "Connected to hub" });
                Console.WriteLine("SignalR connection established successfully");
                Console.WriteLine($"Connection ID: {_conn.ConnectionId}");
            }
            else
            {
                throw new Exception($"Connection failed. State: {_conn.State}");
            }
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
        if (_conn is null) return;
        try { await _conn.StopAsync(); } catch {}
        await _conn.DisposeAsync();
        _conn = null;
        IsConnected = false;
        Status = "Disconnected";
    }

    async Task JoinGroupAsync()
    {
        if (!IsConnected || _conn is null || string.IsNullOrWhiteSpace(PortfolioId)) 
        {
            Console.WriteLine($"Cannot join group: IsConnected={IsConnected}, Connection={_conn?.State}, PortfolioId={PortfolioId}");
            return;
        }
        
        // Check if connection is in the right state
        if (_conn.State != HubConnectionState.Connected)
        {
            Console.WriteLine($"Connection not ready. Current state: {_conn.State}");
            return;
        }
        
        try
        {
            Console.WriteLine($"Attempting to join group: {PortfolioId}");
            await _conn.InvokeAsync("JoinPortfolioGroup", PortfolioId);
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
        if (!IsConnected || _conn is null || string.IsNullOrWhiteSpace(PortfolioId)) return;
        try
        {
            await _conn.InvokeAsync("LeavePortfolioGroup", PortfolioId);
            Add("System", new { message = $"Left group {PortfolioId}" });
        }
        catch (Exception ex) { Add("Error", new { message = $"Leave failed: {ex.Message}" }); }
    }

    void Add(string title, object payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        Lines.Add($"{title}\n{json}");
        StateHasChanged();
    }

    // Specific method to handle processing updates from SignalR
    private void HandleProcessingUpdate(object update)
    {
        try
        {
            // Log the processing update
            Console.WriteLine($"SignalR Processing Update Received: {JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true })}");
            
            // Try to parse as JSON if it's a string
            if (update is string jsonString)
            {
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                    Console.WriteLine($"Parsed JSON: {JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true })}");
                    Add("ProcessingUpdate", jsonElement);
                }
                catch
                {
                    // If parsing fails, treat as regular string
                    Add("ProcessingUpdate", update);
                }
            }
            else
            {
                Add("ProcessingUpdate", update);
            }
            
            // You can add specific logic here based on the update content
            // For example, update UI, show notifications, etc.
            
            // Force UI refresh
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling processing update: {ex.Message}");
        }
    }

    // Method to update portfolio ID and join SignalR group
    private async Task UpdatePortfolioIdAndJoinGroup(string portfolioId)
    {
        try
        {
            PortfolioId = portfolioId;
            Console.WriteLine($"Updated Portfolio ID: {PortfolioId}");
            
            // If already connected, join the new group
            if (IsConnected && _conn != null)
            {
                await JoinGroupAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating portfolio ID: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conn != null)
        {
            try { await _conn.StopAsync(); } catch {}
            await _conn.DisposeAsync();
        }
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

            // Connect to SignalR and join portfolio group
            await ConnectAsync();
            
            // Wait for connection to be fully established before joining group
            if (IsConnected && _conn?.State == HubConnectionState.Connected)
            {
                await JoinGroupAsync();
            }
            else
            {
                Console.WriteLine("SignalR connection not ready, skipping group join");
            }

            

            Console.WriteLine(Lines);

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
                    
                    // // Extract validation results
                    // var portfolioId = result.GetProperty("PortfolioId").GetString();
                    // var status = result.GetProperty("Status").GetString();
                    
                    // // Get summary information
                    // var summary = result.GetProperty("Summary");
                    // var totalDocuments = summary.GetProperty("TotalDocuments").GetInt32();
                    // var validDocuments = summary.GetProperty("ValidDocuments").GetInt32();
                    // var invalidDocuments = summary.GetProperty("InvalidDocuments").GetInt32();
                    
                    // // Build validation message
                    // var validationMessage = $"Portfolio ID: {portfolioId}\n";
                    // validationMessage += $"Status: {status}\n\n";
                    // validationMessage += $"Total Documents: {totalDocuments}\n";
                    // validationMessage += $"Valid Documents: {validDocuments}\n";
                    // validationMessage += $"Invalid Documents: {invalidDocuments}\n\n";
                    
                    // // Add valid documents details
                    // if (validDocuments > 0)
                    // {
                    //     validationMessage += "✅ VALID DOCUMENTS:\n";
                    //     var validDocs = result.GetProperty("UploadedDocuments");
                    //     foreach (var doc in validDocs.EnumerateArray())
                    //     {
                    //         var fileName = doc.GetProperty("FileName").GetString();
                    //         var documentType = doc.GetProperty("DocumentType").GetString();
                    //         validationMessage += $"• {fileName} ({documentType})\n";
                    //     }
                    //     validationMessage += "\n";
                    // }
                    
                    // // Add invalid documents details
                    // if (invalidDocuments > 0)
                    // {
                    //     validationMessage += "❌ INVALID DOCUMENTS:\n";
                    //     var invalidDocs = result.GetProperty("InvalidDocuments");
                    //     foreach (var doc in invalidDocs.EnumerateArray())
                    //     {
                    //         var fileName = doc.GetProperty("FileName").GetString();
                    //         var reason = doc.GetProperty("Reason").GetString();
                    //         var identifiedType = doc.TryGetProperty("IdentifiedType", out var idType) ? idType.GetString() : "Unknown";
                    //         validationMessage += $"• {fileName} ({identifiedType}) - {reason}\n";
                    //     }
                    // }
                    
                    // Show validation results in alert
                    // await JSRuntime.InvokeVoidAsync("alert", validationMessage);
                    
                    // Console.WriteLine($"Portfolio ID: {portfolioId}");
                    // Console.WriteLine($"Status: {status}");
                    // Console.WriteLine($"Valid: {validDocuments}, Invalid: {invalidDocuments}");
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
