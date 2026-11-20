using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Oracle.ManagedDataAccess.Client;
using U360Prospect.Data;


namespace U360Prospect.Services;

public sealed class AppRoleService : IClaimsTransformation
{
    private readonly OracleConnectionFactory _connectionFactory;
    private readonly ILogger<AppRoleService> _logger;
    private readonly string _authorizationSchema;

    public AppRoleService(
    OracleConnectionFactory connectionFactory,
    ILogger<AppRoleService> logger,
    IConfiguration configuration)
{
    _connectionFactory = connectionFactory;
    _logger = logger;

    var configuredSchema =
        configuration["Authorization:Schema"]?.Trim();

    if (!string.Equals(
        configuredSchema,
        "U360PROSPECT",
        StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Authorization schema must be U360PROSPECT.");
    }

    _authorizationSchema = "U360PROSPECT";
}


    public async Task<ClaimsPrincipal> TransformAsync(
        ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;

        if (identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(identity.Name))
        {
            return principal;
        }

        // Avoid adding the same role claims more than once.
        if (identity.HasClaim(
                claim => claim.Type == "AppRolesLoaded"))
        {
            return principal;
        }

        try
        {
            await using var connection =
                _connectionFactory.CreateConnection();

            await connection.OpenAsync();

                var sql = $"""
                    SELECT DISTINCT r.ROLE_NAME
                    FROM {_authorizationSchema}.APP_USERS u
                    INNER JOIN {_authorizationSchema}.APP_USER_ROLES ur
                        ON ur.USER_ID = u.USER_ID
                    INNER JOIN {_authorizationSchema}.APP_ROLES r
                        ON r.ROLE_ID = ur.ROLE_ID
                    WHERE UPPER(TRIM(u.WINDOWS_USERNAME)) =
                          UPPER(TRIM(:WindowsUsername))
                      AND u.IS_ACTIVE = 'Y'
                """;

            await using var command =
                new OracleCommand(sql, connection);

            command.Parameters.Add(
                "WindowsUsername",
                OracleDbType.Varchar2,
                256).Value = identity.Name;

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var roleName = reader.GetString(0).Trim();

                if (!string.IsNullOrWhiteSpace(roleName) &&
                    !identity.HasClaim(
                        ClaimTypes.Role, roleName))
                {
                    identity.AddClaim(
                        new Claim(ClaimTypes.Role, roleName));
                }
            }

            identity.AddClaim(
                new Claim("AppRolesLoaded", "true"));
        }
        catch (Exception ex)
        {
            // Fail closed: do not grant application roles
            // if the role lookup fails.
            _logger.LogError(
                ex,
                "Failed to load application roles for {Username}",
                identity.Name);
        }

        return principal;
    }
}
