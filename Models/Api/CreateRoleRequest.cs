
using System.ComponentModel.DataAnnotations;

namespace U360Prospect.Models.Api;

public sealed class CreateRoleRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(
        @"^[A-Za-z][A-Za-z0-9_-]*$",
        ErrorMessage = "Role name must start with a letter and contain only letters, numbers, underscores or hyphens.")]
    public string RoleName { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }
}
