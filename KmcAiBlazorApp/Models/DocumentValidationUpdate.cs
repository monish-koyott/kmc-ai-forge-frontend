namespace KmcAiBlazorApp.Models;

public class DocumentValidationUpdate : ProcessingUpdate
    {
        public int TotalDocuments { get; set; }
        public int ValidDocuments { get; set; }
        public int InvalidDocuments { get; set; }
        public List<string> ValidFileNames { get; set; } = new List<string>();
        public List<string> InvalidFileNames { get; set; } = new List<string>();
    }
