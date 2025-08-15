using Microsoft.AspNetCore.Components;
using System.Text.Json;
using KmcAiBlazorApp.Models;

namespace KmcAiBlazorApp.Components;

public partial class ProcessingModal : ComponentBase
{
    [Parameter]
    public bool IsVisible { get; set; } = false;
    
    [Parameter]
    public EventCallback<bool> IsVisibleChanged { get; set; }
    
    [Parameter]
    public EventCallback CancelProcessingRequested { get; set; }
    
    [Parameter]
    public EventCallback ViewResultsRequested { get; set; }
    
    public List<ProcessingStep> ProcessingSteps { get; set; } = new();
    public int ProgressPercentage { get; set; } = 0;
    public bool IsProcessingComplete { get; set; } = false;
    
    protected override void OnInitialized()
    {
        InitializeProcessingSteps();
    }
    
    private void InitializeProcessingSteps()
    {
        ProcessingSteps = new List<ProcessingStep>
        {
            new ProcessingStep { Description = "Document Validation", Status = ProcessingStatus.Alert },
            new ProcessingStep { Description = "Portfolio Completion", Status = ProcessingStatus.Alert },
            new ProcessingStep { Description = "Company House Validation", Status = ProcessingStatus.Alert },
            new ProcessingStep { Description = "Final Processing & Completion", Status = ProcessingStatus.Alert }
        };
        
        UpdateProgress();
    }
    
    private void UpdateProgress()
    {
        // Use the progress percentage from backend SignalR updates
        // The backend sends: DocumentValidation=25%, PortfolioCompletion=50%, CompanyHouseValidation=75%, ProcessingComplete=100%
        
        var completedSteps = ProcessingSteps.Count(s => s.Status == ProcessingStatus.Success);
        var totalSteps = ProcessingSteps.Count;
        
        // Calculate base progress from completed steps
        var baseProgress = totalSteps > 0 ? (completedSteps * 100) / totalSteps : 0;
        
        // Add progress for in-progress steps
        var inProgressSteps = ProcessingSteps.Count(s => s.Status == ProcessingStatus.InProgress);
        if (inProgressSteps > 0)
        {
            // Show progress for in-progress steps (25% per step)
            ProgressPercentage = Math.Min(baseProgress + (inProgressSteps * 25), 100);
        }
        else
        {
            // If no steps are in progress, show base progress (completed steps only)
            ProgressPercentage = baseProgress;
        }
        
        // Check if all steps are completed (either success or failure, but not alert or in-progress)
        IsProcessingComplete = ProcessingSteps.All(s => s.Status == ProcessingStatus.Success || s.Status == ProcessingStatus.Failure);
        
        Console.WriteLine($"Progress Update - Completed: {completedSteps}, InProgress: {inProgressSteps}, Total: {totalSteps}, Progress: {ProgressPercentage}%");
    }
    
    public void UpdateStepStatus(string stepDescription, ProcessingStatus status)
    {
        var step = ProcessingSteps.FirstOrDefault(s => s.Description == stepDescription);
        if (step != null)
        {
            step.Status = status;
            UpdateProgress();
            StateHasChanged();
        }
    }
    
    public void UpdateValidationMessage(string stepDescription, string validationMessage)
    {
        var step = ProcessingSteps.FirstOrDefault(s => s.Description == stepDescription);
        if (step != null)
        {
            step.ValidationMessage = validationMessage;
            StateHasChanged();
        }
    }
    
    public void UpdateFromApiResult(JsonElement result)
    {
        try
        {
            // Update portfolio ID if available
            if (result.TryGetProperty("PortfolioId", out var portfolioId))
            {
                Console.WriteLine($"Portfolio ID: {portfolioId.GetString()}");
            }
            
            // Update summary information
            if (result.TryGetProperty("Summary", out var summary))
            {
                if (summary.TryGetProperty("TotalDocuments", out var totalDocs))
                {
                    Console.WriteLine($"Total Documents: {totalDocs.GetInt32()}");
                }
                
                if (summary.TryGetProperty("ValidDocuments", out var validDocs))
                {
                    Console.WriteLine($"Valid Documents: {validDocs.GetInt32()}");
                }
                
                if (summary.TryGetProperty("InvalidDocuments", out var invalidDocs))
                {
                    Console.WriteLine($"Invalid Documents: {invalidDocs.GetInt32()}");
                }
            }
            
            // Update step statuses based on API response
            UpdateStepStatus("Uploading documents", ProcessingStatus.Success);
            UpdateValidationMessage("Uploading documents", "Documents uploaded successfully");
            
            UpdateStepStatus("Checking completeness of Portfolio form", ProcessingStatus.Success);
            UpdateValidationMessage("Checking completeness of Portfolio form", "Portfolio form validation completed");
            
            // Update property validation step with actual data from API
            var propertyStep = ProcessingSteps.FirstOrDefault(s => s.Description.Contains("Validating portfolio properties"));
            if (propertyStep != null)
            {
                // Try to extract property information from the API response
                string propertyMessage = "Property validation completed";
                
                if (result.TryGetProperty("PortfolioSummary", out var portfolioSummary))
                {
                    if (portfolioSummary.TryGetProperty("TotalProperties", out var totalProps) && 
                        portfolioSummary.TryGetProperty("ExcludedProperties", out var excludedProps))
                    {
                        var total = totalProps.GetInt32();
                        var excluded = excludedProps.GetInt32();
                        var valid = total - excluded;
                        
                        propertyStep.Description = $"Validating portfolio properties ({valid} valid, {excluded} excluded)";
                        propertyMessage = $"Total properties: {total}, Valid: {valid}, Excluded: {excluded}";
                    }
                }
                else if (result.TryGetProperty("Summary", out var summarySection))
                {
                    // Alternative property information location
                    if (summarySection.TryGetProperty("TotalProperties", out var totalProps) && 
                        summarySection.TryGetProperty("ExcludedProperties", out var excludedProps))
                    {
                        var total = totalProps.GetInt32();
                        var excluded = excludedProps.GetInt32();
                        var valid = total - excluded;
                        
                        propertyStep.Description = $"Validating portfolio properties ({valid} valid, {excluded} excluded)";
                        propertyMessage = $"Total properties: {total}, Valid: {valid}, Excluded: {excluded}";
                    }
                }
                
                UpdateStepStatus(propertyStep.Description, ProcessingStatus.Success);
                UpdateValidationMessage(propertyStep.Description, propertyMessage);
            }
            
            // Check for validation results
            if (result.TryGetProperty("UploadedDocuments", out var validDocuments))
            {
                UpdateStepStatus("Verifying Rental Income", ProcessingStatus.Success);
                UpdateValidationMessage("Verifying Rental Income", "Rental income verification successful");
            }
            else
            {
                UpdateStepStatus("Verifying Rental Income", ProcessingStatus.Failure);
                UpdateValidationMessage("Verifying Rental Income", "Rental income verification failed");
            }
            
            if (result.TryGetProperty("InvalidDocuments", out var invalidDocuments))
            {
                var invalidCount = invalidDocuments.GetArrayLength();
                if (invalidCount > 0)
                {
                    UpdateStepStatus("Verifying CMI & Balance", ProcessingStatus.Failure);
                    UpdateValidationMessage("Verifying CMI & Balance", $"CMI & Balance verification failed - {invalidCount} invalid documents");
                }
                else
                {
                    UpdateStepStatus("Verifying CMI & Balance", ProcessingStatus.Success);
                    UpdateValidationMessage("Verifying CMI & Balance", "CMI & Balance verification successful");
                }
            }
            
            UpdateStepStatus("Verifying Mortgage conduct", ProcessingStatus.Success);
            UpdateValidationMessage("Verifying Mortgage conduct", "Mortgage conduct verification completed");
            
            UpdateProgress();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating modal from API result: {ex.Message}");
        }
    }
    
    public void UpdateProgress(int percentage)
    {
        ProgressPercentage = Math.Clamp(percentage, 0, 100);
        Console.WriteLine($"ProcessingModal.UpdateProgress called with: {percentage}%");
        StateHasChanged();
    }
    
    private async Task CloseModal()
    {
        IsVisible = false;
        await IsVisibleChanged.InvokeAsync(IsVisible);
    }
    
    private async Task CancelProcessing()
    {
        await CancelProcessingRequested.InvokeAsync();
        await CloseModal();
    }
    
    private async Task ViewResults()
    {
        await ViewResultsRequested.InvokeAsync();
        await CloseModal();
    }
}

public class ProcessingStep
{
    public string Description { get; set; } = string.Empty;
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Alert;
    public string ValidationMessage { get; set; } = string.Empty;
}
