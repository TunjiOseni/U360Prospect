
using Oracle.ManagedDataAccess.Client;
using U360Prospect.Data;
using System.Data;

namespace U360Prospect.Services;

public sealed class AppUserAuthorizationService
{
    private readonly OracleConnectionFactory _connectionFactory;
    private readonly ILogger<AppUserAuthorizationService> _logger;
    private readonly string _authorizationSchema;

    public AppUserAuthorizationService(
    OracleConnectionFactory connectionFactory,
    ILogger<AppUserAuthorizationService> logger,
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


public async Task<IReadOnlyList<string>> GetRolesAndRecordLoginAsync(
    string username,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(username))
        return Array.Empty<string>();

    var normalizedUsername = username.Trim();

    if (normalizedUsername.Contains('\\'))
        normalizedUsername = normalizedUsername.Split('\\').Last();

    if (normalizedUsername.Contains('@'))
        normalizedUsername = normalizedUsername.Split('@')[0];

    try
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(cancellationToken);

        using var transaction =
            connection.BeginTransaction(IsolationLevel.ReadCommitted);

        // 1. Find and lock the application account.
        var userSql = $"""
    SELECT
        USER_ID,
        IS_ACTIVE,
        DISABLED_REASON,
    UPDATED_BY
    FROM {_authorizationSchema}.APP_USERS
    WHERE UPPER(TRIM(WINDOWS_USERNAME)) =
          UPPER(TRIM(:WindowsUsername))
    FOR UPDATE
    """;

long userId;
bool isActive;
string? disabledReason;
string? disabledBy;


        await using (var command =
    new OracleCommand(userSql, connection))
{
    command.Transaction = transaction;
    command.BindByName = true;

    command.Parameters.Add(
        "WindowsUsername",
        OracleDbType.Varchar2,
        256).Value = normalizedUsername;

    await using var reader =
        await command.ExecuteReaderAsync(cancellationToken);

    if (!await reader.ReadAsync(cancellationToken))
    {
        transaction.Rollback();

        _logger.LogWarning(
            "Application account not found for {Username}.",
            normalizedUsername);

        return Array.Empty<string>();
    }

    userId = Convert.ToInt64(reader["USER_ID"]);

    isActive = reader["IS_ACTIVE"].ToString() == "Y";

    disabledReason = reader["DISABLED_REASON"] == DBNull.Value
        ? null
        : reader["DISABLED_REASON"].ToString();

    disabledBy = reader["UPDATED_BY"] == DBNull.Value
    ? null
    : reader["UPDATED_BY"].ToString();
}


const string automaticInactivityReason =
    "Automatically disabled after 30 days of inactivity";

bool automaticallyDisabled =
    string.Equals(
        disabledReason,
        automaticInactivityReason,
        StringComparison.Ordinal) &&
    string.Equals(
        disabledBy,
        "SYSTEM",
        StringComparison.OrdinalIgnoreCase);


bool wasAutomaticallyDisabled =
    !isActive && automaticallyDisabled;


if (!isActive && !automaticallyDisabled)
{
    transaction.Rollback();

    _logger.LogWarning(
        "Login denied: account {Username} was administratively deactivated.",
        normalizedUsername);

    return Array.Empty<string>();
}

// 2. Retrieve the user's assigned application roles.
var rolesSql = $"""
    SELECT DISTINCT r.ROLE_NAME
    FROM {_authorizationSchema}.APP_USER_ROLES ur
    INNER JOIN {_authorizationSchema}.APP_ROLES r
        ON r.ROLE_ID = ur.ROLE_ID
    WHERE ur.USER_ID = :UserId
    """;

var roles = new List<string>();

await using (var command =
    new OracleCommand(rolesSql, connection))
{
    command.Transaction = transaction;
    command.BindByName = true;

    command.Parameters.Add(
        "UserId",
        OracleDbType.Int64).Value = userId;

    await using var reader =
        await command.ExecuteReaderAsync(cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        if (!reader.IsDBNull(0))
        {
            var role = reader.GetString(0).Trim();

            if (!string.IsNullOrWhiteSpace(role))
                roles.Add(role);
        }
    }
}

// No application role means no access.
if (roles.Count == 0)
{
    transaction.Rollback();

    _logger.LogWarning(
        "Login denied: no application roles assigned to {Username}.",
        normalizedUsername);

    return Array.Empty<string>();
}
        // 3. Reactivate the account and record successful login.
        var updateSql = $"""
            UPDATE {_authorizationSchema}.APP_USERS
            SET
                IS_ACTIVE = 'Y',
                LAST_LOGIN_DATE = SYSTIMESTAMP,
                DISABLED_DATE = NULL,
                DISABLED_REASON = NULL,
                UPDATED_AT = SYSTIMESTAMP,
                UPDATED_BY = 'SYSTEM'
            WHERE USER_ID = :UserId
            """;

        await using (var command =
            new OracleCommand(updateSql, connection))
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.Parameters.Add(
                "UserId",
                OracleDbType.Int64).Value = userId;

            var updatedRows =
    await command.ExecuteNonQueryAsync(cancellationToken);

if (updatedRows != 1)
{
    transaction.Rollback();

    _logger.LogWarning(
        "Login denied: account status could not be updated for {Username}.",
        normalizedUsername);

    return Array.Empty<string>();
}
        }



// Record automatic account reactivation separately from login.
if (wasAutomaticallyDisabled)
{
    var reactivationAuditSql = $"""
        INSERT INTO {_authorizationSchema}.APP_AUDIT_LOGS
        (
            ACTOR,
            ACTION,
            ENTITY_TYPE,
            ENTITY_ID,
            OUTCOME,
            DETAILS,
            IP_ADDRESS,
            CREATED_AT
        )
        VALUES
        (
            :Actor,
            'REACTIVATE_USER',
            'USER',
            :EntityId,
            'SUCCESS',
            :Details,
            NULL,
            SYSTIMESTAMP
        )
        """;

    await using var reactivationAuditCommand =
        new OracleCommand(reactivationAuditSql, connection);

    reactivationAuditCommand.Transaction = transaction;
    reactivationAuditCommand.BindByName = true;

    reactivationAuditCommand.Parameters.Add(
        "Actor",
        OracleDbType.Varchar2,
        100).Value = "SYSTEM";

    reactivationAuditCommand.Parameters.Add(
        "EntityId",
        OracleDbType.Varchar2,
        100).Value = userId.ToString();

    reactivationAuditCommand.Parameters.Add(
        "Details",
        OracleDbType.Varchar2).Value =
        $"Account {normalizedUsername} automatically reactivated during successful login.";

    await reactivationAuditCommand.ExecuteNonQueryAsync(
        cancellationToken);
}

        // 4. Record successful application login in the audit history.
var auditSql = $"""
    INSERT INTO {_authorizationSchema}.APP_AUDIT_LOGS
    (
        ACTOR,
        ACTION,
        ENTITY_TYPE,
        ENTITY_ID,
        OUTCOME,
        DETAILS,
        IP_ADDRESS,
        CREATED_AT
    )
    VALUES
    (
        :Actor,
        'LOGIN',
        'USER',
        :EntityId,
        'SUCCESS',
        'Application login successful.',
        NULL,
        SYSTIMESTAMP
    )
    """;

await using (var auditCommand =
    new OracleCommand(auditSql, connection))
{
    auditCommand.Transaction = transaction;
    auditCommand.BindByName = true;

    auditCommand.Parameters.Add(
        "Actor",
        OracleDbType.Varchar2,
        100).Value = normalizedUsername;

    auditCommand.Parameters.Add(
        "EntityId",
        OracleDbType.Varchar2,
        100).Value = userId.ToString();

    await auditCommand.ExecuteNonQueryAsync(cancellationToken);
}


        transaction.Commit();

        _logger.LogInformation(
            "Successful application authorization and login recorded for {Username}.",
            normalizedUsername);

     return roles.Distinct(
            StringComparer.OrdinalIgnoreCase).ToArray();
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Oracle account verification failed for {Username}.",
            normalizedUsername);

        return Array.Empty<string>();
    }
}



public async Task<int> DisableInactiveUsersAsync(
    CancellationToken cancellationToken = default)
{
    const string inactivityReason =
        "Automatically disabled after 30 days of inactivity";

    try
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(cancellationToken);

        using var transaction =
            connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            var inactiveUsers = new List<(long UserId, string Username)>();

            var selectSql = $"""
                SELECT USER_ID, WINDOWS_USERNAME
                FROM {_authorizationSchema}.APP_USERS
                WHERE IS_ACTIVE = 'Y'
                  AND LAST_LOGIN_DATE IS NOT NULL
                  AND LAST_LOGIN_DATE <
                      SYSTIMESTAMP - INTERVAL '30' DAY
                FOR UPDATE
                """;

            await using (var selectCommand =
                new OracleCommand(selectSql, connection))
            {
                selectCommand.Transaction = transaction;

                await using var reader =
                    await selectCommand.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    inactiveUsers.Add((
                        Convert.ToInt64(reader["USER_ID"]),
                        reader["WINDOWS_USERNAME"]?.ToString() ?? "UNKNOWN"
                    ));
                }
            }

            foreach (var user in inactiveUsers)
            {
                var updateSql = $"""
                    UPDATE {_authorizationSchema}.APP_USERS
                    SET
                        IS_ACTIVE = 'N',
                        DISABLED_DATE = SYSTIMESTAMP,
                        DISABLED_REASON = :Reason,
                        UPDATED_AT = SYSTIMESTAMP,
                        UPDATED_BY = 'SYSTEM'
                    WHERE USER_ID = :UserId
                      AND IS_ACTIVE = 'Y'
                    """;

                await using (var updateCommand =
                    new OracleCommand(updateSql, connection))
                {
                    updateCommand.Transaction = transaction;
                    updateCommand.BindByName = true;

                    updateCommand.Parameters.Add(
                        "Reason",
                        OracleDbType.Varchar2).Value = inactivityReason;

                    updateCommand.Parameters.Add(
                        "UserId",
                        OracleDbType.Int64).Value = user.UserId;

                    var updatedRows =
                        await updateCommand.ExecuteNonQueryAsync(
                            cancellationToken);

                    if (updatedRows != 1)
                    {
                        throw new InvalidOperationException(
                            $"Could not disable inactive account {user.Username}.");
                    }
                }

                var auditSql = $"""
                    INSERT INTO {_authorizationSchema}.APP_AUDIT_LOGS
                    (
                        ACTOR,
                        ACTION,
                        ENTITY_TYPE,
                        ENTITY_ID,
                        OUTCOME,
                        DETAILS,
                        IP_ADDRESS,
                        CREATED_AT
                    )
                    VALUES
                    (
                        'SYSTEM',
                        'DEACTIVATE_USER',
                        'USER',
                        :EntityId,
                        'SUCCESS',
                        :Details,
                        NULL,
                        SYSTIMESTAMP
                    )
                    """;

                await using (var auditCommand =
                    new OracleCommand(auditSql, connection))
                {
                    auditCommand.Transaction = transaction;
                    auditCommand.BindByName = true;

                    auditCommand.Parameters.Add(
                        "EntityId",
                        OracleDbType.Varchar2).Value =
                        user.UserId.ToString();

                    auditCommand.Parameters.Add(
                        "Details",
                        OracleDbType.Varchar2).Value =
                        $"Account {user.Username} automatically disabled after 30 days of inactivity.";

                    await auditCommand.ExecuteNonQueryAsync(
                        cancellationToken);
                }
            }

            transaction.Commit();

            if (inactiveUsers.Count > 0)
            {
                _logger.LogInformation(
                    "{Count} inactive application accounts were disabled and audited.",
                    inactiveUsers.Count);
            }

            return inactiveUsers.Count;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to disable inactive application accounts.");

        throw;
    }
}



    public async Task<IReadOnlyList<string>> GetRolesAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Array.Empty<string>();

        // Accept username, DOMAIN\username, or username@domain.
        var normalizedUsername = username.Trim();

        if (normalizedUsername.Contains('\\'))
            normalizedUsername = normalizedUsername.Split('\\').Last();

        if (normalizedUsername.Contains('@'))
            normalizedUsername = normalizedUsername.Split('@')[0];

        try
        {
            await using var connection =
                _connectionFactory.CreateConnection();

            await connection.OpenAsync(cancellationToken);

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

            await using var command = new OracleCommand(sql, connection);

            command.Parameters.Add(
                "WindowsUsername",
                OracleDbType.Varchar2,
                256).Value = normalizedUsername;

            var roles = new List<string>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    var role = reader.GetString(0).Trim();

                    if (!string.IsNullOrWhiteSpace(role))
                        roles.Add(role);
                }
            }

            return roles.Distinct(
                StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Oracle authorization lookup failed for username {Username}.",
                normalizedUsername);

            // Fail closed: no roles are returned if Oracle is unavailable.
            return Array.Empty<string>();
        }
    }

public async Task<bool> IsAccountActiveAsync(
    string username,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(username))
        return false;

    var normalizedUsername = username.Trim();

    if (normalizedUsername.Contains('\\'))
        normalizedUsername = normalizedUsername.Split('\\').Last();

    if (normalizedUsername.Contains('@'))
        normalizedUsername = normalizedUsername.Split('@')[0];

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    var sql = $"""
        SELECT IS_ACTIVE
        FROM {_authorizationSchema}.APP_USERS
        WHERE UPPER(TRIM(WINDOWS_USERNAME)) =
              UPPER(TRIM(:WindowsUsername))
        """;

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    command.Parameters.Add(
        "WindowsUsername",
        OracleDbType.Varchar2,
        256).Value = normalizedUsername;

    var result = await command.ExecuteScalarAsync(cancellationToken);

    return result != null &&
           result != DBNull.Value &&
           result.ToString() == "Y";
}

}
