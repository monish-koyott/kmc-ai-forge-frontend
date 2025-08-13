using Microsoft.AspNetCore.Components.Forms;

namespace KmcAiBlazorApp.Models;

public class UploadedFile
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string ContentType { get; set; } = "";
    public IBrowserFile? File { get; set; }
}
