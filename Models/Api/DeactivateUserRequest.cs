using System.ComponentModel.DataAnnotations;

namespace U360Prospect.Models.Api;

public sealed class DeactivateUserRequest
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
