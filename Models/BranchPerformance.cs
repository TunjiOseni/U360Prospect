namespace U360Prospect.Models;

public class BranchPerformance
{
    public string BranchId { get; set; } = string.Empty;

    public int TotalProspects { get; set; }

    public int ConvertedCustomers { get; set; }

    public decimal ConversionRate { get; set; }
}
