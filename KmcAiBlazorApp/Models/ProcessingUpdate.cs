namespace KmcAiBlazorApp.Models;

 public class ProcessingUpdate
    {
        public string PortfolioId { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public int Progress { get; set; } = 0;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Data { get; set; }
        public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.InProgress;
        public ProcessingStep ProcessingStep { get; set; } = ProcessingStep.DocumentValidation;
    }
