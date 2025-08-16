namespace KmcAiBlazorApp.Models;


    public class CompanyHouseValidationUpdate : ProcessingUpdate
    {
        public bool HasCompanyData { get; set; }
        public string? CompanyNumber { get; set; }
        public int ChargeCount { get; set; }
    }
