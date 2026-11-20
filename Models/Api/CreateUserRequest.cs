
using System.ComponentModel.DataAnnotations;

namespace U360Prospect.Models.Api;

public sealed class CreateUserRequest
{
    [Required]
    [StringLength(256, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;
}
