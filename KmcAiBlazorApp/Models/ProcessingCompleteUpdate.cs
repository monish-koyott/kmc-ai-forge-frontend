namespace KmcAiBlazorApp.Models;

    public class ProcessingCompleteUpdate : ProcessingUpdate
    {
        public string ProcessingTime { get; set; } = "";
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }