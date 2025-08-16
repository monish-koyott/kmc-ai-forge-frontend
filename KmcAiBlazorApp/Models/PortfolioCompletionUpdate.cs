namespace KmcAiBlazorApp.Models;

    public class PortfolioCompletionUpdate : ProcessingUpdate
    {
        public bool HasPortfolioData { get; set; }
        public string? CompanyName { get; set; }
        public int PropertyCount { get; set; }
    }