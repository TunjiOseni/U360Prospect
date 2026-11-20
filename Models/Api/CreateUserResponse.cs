namespace U360Prospect.Models.Api;

public sealed class CreateUserResponse
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "USER";
    public bool IsActive { get; set; }
    public string Message { get; set; } = string.Empty;
}
