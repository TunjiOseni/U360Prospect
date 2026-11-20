namespace U360Prospect.Models.Api;

public sealed class UserListResponse
{
    public List<UserDetailsResponse> Users { get; set; } = new();

    public int TotalCount => Users.Count;
}
