using System.ComponentModel.DataAnnotations;

namespace U360Prospect.Models.Api;

public sealed class AssignRoleRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string RoleName { get; set; } = string.Empty;
}
