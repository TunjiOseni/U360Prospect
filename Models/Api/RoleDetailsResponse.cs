namespace U360Prospect.Models.Api;

public sealed class RoleDetailsResponse
{
    public long RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public int AssignedUsersCount { get; set; }
}
