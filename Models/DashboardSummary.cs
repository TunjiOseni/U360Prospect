namespace U360Prospect.Models;

public class DashboardSummary
{
    public int TotalProspects { get; set; }

    public int ActiveProspects { get; set; }

    public int ConvertedCustomers { get; set; }

    public decimal ConversionRate { get; set; }

    public decimal AverageConversionDays { get; set; }

    public int NewCount { get; set; }

    public int ContactedCount { get; set; }

    public int EngagedCount { get; set; }

    public int InterestedCount { get; set; }

    public int NotInterestedCount { get; set; }

    public int LostCount { get; set; }

    public int ConvertedCount { get; set; }
}
