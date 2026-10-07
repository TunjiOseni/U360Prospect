namespace U360Prospect.Models;

using System.ComponentModel.DataAnnotations;

public class Prospect
{
    // Used internally for Edit/Delete.
    // This is Oracle ROWID, not displayed to the user.
    public string? Id { get; set; }

    [Required(ErrorMessage = "Country is required")]
    [Display(Name = "Country")]
    public string Country { get; set; } = string.Empty;

    [Required(ErrorMessage = "Company Name is required")]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prospect Name is required")]
    [Display(Name = "Prospect Name")]
    public string ProspectName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prospect Type is required")]
    [Display(Name = "Prospect Type")]
    public string ProspectType { get; set; } = string.Empty;

    [Display(Name = "UBA Customer")]
    public string UbaCustomer { get; set; } = "No";

    [Display(Name = "Marketed By")]
    public string? MarketedBy { get; set; }

    [Display(Name = "Marketed Date")]
    [DataType(DataType.Date)]
    public DateTime? MarketedDate { get; set; }

    [Required(ErrorMessage = "Acquisition Status is required")]
    [Display(Name = "Acquisition Status")]
    public string AcquisitionStatus { get; set; } = "New";

    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    [Required(ErrorMessage = "Industry is required")]
    public string Industry { get; set; } = string.Empty;

    [Display(Name = "Uploaded Date")]
    [DataType(DataType.Date)]
    public DateTime? UploadedDate { get; set; }

    [Display(Name = "Converted Date")]
    [DataType(DataType.Date)]
    public DateTime? ConvertedDate { get; set; }

    [Display(Name = "Branch ID")]
    public string? BranchId { get; set; }

    [Display(Name = "State")]
    public string? State { get; set; }

    public string? Region { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }
}
