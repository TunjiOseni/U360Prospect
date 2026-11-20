
using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;
using U360Prospect.Configuration;

namespace U360Prospect.Services;

public sealed class ActiveDirectoryAuthService
    : IActiveDirectoryAuthService
{
    private readonly ActiveDirectorySettings _settings;
    private readonly ILogger<ActiveDirectoryAuthService> _logger;

    public ActiveDirectoryAuthService(
        IOptions<ActiveDirectorySettings> options,
        ILogger<ActiveDirectoryAuthService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(_settings.LdapServer) ||
            string.IsNullOrWhiteSpace(_settings.Domain) ||
            !_settings.UseLdaps ||
            _settings.LdapPort != 636)
        {
            return Task.FromResult(false);
        }

        try
        {
            // Accept either username or DOMAIN\username.
            var accountName = username.Trim();

            if (accountName.Contains('\\'))
            {
                var parts = accountName.Split('\\');

                if (parts.Length != 2 ||
                    !string.Equals(
                        parts[0],
                        _settings.Domain,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(false);
                }

                accountName = parts[1];
            }

            // Use a domain-qualified bind identity.
            var bindIdentity = $"{accountName}@{_settings.Domain}";

            var identifier = new LdapDirectoryIdentifier(
                _settings.LdapServer,
                _settings.LdapPort,
                fullyQualifiedDnsHostName: true,
                connectionless: false);

            using var connection = new LdapConnection(identifier)
            {
                AuthType = AuthType.Basic,
                Timeout = TimeSpan.FromSeconds(10)
            };

            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = true;

            // Do not install a permissive certificate callback.
            // The platform must validate the server certificate.

            connection.Bind(
                new NetworkCredential(bindIdentity, password));

            return Task.FromResult(true);
        }
        catch (LdapException ex)
        {
            // Avoid logging credentials or LDAP bind payloads.
            _logger.LogWarning(
                "Active Directory authentication failed. LDAP error code: {ErrorCode}.",
                ex.ErrorCode);

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Active Directory authentication could not be completed.");

            return Task.FromResult(false);
        }
    }
}
