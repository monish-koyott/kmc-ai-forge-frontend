using Microsoft.AspNetCore.Components;
using KmcAiBlazorApp.Models;

namespace KmcAiBlazorApp.Components;

public partial class UploadedFiles : ComponentBase
{
    [Parameter]
    public List<UploadedFile> Files { get; set; } = new();
    
    [Parameter]
    public EventCallback<UploadedFile> OnFileRemoved { get; set; }
    
    private string GetFileIcon(string contentType)
    {
        return contentType.ToLower() switch
        {
            var type when type.Contains("pdf") => "📄",
            var type when type.Contains("word") || type.Contains("doc") => "📝",
            var type when type.Contains("excel") || type.Contains("sheet") || type.Contains("xls") => "📊",
            var type when type.Contains("image") || type.Contains("jpg") || type.Contains("jpeg") || type.Contains("png") => "🖼️",
            _ => "📎"
        };
    }
    
    private string GetFileType(string contentType)
    {
        return contentType.ToLower() switch
        {
            var type when type.Contains("pdf") => "PDF Document",
            var type when type.Contains("word") || type.Contains("doc") => "Word Document",
            var type when type.Contains("excel") || type.Contains("sheet") || type.Contains("xls") => "Excel Spreadsheet",
            var type when type.Contains("image") || type.Contains("jpg") || type.Contains("jpeg") => "JPEG Image",
            var type when type.Contains("png") => "PNG Image",
            _ => "Unknown File Type"
        };
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {sizes[order]}";
    }
    
    private async Task RemoveFile(UploadedFile file)
    {
        Files.Remove(file);
        await OnFileRemoved.InvokeAsync(file);
    }
}
