namespace U360Prospect.Models.Api;

public sealed class UserDetailsResponse
{
    public long UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public List<string> Roles { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginDate { get; set; }
}
