using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using U360Prospect.Data;
using U360Prospect.Models.Api;

namespace U360Prospect.Services;

public sealed class UserProvisioningService
{
    private readonly OracleConnectionFactory _connectionFactory;
    private readonly ILogger<UserProvisioningService> _logger;
    private readonly string _schema;


    public UserProvisioningService(
    OracleConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<UserProvisioningService> logger)
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

    _schema = "U360PROSPECT";
}

    public async Task<CreateUserResponse?> CreateUserAsync(
        CreateUserRequest request,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        if (username.Contains('\\') ||
            username.Contains('@') ||
            string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Provide the AD username without a domain prefix.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException(
                "The provisioning actor must be identified.");
        }

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();

        try
        {
            // Check whether this username already exists.
            using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.Transaction = transaction;
                checkCommand.BindByName = true;

                checkCommand.CommandText = $@"
                    SELECT COUNT(*)
                    FROM {_schema}.APP_USERS
                    WHERE UPPER(TRIM(WINDOWS_USERNAME)) =
                          UPPER(:Username)";

                checkCommand.Parameters.Add(
                    "Username",
                    OracleDbType.Varchar2
                ).Value = username;

                var count = Convert.ToInt32(
                    await checkCommand.ExecuteScalarAsync(
                        cancellationToken));

                if (count > 0)
                {
                    transaction.Rollback();

                    _logger.LogWarning(
                        "User provisioning rejected because username {Username} already exists.",
                        username);

                    return null;
                }
            }

            // Find the standard USER role.
            long userRoleId;

            using (var roleCommand = connection.CreateCommand())
            {
                roleCommand.Transaction = transaction;
                roleCommand.BindByName = true;

                roleCommand.CommandText = $@"
                    SELECT ROLE_ID
                    FROM {_schema}.APP_ROLES
                    WHERE UPPER(TRIM(ROLE_NAME)) = 'USER'";

                var roleResult =
                    await roleCommand.ExecuteScalarAsync(
                        cancellationToken);

                if (roleResult == null ||
                    roleResult == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        "The USER role does not exist in APP_ROLES.");
                }

                userRoleId = Convert.ToInt64(
    roleResult.ToString(),
    CultureInfo.InvariantCulture);
            }

            // Create the application user.
            long newUserId;

            using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.Transaction = transaction;
                insertCommand.BindByName = true;

                insertCommand.CommandText = $@"
                    INSERT INTO {_schema}.APP_USERS
                    (
                        WINDOWS_USERNAME,
                        DISPLAY_NAME,
                        IS_ACTIVE,
                        CREATED_BY
                    )
                    VALUES
                    (
                        :Username,
                        :DisplayName,
                        'Y',
                        :CreatedBy
                    )
                    RETURNING USER_ID INTO :NewUserId";

                insertCommand.Parameters.Add(
                    "Username",
                    OracleDbType.Varchar2
                ).Value = username;

                insertCommand.Parameters.Add(
                    "DisplayName",
                    OracleDbType.Varchar2
                ).Value = displayName;

                insertCommand.Parameters.Add(
                    "CreatedBy",
                    OracleDbType.Varchar2
                ).Value = createdBy;

                var userIdParameter = new OracleParameter(
                    "NewUserId",
                    OracleDbType.Decimal)
                {
                    Direction = System.Data.ParameterDirection.Output
                };

                insertCommand.Parameters.Add(userIdParameter);

                await insertCommand.ExecuteNonQueryAsync(
                    cancellationToken);

                newUserId = Convert.ToInt64(
                    userIdParameter.Value.ToString(),
                    CultureInfo.InvariantCulture);
            }

            // Assign the standard USER role.
            using (var assignmentCommand = connection.CreateCommand())
            {
                assignmentCommand.Transaction = transaction;
                assignmentCommand.BindByName = true;

                assignmentCommand.CommandText = $@"
                    INSERT INTO {_schema}.APP_USER_ROLES
                    (
                        USER_ID,
                        ROLE_ID,
                        ASSIGNED_BY
                    )
                    VALUES
                    (
                        :UserId,
                        :RoleId,
                        :AssignedBy
                    )";

                assignmentCommand.Parameters.Add(
                    "UserId",
                    OracleDbType.Int64
                ).Value = newUserId;

                assignmentCommand.Parameters.Add(
                    "RoleId",
                    OracleDbType.Int64
                ).Value = userRoleId;

                assignmentCommand.Parameters.Add(
                    "AssignedBy",
                    OracleDbType.Varchar2
                ).Value = createdBy;

                await assignmentCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            // Record successful user creation in the same transaction.
using (var auditCommand = connection.CreateCommand())
{
    auditCommand.Transaction = transaction;
    auditCommand.BindByName = true;

    auditCommand.CommandText = $@"
        INSERT INTO {_schema}.APP_AUDIT_LOGS
        (
            ACTOR, ACTION, ENTITY_TYPE, ENTITY_ID,
            OUTCOME, DETAILS, CREATED_AT
        )
        VALUES
        (
            :Actor, :Action, :EntityType, :EntityId,
            :Outcome, :Details, SYSTIMESTAMP
        )";

    auditCommand.Parameters.Add(
        "Actor", OracleDbType.Varchar2).Value = createdBy;

    auditCommand.Parameters.Add(
        "Action", OracleDbType.Varchar2).Value = "CREATE_USER";

    auditCommand.Parameters.Add(
        "EntityType", OracleDbType.Varchar2).Value = "USER";

    auditCommand.Parameters.Add(
        "EntityId", OracleDbType.Varchar2).Value =
        newUserId.ToString(CultureInfo.InvariantCulture);

    auditCommand.Parameters.Add(
        "Outcome", OracleDbType.Varchar2).Value = "SUCCESS";

    auditCommand.Parameters.Add(
        "Details", OracleDbType.Varchar2).Value =
        $"User {username} created and assigned the default USER role.";

    await auditCommand.ExecuteNonQueryAsync(cancellationToken);
}

// Commit user creation, role assignment and audit record together.
transaction.Commit();

            _logger.LogInformation(
                "User {Username} was provisioned successfully with USER role. User ID: {UserId}",
                username,
                newUserId);

            return new CreateUserResponse
            {
                UserId = newUserId,
                Username = username,
                DisplayName = displayName,
                Role = "USER",
                IsActive = true,
                Message = "User provisioned successfully."
            };
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            transaction.Rollback();

            _logger.LogWarning(
                ex,
                "Duplicate user detected during provisioning for {Username}.",
                username);

            return null;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public async Task<UserDetailsResponse?> GetUserAsync(
    string username,
    CancellationToken cancellationToken = default)
{
    username = username.Trim().ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(username) ||
        username.Contains('\\') ||
        username.Contains('@'))
    {
        throw new ArgumentException(
            "Provide a valid AD username without a domain prefix.");
    }

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    long userId;
    string displayName;
    string storedUsername;
    bool isActive;
    DateTime createdAt;
    DateTime? lastLoginDate;

    using (var command = connection.CreateCommand())
    {
        command.BindByName = true;

        command.CommandText = $@"
            SELECT
                USER_ID,
                WINDOWS_USERNAME,
                DISPLAY_NAME,
                IS_ACTIVE,
                CREATED_AT,
                LAST_LOGIN_DATE
            FROM {_schema}.APP_USERS
            WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)";

        command.Parameters.Add(
            "Username",
            OracleDbType.Varchar2
        ).Value = username;

        using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        userId = Convert.ToInt64(reader["USER_ID"]);
        storedUsername = reader["WINDOWS_USERNAME"].ToString()!;
        displayName = reader["DISPLAY_NAME"] == DBNull.Value
            ? string.Empty
            : reader["DISPLAY_NAME"].ToString()!;

        isActive = reader["IS_ACTIVE"].ToString() == "Y";

        createdAt = Convert.ToDateTime(reader["CREATED_AT"]);

        lastLoginDate = reader["LAST_LOGIN_DATE"] == DBNull.Value
            ? null
            : Convert.ToDateTime(reader["LAST_LOGIN_DATE"]);
    }

    var roles = new List<string>();

    using (var roleCommand = connection.CreateCommand())
    {
        roleCommand.BindByName = true;

        roleCommand.CommandText = $@"
            SELECT R.ROLE_NAME
            FROM {_schema}.APP_USER_ROLES UR
            INNER JOIN {_schema}.APP_ROLES R
                ON R.ROLE_ID = UR.ROLE_ID
            WHERE UR.USER_ID = :UserId
            ORDER BY R.ROLE_NAME";

        roleCommand.Parameters.Add(
            "UserId",
            OracleDbType.Int64
        ).Value = userId;

        using var roleReader = await roleCommand.ExecuteReaderAsync(
            cancellationToken);

        while (await roleReader.ReadAsync(cancellationToken))
        {
            roles.Add(roleReader["ROLE_NAME"].ToString()!);
        }
    }

    return new UserDetailsResponse
    {
        UserId = userId,
        Username = storedUsername,
        DisplayName = displayName,
        IsActive = isActive,
        Roles = roles,
        CreatedAt = createdAt,
        LastLoginDate = lastLoginDate
    };
}


public async Task<List<UserDetailsResponse>> GetAllUsersAsync(
    CancellationToken cancellationToken = default)
{
    var users = new Dictionary<long, UserDetailsResponse>();

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var command = connection.CreateCommand();

    command.CommandText = $@"
        SELECT
            U.USER_ID,
            U.WINDOWS_USERNAME,
            U.DISPLAY_NAME,
            U.IS_ACTIVE,
            U.CREATED_AT,
            U.LAST_LOGIN_DATE,
            R.ROLE_NAME
        FROM {_schema}.APP_USERS U
        LEFT JOIN {_schema}.APP_USER_ROLES UR
            ON UR.USER_ID = U.USER_ID
        LEFT JOIN {_schema}.APP_ROLES R
            ON R.ROLE_ID = UR.ROLE_ID
        ORDER BY U.DISPLAY_NAME, R.ROLE_NAME";

    using var reader = await command.ExecuteReaderAsync(
        cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        var userId = Convert.ToInt64(reader["USER_ID"]);

        if (!users.TryGetValue(userId, out var user))
        {
            user = new UserDetailsResponse
            {
                UserId = userId,
                Username = reader["WINDOWS_USERNAME"].ToString()!,
                DisplayName = reader["DISPLAY_NAME"] == DBNull.Value
                    ? string.Empty
                    : reader["DISPLAY_NAME"].ToString()!,
                IsActive = reader["IS_ACTIVE"].ToString() == "Y",
                CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                LastLoginDate = reader["LAST_LOGIN_DATE"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["LAST_LOGIN_DATE"])
            };

            users.Add(userId, user);
        }

        if (reader["ROLE_NAME"] != DBNull.Value)
        {
            user.Roles.Add(reader["ROLE_NAME"].ToString()!);
        }
    }

    return users.Values.ToList();
}


public async Task<bool> UpdateUserAsync(
    string username,
    UpdateUserRequest request,
    string updatedBy,
    CancellationToken cancellationToken = default)
{
    username = username.Trim().ToLowerInvariant();
    var displayName = request.DisplayName.Trim();

    if (string.IsNullOrWhiteSpace(username) ||
        username.Contains('\\') ||
        username.Contains('@'))
    {
        throw new ArgumentException(
            "Provide a valid AD username without a domain prefix.");
    }

    if (string.IsNullOrWhiteSpace(displayName))
    {
        throw new ArgumentException("Display name is required.");
    }

    if (string.IsNullOrWhiteSpace(updatedBy))
    {
        throw new ArgumentException(
            "The updating user must be identified.");
    }

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var transaction = connection.BeginTransaction();

    try
    {

	long userId;

using (var userIdCommand = connection.CreateCommand())
{
    userIdCommand.Transaction = transaction;
    userIdCommand.BindByName = true;

    userIdCommand.CommandText = $@"
        SELECT USER_ID
        FROM {_schema}.APP_USERS
        WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)";

    userIdCommand.Parameters.Add(
        "Username",
        OracleDbType.Varchar2).Value = username;

    var result = await userIdCommand.ExecuteScalarAsync(
        cancellationToken);

    if (result == null || result == DBNull.Value)
    {
        transaction.Rollback();
        return false;
    }

    userId = Convert.ToInt64(result);
}

        using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.BindByName = true;

        command.CommandText = $@"
            UPDATE {_schema}.APP_USERS
            SET
                DISPLAY_NAME = :DisplayName,
                UPDATED_BY = :UpdatedBy,
                UPDATED_AT = SYSTIMESTAMP
            WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)";

        command.Parameters.Add(
            "DisplayName",
            OracleDbType.Varchar2).Value = displayName;

        command.Parameters.Add(
            "UpdatedBy",
            OracleDbType.Varchar2).Value = updatedBy;

        command.Parameters.Add(
            "Username",
            OracleDbType.Varchar2).Value = username;

        var rowsUpdated = await command.ExecuteNonQueryAsync(
            cancellationToken);

        if (rowsUpdated > 0)
        {
            using var auditCommand = connection.CreateCommand();

            auditCommand.Transaction = transaction;
            auditCommand.BindByName = true;

            auditCommand.CommandText = $@"
                INSERT INTO {_schema}.APP_AUDIT_LOGS
                (
                    ACTOR,
                    ACTION,
                    ENTITY_TYPE,
                    ENTITY_ID,
                    OUTCOME,
                    DETAILS,
                    CREATED_AT
                )
                VALUES
                (
                    :Actor,
                    :Action,
                    :EntityType,
                    :EntityId,
                    :Outcome,
                    :Details,
                    SYSTIMESTAMP
                )";

            auditCommand.Parameters.Add(
                "Actor",
                OracleDbType.Varchar2).Value = updatedBy;

            auditCommand.Parameters.Add(
                "Action",
                OracleDbType.Varchar2).Value = "UPDATE_USER";

            auditCommand.Parameters.Add(
                "EntityType",
                OracleDbType.Varchar2).Value = "USER";

            auditCommand.Parameters.Add(
                 "EntityId",
                OracleDbType.Varchar2).Value =
                userId.ToString(CultureInfo.InvariantCulture);

            auditCommand.Parameters.Add(
                "Outcome",
                OracleDbType.Varchar2).Value = "SUCCESS";

            auditCommand.Parameters.Add(
                "Details",
                OracleDbType.Varchar2).Value =
                    $"Display name updated for user {username}.";

            await auditCommand.ExecuteNonQueryAsync(
                cancellationToken);
        }

        transaction.Commit();

        return rowsUpdated > 0;
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

public async Task<bool> DeactivateUserAsync(
    string username,
    DeactivateUserRequest request,
    string updatedBy,
    CancellationToken cancellationToken = default)
{
    if (request == null)
{
    throw new ArgumentNullException(nameof(request));
}

username = username?.Trim().ToLowerInvariant() ?? string.Empty;
var reason = request.Reason?.Trim();

    if (string.IsNullOrWhiteSpace(username) ||
        username.Contains('\\') ||
        username.Contains('@'))
    {
        throw new ArgumentException(
            "Provide a valid AD username without a domain prefix.");
    }

    if (string.IsNullOrWhiteSpace(reason) ||
    reason.Length < 3 ||
    reason.Length > 500)
    {
        throw new ArgumentException(
            "Deactivation reason must contain between 3 and 500 characters.");
    }

    if (string.IsNullOrWhiteSpace(updatedBy))
    {
        throw new ArgumentException(
            "The deactivation actor must be identified.");
    }

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var transaction = connection.BeginTransaction();
    bool transactionRolledBack = false;

    try
    {

	long userId;

using (var userIdCommand = connection.CreateCommand())
{
    userIdCommand.Transaction = transaction;
    userIdCommand.BindByName = true;

    userIdCommand.CommandText = $@"
        SELECT USER_ID
        FROM {_schema}.APP_USERS
        WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)
          AND IS_ACTIVE = 'Y'
        FOR UPDATE";

    userIdCommand.Parameters.Add(
        "Username",
        OracleDbType.Varchar2).Value = username;

    var result = await userIdCommand.ExecuteScalarAsync(
        cancellationToken);

    if (result == null || result == DBNull.Value)
{
    transaction.Rollback();
    transactionRolledBack = true;

    await LogAuditAsync(
        updatedBy.Trim(),
        "DEACTIVATE_USER",
        "FAILURE",
        "USER",
        username,
        $"User {username} deactivation rejected because the user does not exist or is already inactive.",
        cancellationToken: cancellationToken);

    return false;
}

    userId = Convert.ToInt64(result);
}

        using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.BindByName = true;

        command.CommandText = $@"
            UPDATE {_schema}.APP_USERS
            SET
                IS_ACTIVE = 'N',
                DISABLED_DATE = SYSTIMESTAMP,
                DISABLED_REASON = :Reason,
                UPDATED_BY = :UpdatedBy,
                UPDATED_AT = SYSTIMESTAMP
            WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)
              AND IS_ACTIVE = 'Y'";

        command.Parameters.Add(
            "Reason",
            OracleDbType.Varchar2).Value = reason;

        command.Parameters.Add(
            "UpdatedBy",
            OracleDbType.Varchar2).Value = updatedBy;

        command.Parameters.Add(
            "Username",
            OracleDbType.Varchar2).Value = username;

        var rowsUpdated = await command.ExecuteNonQueryAsync(
            cancellationToken);

        if (rowsUpdated > 0)
        {
            using var auditCommand = connection.CreateCommand();

            auditCommand.Transaction = transaction;
            auditCommand.BindByName = true;

            auditCommand.CommandText = $@"
                INSERT INTO {_schema}.APP_AUDIT_LOGS
                (
                    ACTOR,
                    ACTION,
                    ENTITY_TYPE,
                    ENTITY_ID,
                    OUTCOME,
                    DETAILS,
                    CREATED_AT
                )
                VALUES
                (
                    :Actor,
                    :Action,
                    :EntityType,
                    :EntityId,
                    :Outcome,
                    :Details,
                    SYSTIMESTAMP
                )";

            auditCommand.Parameters.Add(
                "Actor",
                OracleDbType.Varchar2).Value = updatedBy;

            auditCommand.Parameters.Add(
                "Action",
                OracleDbType.Varchar2).Value = "DEACTIVATE_USER";

            auditCommand.Parameters.Add(
                "EntityType",
                OracleDbType.Varchar2).Value = "USER";

            auditCommand.Parameters.Add(
                "EntityId",
                OracleDbType.Varchar2).Value =
                userId.ToString(CultureInfo.InvariantCulture);

            auditCommand.Parameters.Add(
                "Outcome",
                OracleDbType.Varchar2).Value = "SUCCESS";

            auditCommand.Parameters.Add(
                "Details",
                OracleDbType.Varchar2).Value =
                    $"User {username} deactivated. Reason: {reason}";

            await auditCommand.ExecuteNonQueryAsync(
                cancellationToken);

            _logger.LogInformation(
                "User {Username} deactivated by {Actor}.",
                username,
                updatedBy);
        }

        transaction.Commit();

        return rowsUpdated > 0;
    }
    catch
{
    if (!transactionRolledBack)
        transaction.Rollback();

    throw;
}
}


public async Task<bool> ReactivateUserAsync(
    string username,
    ReactivateUserRequest request,
    string updatedBy,
    CancellationToken cancellationToken = default)
{
    username = username.Trim().ToLowerInvariant();
    var reason = request.Reason?.Trim();

    if (string.IsNullOrWhiteSpace(username) ||
        username.Contains('\\') ||
        username.Contains('@'))
    {
        throw new ArgumentException(
            "Provide a valid AD username without a domain prefix.");
    }

    if (reason?.Length > 500)
    {
        throw new ArgumentException(
            "Reactivation reason cannot exceed 500 characters.");
    }

    if (string.IsNullOrWhiteSpace(updatedBy))
    {
        throw new ArgumentException(
            "The reactivation actor must be identified.");
    }

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var transaction = connection.BeginTransaction();

    bool transactionRolledBack = false;

    try
    {

	long userId;

using (var userIdCommand = connection.CreateCommand())
{
    userIdCommand.Transaction = transaction;
    userIdCommand.BindByName = true;

    userIdCommand.CommandText = $@"
        SELECT USER_ID
        FROM {_schema}.APP_USERS
        WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)
          AND IS_ACTIVE = 'N'
        FOR UPDATE";

    userIdCommand.Parameters.Add(
        "Username",
        OracleDbType.Varchar2).Value = username;

    var result = await userIdCommand.ExecuteScalarAsync(
        cancellationToken);

if (result == null || result == DBNull.Value)
{
    transaction.Rollback();
    transactionRolledBack = true;

    await LogAuditAsync(
        updatedBy.Trim(),
        "REACTIVATE_USER",
        "FAILURE",
        "USER",
        username,
        $"User {username} reactivation rejected because the user does not exist or is already active.",
        cancellationToken: cancellationToken);

    return false;
}
    userId = Convert.ToInt64(result);
}
        using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.BindByName = true;

        command.CommandText = $@"
            UPDATE {_schema}.APP_USERS
            SET
                IS_ACTIVE = 'Y',
                DISABLED_DATE = NULL,
                DISABLED_REASON = NULL,
                UPDATED_BY = :UpdatedBy,
                UPDATED_AT = SYSTIMESTAMP
            WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)
              AND IS_ACTIVE = 'N'";

        command.Parameters.Add(
            "UpdatedBy",
            OracleDbType.Varchar2).Value = updatedBy;

        command.Parameters.Add(
            "Username",
            OracleDbType.Varchar2).Value = username;

        var rowsUpdated = await command.ExecuteNonQueryAsync(
            cancellationToken);

        if (rowsUpdated > 0)
        {
            using var auditCommand = connection.CreateCommand();

            auditCommand.Transaction = transaction;
            auditCommand.BindByName = true;

            auditCommand.CommandText = $@"
                INSERT INTO {_schema}.APP_AUDIT_LOGS
                (
                    ACTOR,
                    ACTION,
                    ENTITY_TYPE,
                    ENTITY_ID,
                    OUTCOME,
                    DETAILS,
                    CREATED_AT
                )
                VALUES
                (
                    :Actor,
                    :Action,
                    :EntityType,
                    :EntityId,
                    :Outcome,
                    :Details,
                    SYSTIMESTAMP
                )";

            auditCommand.Parameters.Add(
                "Actor",
                OracleDbType.Varchar2).Value = updatedBy;

            auditCommand.Parameters.Add(
                "Action",
                OracleDbType.Varchar2).Value = "REACTIVATE_USER";

            auditCommand.Parameters.Add(
                "EntityType",
                OracleDbType.Varchar2).Value = "USER";

            auditCommand.Parameters.Add(
                "EntityId",
                OracleDbType.Varchar2).Value =
                userId.ToString(CultureInfo.InvariantCulture);

            auditCommand.Parameters.Add(
                "Outcome",
                OracleDbType.Varchar2).Value = "SUCCESS";

            auditCommand.Parameters.Add(
                "Details",
                OracleDbType.Varchar2).Value =
                    $"User {username} reactivated. Reason: " +
                    (string.IsNullOrWhiteSpace(reason)
                        ? "Not provided"
                        : reason);

            await auditCommand.ExecuteNonQueryAsync(
                cancellationToken);

            _logger.LogInformation(
                "User {Username} reactivated by {Actor}. Reason: {Reason}",
                username,
                updatedBy,
                string.IsNullOrWhiteSpace(reason)
                    ? "Not provided"
                    : reason);
        }

        transaction.Commit();

        return rowsUpdated > 0;
    }
    catch
{
    if (!transactionRolledBack)
        transaction.Rollback();

    throw;
}
}

public async Task<List<RoleDetailsResponse>> GetAllRolesAsync(
    CancellationToken cancellationToken = default)
{
    var roles = new List<RoleDetailsResponse>();

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var command = connection.CreateCommand();

    command.CommandText = $@"
        SELECT
            R.ROLE_ID,
            R.ROLE_NAME,
            R.DESCRIPTION,
            R.CREATED_AT,
            (
                SELECT COUNT(*)
                FROM {_schema}.APP_USER_ROLES UR
                WHERE UR.ROLE_ID = R.ROLE_ID
            ) AS ASSIGNED_USERS_COUNT
        FROM {_schema}.APP_ROLES R
        ORDER BY R.ROLE_NAME";

    using var reader = await command.ExecuteReaderAsync(
        cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        roles.Add(new RoleDetailsResponse
        {
            RoleId = Convert.ToInt64(reader["ROLE_ID"]),

            RoleName = reader["ROLE_NAME"].ToString()!,

            Description = reader["DESCRIPTION"] == DBNull.Value
                ? null
                : reader["DESCRIPTION"].ToString(),

            CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),

            AssignedUsersCount = Convert.ToInt32(
                reader["ASSIGNED_USERS_COUNT"])
        });
    }

    return roles;
}




public async Task<bool> CreateRoleAsync(
    CreateRoleRequest request,
    string createdBy,
    CancellationToken cancellationToken = default)
{
    var roleName = request.RoleName.Trim().ToUpperInvariant();
    var description = request.Description?.Trim();

    if (string.IsNullOrWhiteSpace(roleName))
        throw new ArgumentException("Role name is required.");

    if (string.IsNullOrWhiteSpace(createdBy))
        throw new ArgumentException(
            "The creating administrator must be identified.");

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var transaction = connection.BeginTransaction();

    try
    {
        long newRoleId;

        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.BindByName = true;

            checkCommand.CommandText = $@"
                SELECT COUNT(*)
                FROM {_schema}.APP_ROLES
                WHERE UPPER(TRIM(ROLE_NAME)) = :RoleName";

            checkCommand.Parameters.Add(
                "RoleName",
                OracleDbType.Varchar2,
                50).Value = roleName;

            var existingRoleCount = Convert.ToInt32(
                await checkCommand.ExecuteScalarAsync(
                    cancellationToken));

            if (existingRoleCount > 0)
{
    transaction.Rollback();

    await LogAuditAsync(
        createdBy.Trim(),
        "CREATE_ROLE",
        "FAILURE",
        "ROLE",
        roleName,
        $"Role {roleName} creation rejected because it already exists.",
        cancellationToken: cancellationToken);

    _logger.LogWarning(
        "Role creation rejected. Role {RoleName} already exists. Requested by {Actor}.",
        roleName,
        createdBy);

    return false;
}
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                INSERT INTO {_schema}.APP_ROLES
                (
                    ROLE_NAME,
                    DESCRIPTION,
                    CREATED_AT
                )
                VALUES
                (
                    :RoleName,
                    :Description,
                    SYSTIMESTAMP
                )
                RETURNING ROLE_ID INTO :NewRoleId";

            command.Parameters.Add(
                "RoleName",
                OracleDbType.Varchar2,
                50).Value = roleName;

            command.Parameters.Add(
                "Description",
                OracleDbType.Varchar2,
                250).Value =
                    string.IsNullOrWhiteSpace(description)
                        ? DBNull.Value
                        : description;

            var roleIdParameter = command.Parameters.Add(
                "NewRoleId",
                OracleDbType.Int64);

            roleIdParameter.Direction =
                System.Data.ParameterDirection.Output;

            await command.ExecuteNonQueryAsync(cancellationToken);

            newRoleId = Convert.ToInt64(
                roleIdParameter.Value.ToString());
        }

        using (var auditCommand = connection.CreateCommand())
        {
            auditCommand.Transaction = transaction;
            auditCommand.BindByName = true;

            auditCommand.CommandText = $@"
                INSERT INTO {_schema}.APP_AUDIT_LOGS
                (
                    ACTOR,
                    ACTION,
                    ENTITY_TYPE,
                    ENTITY_ID,
                    OUTCOME,
                    DETAILS,
                    CREATED_AT
                )
                VALUES
                (
                    :Actor,
                    :Action,
                    :EntityType,
                    :EntityId,
                    :Outcome,
                    :Details,
                    SYSTIMESTAMP
                )";

            auditCommand.Parameters.Add(
                "Actor",
                OracleDbType.Varchar2).Value = createdBy;

            auditCommand.Parameters.Add(
                "Action",
                OracleDbType.Varchar2).Value = "CREATE_ROLE";

            auditCommand.Parameters.Add(
                "EntityType",
                OracleDbType.Varchar2).Value = "ROLE";

            auditCommand.Parameters.Add(
                "EntityId",
                OracleDbType.Varchar2).Value =
                    newRoleId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);

            auditCommand.Parameters.Add(
                "Outcome",
                OracleDbType.Varchar2).Value = "SUCCESS";

            auditCommand.Parameters.Add(
                "Details",
                OracleDbType.Varchar2).Value =
                    $"Role {roleName} created. Description: " +
                    (string.IsNullOrWhiteSpace(description)
                        ? "Not provided"
                        : description);

            await auditCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();

        _logger.LogInformation(
            "Role {RoleName} created by {Actor}.",
            roleName,
            createdBy);

        return true;
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}



public async Task<string> AssignRoleAsync(
    string username,
    AssignRoleRequest request,
    string assignedBy,
    CancellationToken cancellationToken = default)
{
    username = username.Trim().ToLowerInvariant();
    var roleName = request.RoleName.Trim().ToUpperInvariant();

    if (string.IsNullOrWhiteSpace(username) ||
        username.Contains('\\') ||
        username.Contains('@'))
    {
        throw new ArgumentException("Invalid AD username.");
    }

    if (string.IsNullOrWhiteSpace(roleName))
        throw new ArgumentException("Role name is required.");

    if (string.IsNullOrWhiteSpace(assignedBy))
        throw new ArgumentException(
            "The assigning administrator must be identified.");

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var transaction = connection.BeginTransaction();


    bool transactionRolledBack = false;

    try
    {
        long userId;

        // 1. Find and lock the target user.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT USER_ID
                FROM {_schema}.APP_USERS
                WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)
                FOR UPDATE";

            command.Parameters.Add(
                "Username",
                OracleDbType.Varchar2).Value = username;

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            if (result == null || result == DBNull.Value)
{
    transaction.Rollback();
    transactionRolledBack = true;

    await LogAuditAsync(
        assignedBy.Trim(),
        "ASSIGN_ROLE",
        "FAILURE",
        "USER",
        username,
        $"Role {roleName} assignment rejected because user {username} does not exist.",
        cancellationToken: cancellationToken);

    return "USER_NOT_FOUND";
}

            userId = Convert.ToInt64(result);
        }

	// Record rejected role-assignment attempts outside the business transaction.
async Task<string> RejectAssignmentAsync(
    string result,
    string details)
{
    transaction.Rollback();

    transactionRolledBack = true;

    await LogAuditAsync(
        assignedBy.Trim(),
        "ASSIGN_ROLE",
        "FAILURE",
        "USER",
        userId.ToString(CultureInfo.InvariantCulture),
        details,
        cancellationToken: cancellationToken);

    return result;
}

        // 2. Confirm that the account is active.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT IS_ACTIVE
                FROM {_schema}.APP_USERS
                WHERE USER_ID = :UserId";

            command.Parameters.Add(
                "UserId",
                OracleDbType.Int64).Value = userId;

            var activeStatus = Convert.ToString(
                await command.ExecuteScalarAsync(cancellationToken));

            if (!string.Equals(
                activeStatus?.Trim(),
                "Y",
                StringComparison.OrdinalIgnoreCase))
            {
                await RejectAssignmentAsync(
              "INACTIVE_USER",
              $"Role {roleName} assignment rejected because user {username} is inactive.");

              throw new ArgumentException(
                  "A role cannot be assigned to an inactive user.");
            }
        }

        // 3. Find the requested role.
        long roleId;

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT ROLE_ID
                FROM {_schema}.APP_ROLES
                WHERE UPPER(TRIM(ROLE_NAME)) = :RoleName";

            command.Parameters.Add(
                "RoleName",
                OracleDbType.Varchar2).Value = roleName;

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            if (result == null || result == DBNull.Value)
            {
               return await RejectAssignmentAsync(
               "ROLE_NOT_FOUND",
               $"Role {roleName} does not exist. Assignment requested for user {username}.");
            }

            roleId = Convert.ToInt64(result);
        }

        // 4. Serialize ADMIN role assignments and removals.
        if (roleName == "ADMIN")
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT ROLE_ID
                FROM {_schema}.APP_ROLES
                WHERE ROLE_ID = :RoleId
                FOR UPDATE";

            command.Parameters.Add(
                "RoleId",
                OracleDbType.Int64).Value = roleId;

            await command.ExecuteScalarAsync(cancellationToken);
        }

        // 5. Check for an existing assignment.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT COUNT(*)
                FROM {_schema}.APP_USER_ROLES
                WHERE USER_ID = :UserId
                  AND ROLE_ID = :RoleId";

            command.Parameters.Add(
                "UserId",
                OracleDbType.Int64).Value = userId;

            command.Parameters.Add(
                "RoleId",
                OracleDbType.Int64).Value = roleId;

            var count = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken));

            if (count > 0)
            {
                return await RejectAssignmentAsync(
                "ALREADY_ASSIGNED",
                $"Role {roleName} is already assigned to user {username}.");
            }
        }

        // 6. Insert the role assignment.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                INSERT INTO {_schema}.APP_USER_ROLES
                (
                    USER_ID,
                    ROLE_ID,
                    ASSIGNED_BY,
                    ASSIGNED_AT
                )
                VALUES
                (
                    :UserId,
                    :RoleId,
                    :AssignedBy,
                    SYSTIMESTAMP
                )";

            command.Parameters.Add(
                "UserId",
                OracleDbType.Int64).Value = userId;

            command.Parameters.Add(
                "RoleId",
                OracleDbType.Int64).Value = roleId;

            command.Parameters.Add(
                "AssignedBy",
                OracleDbType.Varchar2,
                256).Value = assignedBy.Trim();

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // 7. Record the assignment in the same transaction.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                INSERT INTO {_schema}.APP_AUDIT_LOGS
                (
                    ACTOR,
                    ACTION,
                    ENTITY_TYPE,
                    ENTITY_ID,
                    OUTCOME,
                    DETAILS,
                    CREATED_AT
                )
                VALUES
                (
                    :Actor,
                    'ASSIGN_ROLE',
                    'USER',
                    :EntityId,
                    'SUCCESS',
                    :Details,
                    SYSTIMESTAMP
                )";

            command.Parameters.Add(
                "Actor",
                OracleDbType.Varchar2,
                100).Value = assignedBy.Trim();

            command.Parameters.Add(
                "EntityId",
                OracleDbType.Varchar2,
                100).Value = userId.ToString();

            command.Parameters.Add(
                "Details",
                OracleDbType.Varchar2,
                2000).Value =
                    $"Role {roleName} assigned to user {username}.";

            await command.ExecuteNonQueryAsync(cancellationToken);
        }


        transaction.Commit();

        _logger.LogInformation(
            "Role {RoleName} assigned to {Username} by {Actor}.",
            roleName,
            username,
            assignedBy);

        return "ASSIGNED";
    }
    catch
{
    if (!transactionRolledBack)
        transaction.Rollback();

    throw;
}
}

public async Task<string> RemoveRoleAsync(
    string username,
    string roleName,
    string removedBy,
    CancellationToken cancellationToken = default)
{
    username = username.Trim().ToLowerInvariant();
    roleName = roleName.Trim().ToUpperInvariant();

    if (string.IsNullOrWhiteSpace(username) ||
        username.Contains('\\') ||
        username.Contains('@'))
    {
        throw new ArgumentException("Invalid AD username.");
    }

    if (string.IsNullOrWhiteSpace(removedBy))
    {
        throw new ArgumentException("The removing administrator must be identified.");
    }

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync(cancellationToken);

    using var transaction = connection.BeginTransaction();

    bool transactionRolledBack = false;

    try
    {
        long userId;

        // 1. Find and lock the target user.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT USER_ID
                FROM {_schema}.APP_USERS
                WHERE UPPER(TRIM(WINDOWS_USERNAME)) = UPPER(:Username)
                FOR UPDATE";

            command.Parameters.Add(
                "Username",
                OracleDbType.Varchar2).Value = username;

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            if (result == null || result == DBNull.Value)
{
    transaction.Rollback();

    await LogAuditAsync(
        removedBy.Trim(),
        "REMOVE_ROLE",
        "FAILURE",
        "USER",
        username,
        $"Role {roleName} removal rejected because user {username} does not exist.",
        cancellationToken: cancellationToken);

    return "USER_NOT_FOUND";
}

            userId = Convert.ToInt64(result);
        }

	// Record rejected role-removal attempts outside the business transaction.
async Task<string> RejectRemovalAsync(
    string result,
    string details)
{
    transaction.Rollback();
    transactionRolledBack = true;

    await LogAuditAsync(
        removedBy.Trim(),
        "REMOVE_ROLE",
        "FAILURE",
        "USER",
        userId.ToString(CultureInfo.InvariantCulture),
        details,
        cancellationToken: cancellationToken);

    return result;
}

        // 2. Find the requested role.
        long roleId;

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT ROLE_ID
                FROM {_schema}.APP_ROLES
                WHERE UPPER(TRIM(ROLE_NAME)) = :RoleName";

            command.Parameters.Add(
                "RoleName",
                OracleDbType.Varchar2).Value = roleName;

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            if (result == null || result == DBNull.Value)
            {
                return await RejectRemovalAsync(
                "ROLE_NOT_FOUND",
                 $"Role {roleName} does not exist. Removal requested for user {username}.");
            }

            roleId = Convert.ToInt64(result);
        }

        // 3. Check whether the role is assigned.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT COUNT(*)
                FROM {_schema}.APP_USER_ROLES
                WHERE USER_ID = :UserId
                  AND ROLE_ID = :RoleId";

            command.Parameters.Add(
                "UserId",
                OracleDbType.Int64).Value = userId;

            command.Parameters.Add(
                "RoleId",
                OracleDbType.Int64).Value = roleId;

            var count = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken));

            if (count == 0)
            {
                return await RejectRemovalAsync(
                "ROLE_NOT_ASSIGNED",
                 $"Role {roleName} is not assigned to user {username}.");
            }
        }


	// 5. Protect the last active ADMIN account.
        if (roleName == "ADMIN")
        {
            // Lock the ADMIN role row to serialize admin removals.
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.BindByName = true;

                command.CommandText = $@"
                    SELECT ROLE_ID
                    FROM {_schema}.APP_ROLES
                    WHERE ROLE_ID = :RoleId
                    FOR UPDATE";

                command.Parameters.Add(
                    "RoleId",
                    OracleDbType.Int64).Value = roleId;

                await command.ExecuteScalarAsync(cancellationToken);
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.BindByName = true;

                command.CommandText = $@"
                    SELECT COUNT(DISTINCT U.USER_ID)
                    FROM {_schema}.APP_USERS U
                    INNER JOIN {_schema}.APP_USER_ROLES UR
                        ON UR.USER_ID = U.USER_ID
                    WHERE U.IS_ACTIVE = 'Y'
                      AND UR.ROLE_ID = :RoleId";

                command.Parameters.Add(
                    "RoleId",
                    OracleDbType.Int64).Value = roleId;

                var adminCount = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken));

                if (adminCount <= 1)
                {
                   return await RejectRemovalAsync(
                   "LAST_ADMIN",
                    $"Removal of the last active ADMIN role from user {username} was rejected.");
                }
            }
        }

        // 4. Prevent removal of the user's final role.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                SELECT COUNT(*)
                FROM {_schema}.APP_USER_ROLES
                WHERE USER_ID = :UserId";

            command.Parameters.Add(
                "UserId",
                OracleDbType.Int64).Value = userId;

            var count = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken));

            if (count <= 1)
            {
                return await RejectRemovalAsync(
                "LAST_ROLE",
                $"Removal of the final application role {roleName} from user {username} was rejected.");
            }
        }


        // 6. Remove the role assignment.
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.CommandText = $@"
                DELETE FROM {_schema}.APP_USER_ROLES
                WHERE USER_ID = :UserId
                  AND ROLE_ID = :RoleId";

            command.Parameters.Add(
                "UserId",
                OracleDbType.Int64).Value = userId;

            command.Parameters.Add(
                "RoleId",
                OracleDbType.Int64).Value = roleId;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

	        // Record successful role removal in the audit log.
        using (var auditCommand = connection.CreateCommand())
        {
            auditCommand.Transaction = transaction;
            auditCommand.BindByName = true;

            auditCommand.CommandText = $@"
                INSERT INTO {_schema}.APP_AUDIT_LOGS
                (
                    ACTOR,
                    ACTION,
                    ENTITY_TYPE,
                    ENTITY_ID,
                    OUTCOME,
                    DETAILS,
                    CREATED_AT
                )
                VALUES
                (
                    :Actor,
                    'REMOVE_ROLE',
                    'USER',
                    :EntityId,
                    'SUCCESS',
                    :Details,
                    SYSTIMESTAMP
                )";

            auditCommand.Parameters.Add(
                "Actor",
                OracleDbType.Varchar2,
                100).Value = removedBy.Trim();

            auditCommand.Parameters.Add(
                "EntityId",
                OracleDbType.Varchar2,
                100).Value = userId.ToString();

            auditCommand.Parameters.Add(
                "Details",
                OracleDbType.Varchar2,
                2000).Value =
                    $"Role {roleName} removed from user {username}.";

            await auditCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();


        _logger.LogInformation(
            "Role {RoleName} removed from {Username} by {Actor}.",
            roleName,
            username,
            removedBy);

        return "REMOVED";
    }
    catch
{
    if (!transactionRolledBack)
        transaction.Rollback();

    throw;
}
}


public async Task LogAuditAsync(
    string actor,
    string action,
    string outcome,
    string? entityType = null,
    string? entityId = null,
    string? details = null,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(actor))
        throw new ArgumentException("Audit actor is required.");

    if (string.IsNullOrWhiteSpace(action))
        throw new ArgumentException("Audit action is required.");

    if (string.IsNullOrWhiteSpace(outcome))
        throw new ArgumentException("Audit outcome is required.");

    await using var connection = _connectionFactory.CreateConnection();
    await connection.OpenAsync(cancellationToken);

    using var command = connection.CreateCommand();
    command.BindByName = true;

    command.CommandText = $@"
        INSERT INTO {_schema}.APP_AUDIT_LOGS
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
            :Action,
            :EntityType,
            :EntityId,
            :Outcome,
            :Details,
            :IpAddress,
            SYSTIMESTAMP
        )";

    command.Parameters.Add("Actor", OracleDbType.Varchar2).Value =
        actor.Trim();

    command.Parameters.Add("Action", OracleDbType.Varchar2).Value =
        action.Trim();

    command.Parameters.Add("EntityType", OracleDbType.Varchar2).Value =
        (object?)entityType ?? DBNull.Value;

    command.Parameters.Add("EntityId", OracleDbType.Varchar2).Value =
        (object?)entityId ?? DBNull.Value;

    command.Parameters.Add("Outcome", OracleDbType.Varchar2).Value =
        outcome.Trim().ToUpperInvariant();

    command.Parameters.Add("Details", OracleDbType.Varchar2).Value =
        (object?)details ?? DBNull.Value;

    command.Parameters.Add("IpAddress", OracleDbType.Varchar2).Value =
        (object?)ipAddress ?? DBNull.Value;

    await command.ExecuteNonQueryAsync(cancellationToken);

    _logger.LogInformation(
        "Audit event recorded: {Action} by {Actor}, outcome {Outcome}.",
        action,
        actor,
        outcome);
}

public async Task<List<AuditLogResponse>> GetAuditLogsAsync(
    string? actor = null,
    string? action = null,
    string? outcome = null,
    DateTime? fromDate = null,
    DateTime? toDate = null,
    CancellationToken cancellationToken = default)
{
    var logs = new List<AuditLogResponse>();

    await using var connection = _connectionFactory.CreateConnection();
    await connection.OpenAsync(cancellationToken);


    using (var diagnosticCommand = connection.CreateCommand())
{
    diagnosticCommand.CommandText = @"
        SELECT
            SYS_CONTEXT('USERENV', 'SESSION_USER')
            || ' | ' ||
            SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
            || ' | ' ||
            SYS_CONTEXT('USERENV', 'SERVICE_NAME')
        FROM DUAL";

    var sessionInfo = await diagnosticCommand.ExecuteScalarAsync(
        cancellationToken);

    _logger.LogInformation(
        "AUDIT DATABASE SESSION: {SessionInfo}",
        sessionInfo);
}

    using var command = connection.CreateCommand();
    command.BindByName = true;

    var conditions = new List<string>();

if (!string.IsNullOrWhiteSpace(actor))
    conditions.Add("UPPER(ACTOR) LIKE :Actor");

if (!string.IsNullOrWhiteSpace(action))
    conditions.Add("UPPER(ACTION) = :Action");

if (!string.IsNullOrWhiteSpace(outcome))
    conditions.Add("UPPER(OUTCOME) = :Outcome");

if (fromDate.HasValue)
    conditions.Add("CREATED_AT >= :FromDate");

if (toDate.HasValue)
    conditions.Add("CREATED_AT < :ToDate");

command.CommandText = $@"
    SELECT
        AUDIT_ID,
        ACTOR,
        ACTION,
        ENTITY_TYPE,
        ENTITY_ID,
        OUTCOME,
        DETAILS,
        IP_ADDRESS,
        CREATED_AT
    FROM {_schema}.APP_AUDIT_LOGS
    {(conditions.Count > 0
        ? "WHERE " + string.Join(" AND ", conditions)
        : "")}
    ORDER BY CREATED_AT DESC, AUDIT_ID DESC";

if (!string.IsNullOrWhiteSpace(actor))
{
    command.Parameters.Add(
        "Actor",
        OracleDbType.Varchar2).Value =
        "%" + actor.Trim().ToUpperInvariant() + "%";
}

if (!string.IsNullOrWhiteSpace(action))
{
    command.Parameters.Add(
        "Action",
        OracleDbType.Varchar2).Value =
        action.Trim().ToUpperInvariant();
}

if (!string.IsNullOrWhiteSpace(outcome))
{
    command.Parameters.Add(
        "Outcome",
        OracleDbType.Varchar2).Value =
        outcome.Trim().ToUpperInvariant();
}

if (fromDate.HasValue)
{
    command.Parameters.Add(
        "FromDate",
        OracleDbType.TimeStamp).Value = fromDate.Value;
}

if (toDate.HasValue)
{
    command.Parameters.Add(
        "ToDate",
        OracleDbType.TimeStamp).Value = toDate.Value;
}


    _logger.LogInformation(
    "AUDIT FILTERS: Actor={Actor}, Action={Action}, Outcome={Outcome}, FromDate={FromDate}, ToDate={ToDate}",
    actor ?? "<NULL>",
    action ?? "<NULL>",
    outcome ?? "<NULL>",
    fromDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "<NULL>",
    toDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "<NULL>");

_logger.LogInformation(
    "AUDIT SQL: {Sql}",
    command.CommandText);

_logger.LogInformation(
    "AUDIT PARAMETER COUNT: {Count}",
    command.Parameters.Count);


    using var reader = await command.ExecuteReaderAsync(cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        logs.Add(new AuditLogResponse
        {
            AuditId = Convert.ToInt64(reader["AUDIT_ID"]),
            Actor = reader["ACTOR"].ToString() ?? string.Empty,
            Action = reader["ACTION"].ToString() ?? string.Empty,
            EntityType = reader["ENTITY_TYPE"] == DBNull.Value
                ? null : reader["ENTITY_TYPE"].ToString(),
            EntityId = reader["ENTITY_ID"] == DBNull.Value
                ? null : reader["ENTITY_ID"].ToString(),
            Outcome = reader["OUTCOME"].ToString() ?? string.Empty,
            Details = reader["DETAILS"] == DBNull.Value
                ? null : reader["DETAILS"].ToString(),
            IpAddress = reader["IP_ADDRESS"] == DBNull.Value
                ? null : reader["IP_ADDRESS"].ToString(),
            CreatedAt = Convert.ToDateTime(reader["CREATED_AT"])
        });
    }

    return logs;
}
}
