using System.ComponentModel.DataAnnotations;

namespace U360Prospect.Models.Api;

public sealed class ReactivateUserRequest
{
    [StringLength(500)]
    public string? Reason { get; set; }
}
