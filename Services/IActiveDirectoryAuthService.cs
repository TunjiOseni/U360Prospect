namespace U360Prospect.Services;

public interface IActiveDirectoryAuthService
{
    Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
