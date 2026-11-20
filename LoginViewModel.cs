using System.ComponentModel.DataAnnotations;

namespace U360Prospect.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    // Retained temporarily for compatibility with the existing controller.
    // Entrust is disabled and this field is no longer required.
    public string TokenCode { get; set; } = string.Empty;
}
