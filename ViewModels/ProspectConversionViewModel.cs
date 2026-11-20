
using System.ComponentModel.DataAnnotations;

namespace U360Prospect.ViewModels;

public sealed class ProspectConversionViewModel
{
    [Required]
    public string ProspectId { get; set; } = string.Empty;

    public string ProspectName { get; set; } = string.Empty;

    public string ProspectCountry { get; set; } = string.Empty;

    public DateTime? UploadedDate { get; set; }

    [Required(ErrorMessage = "Account number is required.")]
    [StringLength(50)]
    [Display(Name = "Account Number")]
    public string AccountNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "SOL ID is required.")]
    [RegularExpression(
        @"^\d{4}$",
        ErrorMessage = "SOL ID must contain exactly four digits, e.g. 0001.")]
    [Display(Name = "SOL ID")]
    public string SolId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [StringLength(100)]
    public string Country { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account opening date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Account Open Date")]
    public DateTime? AccountOpenDate { get; set; }
}
