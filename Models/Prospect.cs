namespace U360Prospect.Models;
using System.ComponentModel.DataAnnotations;


public class Prospect
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Prospect Name is required")]
    public string ProspectName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Industry is required")]
    public string Industry { get; set; } = string.Empty;

    [Required(ErrorMessage = "Company Type is required")]
    public string CompanyType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Region is required")]
    public string Region { get; set; } = string.Empty;

    public string? Address { get; set; }
   
    public string? Email { get; set; }
   
    public string? PhoneNumber { get; set; }

    public string LoadBy { get; set; } = string.Empty;

    public DateTime? LoadDate { get; set; }
}
