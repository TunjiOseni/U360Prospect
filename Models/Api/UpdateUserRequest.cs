using System.ComponentModel.DataAnnotations;

namespace U360Prospect.Models.Api;

public sealed class UpdateUserRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;
}
