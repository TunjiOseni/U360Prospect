using Oracle.ManagedDataAccess.Client;
using System.Data;
using U360Prospect.Models;

namespace U360Prospect.Repositories;

public class ProspectRepository
{
    private readonly string _connectionString;

    public ProspectRepository(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException(
                "Oracle connection string is not configured.");
    }


    // =========================================================
    // GET ALL PROSPECTS
    // =========================================================
    public async Task<PagedResult<Prospect>> GetAllAsync(
        string? searchTerm = null,
        string? country = null,
        string? region = null,
        string? prospectType = null,
        string? ubaCustomer = null,
        int pageNumber = 1,
        int pageSize = 20,
        string sortColumn = "ProspectName",
        string sortDirection = "asc")
    {
        var results = new PagedResult<Prospect>
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var whereConditions = new List<string>();
        var parameters = new List<OracleParameter>();


        // =====================================================
        // SEARCH
        // =====================================================
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            whereConditions.Add("""
                (
                    LOWER(COMPANY_NAME) LIKE LOWER(:SearchTerm)
                    OR LOWER(PROSPECT_NAME) LIKE LOWER(:SearchTerm)
                    OR LOWER(PROSPECT_TYPE) LIKE LOWER(:SearchTerm)
                    OR LOWER(INDUSTRY) LIKE LOWER(:SearchTerm)
                    OR LOWER(COUNTRY) LIKE LOWER(:SearchTerm)
                    OR LOWER(REGION) LIKE LOWER(:SearchTerm)
                )
                """);

            parameters.Add(
                new OracleParameter(
                    ":SearchTerm",
                    OracleDbType.Varchar2)
                {
                    Value = $"%{searchTerm.Trim()}%"
                });
        }


        // =====================================================
        // COUNTRY FILTER
        // =====================================================
        if (!string.IsNullOrWhiteSpace(country))
        {
            whereConditions.Add(
                "LOWER(COUNTRY) = LOWER(:Country)");

            parameters.Add(
                new OracleParameter(
                    ":Country",
                    OracleDbType.Varchar2)
                {
                    Value = country.Trim()
                });
        }


        // =====================================================
        // REGION FILTER
        // =====================================================
        if (!string.IsNullOrWhiteSpace(region))
        {
            whereConditions.Add(
                "LOWER(REGION) = LOWER(:Region)");

            parameters.Add(
                new OracleParameter(
                    ":Region",
                    OracleDbType.Varchar2)
                {
                    Value = region.Trim()
                });
        }


        // =====================================================
        // PROSPECT TYPE FILTER
        // =====================================================
        if (!string.IsNullOrWhiteSpace(prospectType))
        {
            whereConditions.Add(
                "LOWER(PROSPECT_TYPE) = LOWER(:ProspectType)");

            parameters.Add(
                new OracleParameter(
                    ":ProspectType",
                    OracleDbType.Varchar2)
                {
                    Value = prospectType.Trim()
                });
        }


        // =====================================================
        // UBA CUSTOMER FILTER
        // =====================================================
        if (!string.IsNullOrWhiteSpace(ubaCustomer))
        {
            whereConditions.Add(
                "LOWER(UBA_CUSTOMER) = LOWER(:UbaCustomer)");

            parameters.Add(
                new OracleParameter(
                    ":UbaCustomer",
                    OracleDbType.Varchar2)
                {
                    Value = ubaCustomer.Trim()
                });
        }


        var whereClause =
            whereConditions.Count > 0
                ? "WHERE " + string.Join(" AND ", whereConditions)
                : "";


        // =====================================================
        // SAFE SORTING
        // =====================================================
        var allowedSortColumns =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["CompanyName"] = "COMPANY_NAME",
                ["ProspectName"] = "PROSPECT_NAME",
                ["ProspectType"] = "PROSPECT_TYPE",
                ["Industry"] = "INDUSTRY",
                ["Country"] = "COUNTRY",
                ["Region"] = "REGION",
                ["State"] = "STATE",
                ["UbaCustomer"] = "UBA_CUSTOMER"
            };

        var orderColumn =
            allowedSortColumns.TryGetValue(
                sortColumn,
                out var mappedColumn)
                ? mappedColumn
                : "PROSPECT_NAME";

        var orderDirection =
            sortDirection.Equals(
                "desc",
                StringComparison.OrdinalIgnoreCase)
                ? "DESC"
                : "ASC";


        // =====================================================
        // COUNT
        // =====================================================
        var countSql = $"""
            SELECT COUNT(*)
            FROM BANKING.PROSPECT
            {whereClause}
            """;


        await using (var connection =
            new OracleConnection(_connectionString))
        {
            await connection.OpenAsync();

            await using var countCommand =
                new OracleCommand(countSql, connection);

            countCommand.BindByName = true;

            foreach (var parameter in parameters)
            {
                countCommand.Parameters.Add(
                    new OracleParameter(
                        parameter.ParameterName,
                        parameter.OracleDbType)
                    {
                        Value = parameter.Value
                    });
            }

            results.TotalCount =
                Convert.ToInt32(
                    await countCommand.ExecuteScalarAsync());
        }


        if (results.TotalCount == 0)
        {
            results.Items = new List<Prospect>();
            return results;
        }


        if (pageNumber < 1)
            pageNumber = 1;

        if (pageNumber > results.TotalPages)
            pageNumber = results.TotalPages;

        results.PageNumber = pageNumber;


        var offset =
            (pageNumber - 1) * pageSize;


        // =====================================================
        // SELECT
        // =====================================================
        var sql = $"""
            SELECT
                ROWID AS ROW_ID,
                COUNTRY,
                COMPANY_NAME,
                PROSPECT_NAME,
                PROSPECT_TYPE,
                ADDRESS,
                STATE,
                REGION,
                PHONE,
                EMAIL,
                INDUSTRY,
                UBA_CUSTOMER,
                MARKETED_BY,
                MARKETED_DATE,
                BRANCH_ID,
                ACQUISITION_STATUS,
                UPLOADED_DATE,
                CONVERTED_DATE,
                REMARKS
            FROM BANKING.PROSPECT
            {whereClause}
            ORDER BY {orderColumn} {orderDirection}
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY
            """;


        await using (var connection =
            new OracleConnection(_connectionString))
        {
            await connection.OpenAsync();

            await using var command =
                new OracleCommand(sql, connection);

            command.BindByName = true;

            foreach (var parameter in parameters)
            {
                command.Parameters.Add(
                    new OracleParameter(
                        parameter.ParameterName,
                        parameter.OracleDbType)
                    {
                        Value = parameter.Value
                    });
            }

            command.Parameters.Add(
                new OracleParameter(
                    ":Offset",
                    OracleDbType.Int32)
                {
                    Value = offset
                });

            command.Parameters.Add(
                new OracleParameter(
                    ":PageSize",
                    OracleDbType.Int32)
                {
                    Value = pageSize
                });


            await using var reader =
                await command.ExecuteReaderAsync();

            var items = new List<Prospect>();

            while (await reader.ReadAsync())
            {
                items.Add(MapProspect(reader));
            }

            results.Items = items;
        }


        return results;
    }


    // =========================================================
    // GET COUNTRIES
    // =========================================================
    public async Task<List<string>> GetCountriesAsync()
    {
        const string sql = """
            SELECT DISTINCT COUNTRY
            FROM BANKING.PROSPECT
            WHERE COUNTRY IS NOT NULL
              AND TRIM(COUNTRY) IS NOT NULL
            ORDER BY COUNTRY
            """;

        var countries = new List<string>();

        await using var connection =
            new OracleConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new OracleCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            countries.Add(reader.GetString(0));
        }

        return countries;
    }


    // =========================================================
    // GET REGIONS
    // =========================================================
    public async Task<List<string>> GetRegionsAsync(
        string? country = null)
    {
        var conditions = new List<string>
        {
            "REGION IS NOT NULL",
            "TRIM(REGION) IS NOT NULL"
        };

        var parameters = new List<OracleParameter>();

        if (!string.IsNullOrWhiteSpace(country))
        {
            conditions.Add(
                "LOWER(COUNTRY) = LOWER(:Country)");

            parameters.Add(
                new OracleParameter(
                    ":Country",
                    OracleDbType.Varchar2)
                {
                    Value = country.Trim()
                });
        }

        var sql = $"""
            SELECT DISTINCT REGION
            FROM BANKING.PROSPECT
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY REGION
            """;

        var regions = new List<string>();

        await using var connection =
            new OracleConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new OracleCommand(sql, connection);

        command.BindByName = true;

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            regions.Add(reader.GetString(0));
        }

        return regions;
    }


public async Task<List<string>> GetBranchesAsync(
    string? country = null,
    string? region = null)
{
    var whereConditions = new List<string>();
    var parameters = new List<OracleParameter>();

    if (!string.IsNullOrWhiteSpace(country))
    {
        whereConditions.Add(
            "LOWER(COUNTRY) = LOWER(:Country)");

        parameters.Add(
            new OracleParameter(
                ":Country",
                OracleDbType.Varchar2)
            {
                Value = country.Trim()
            });
    }

    if (!string.IsNullOrWhiteSpace(region))
    {
        whereConditions.Add(
            "LOWER(REGION) = LOWER(:Region)");

        parameters.Add(
            new OracleParameter(
                ":Region",
                OracleDbType.Varchar2)
            {
                Value = region.Trim()
            });
    }

    whereConditions.Add(
        "BRANCH_ID IS NOT NULL");

    whereConditions.Add(
        "TRIM(BRANCH_ID) IS NOT NULL");

    var whereClause =
        "WHERE " +
        string.Join(
            " AND ",
            whereConditions);

    var sql = $"""
        SELECT DISTINCT TRIM(BRANCH_ID) AS BRANCH_ID
        FROM BANKING.PROSPECT
        {whereClause}
        ORDER BY TRIM(BRANCH_ID)
        """;

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    foreach (var parameter in parameters)
    {
        command.Parameters.Add(parameter);
    }

    var branches = new List<string>();

    await using var reader =
        await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        if (!reader.IsDBNull(0))
        {
            branches.Add(
                reader.GetString(0).Trim());
        }
    }

    return branches;
}

    // =========================================================
    // GET PROSPECT TYPES
    // =========================================================
    public async Task<List<string>> GetProspectTypesAsync()
    {
        const string sql = """
            SELECT DISTINCT PROSPECT_TYPE
            FROM BANKING.PROSPECT
            WHERE PROSPECT_TYPE IS NOT NULL
              AND TRIM(PROSPECT_TYPE) IS NOT NULL
            ORDER BY PROSPECT_TYPE
            """;

        var prospectTypes = new List<string>();

        await using var connection =
            new OracleConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new OracleCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            prospectTypes.Add(reader.GetString(0));
        }

        return prospectTypes;
    }


    // =========================================================
    // GET BY ROWID
    // =========================================================
    public async Task<Prospect?> GetByIdAsync(string id)
    {
        const string sql = """
            SELECT
                ROWID AS ROW_ID,
                COUNTRY,
                COMPANY_NAME,
                PROSPECT_NAME,
                PROSPECT_TYPE,
                ADDRESS,
                STATE,
                REGION,
                PHONE,
                EMAIL,
                INDUSTRY,
                UBA_CUSTOMER,
                MARKETED_BY,
                MARKETED_DATE,
                BRANCH_ID,
                ACQUISITION_STATUS,
                UPLOADED_DATE,
                CONVERTED_DATE,
                REMARKS
            FROM BANKING.PROSPECT
            WHERE ROWID = CHARTOROWID(:Id)
            """;

        await using var connection =
            new OracleConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new OracleCommand(sql, connection);

        command.BindByName = true;

        command.Parameters.Add(
            new OracleParameter(
                ":Id",
                OracleDbType.Varchar2)
            {
                Value = id
            });

        await using var reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapProspect(reader);
        }

        return null;
    }


    // =========================================================
    // INSERT
    // =========================================================
    // =========================================================
// INSERT
// =========================================================
public async Task AddAsync(Prospect prospect)
{
    const string sql = """
        INSERT INTO BANKING.PROSPECT
        (
            COUNTRY,
            COMPANY_NAME,
            PROSPECT_NAME,
            PROSPECT_TYPE,
            ADDRESS,
            STATE,
            REGION,
            PHONE,
            EMAIL,
            INDUSTRY,
            UBA_CUSTOMER,
            MARKETED_BY,
            MARKETED_DATE,
            BRANCH_ID,
            ACQUISITION_STATUS,
            UPLOADED_DATE,
            CONVERTED_DATE,
            REMARKS
        )
        VALUES
        (
            :Country,
            :CompanyName,
            :ProspectName,
            :ProspectType,
            :Address,
            :State,
            :Region,
            :Phone,
            :Email,
            :Industry,
            :UbaCustomer,
            :MarketedBy,
            :MarketedDate,
            :BranchId,
            :AcquisitionStatus,
             SYSDATE,
            NULL,
            :Remarks
        )
        """;

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    AddStringParameter(
        command,
        ":Country",
        prospect.Country);

    AddStringParameter(
        command,
        ":CompanyName",
        prospect.CompanyName);

    AddStringParameter(
        command,
        ":ProspectName",
        prospect.ProspectName);

    AddStringParameter(
        command,
        ":ProspectType",
        prospect.ProspectType);

    AddStringParameter(
        command,
        ":Address",
        prospect.Address);

    AddStringParameter(
        command,
        ":State",
        prospect.State);

    AddStringParameter(
        command,
        ":Region",
        prospect.Region);

    AddStringParameter(
        command,
        ":Phone",
        prospect.Phone);

    AddStringParameter(
        command,
        ":Email",
        prospect.Email);

    AddStringParameter(
        command,
        ":Industry",
        prospect.Industry);

    AddStringParameter(command,
        ":BranchId",
        prospect.BranchId);

    // New prospects always start as non-customers.
    AddStringParameter(
        command,
        ":UbaCustomer",
        "No");

    AddStringParameter(
        command,
        ":MarketedBy",
        prospect.MarketedBy);

    if (prospect.MarketedDate.HasValue)
    {
        command.Parameters.Add(
            new OracleParameter(
                ":MarketedDate",
                OracleDbType.Date)
            {
                Value = prospect.MarketedDate.Value
            });
    }
    else
    {
        command.Parameters.Add(
            new OracleParameter(
                ":MarketedDate",
                OracleDbType.Date)
            {
                Value = DBNull.Value
            });
    }

    AddStringParameter(
        command,
        ":AcquisitionStatus",
        prospect.AcquisitionStatus ?? "New");

    AddStringParameter(
        command,
        ":Remarks",
        prospect.Remarks);

    await command.ExecuteNonQueryAsync();
}


    // =========================================================
    // UPDATE
    // =========================================================

    // =========================================================
// UPDATE
// =========================================================
public async Task UpdateAsync(Prospect prospect)
{
    const string sql = """
        UPDATE BANKING.PROSPECT
        SET
            COUNTRY = :Country,
            COMPANY_NAME = :CompanyName,
            PROSPECT_NAME = :ProspectName,
            PROSPECT_TYPE = :ProspectType,
            ADDRESS = :Address,
            STATE = :State,
            REGION = :Region,
            PHONE = :Phone,
            EMAIL = :Email,
            INDUSTRY = :Industry,
            MARKETED_BY = :MarketedBy,
            MARKETED_DATE = :MarketedDate,
            BRANCH_ID = :BranchId,
            ACQUISITION_STATUS = :AcquisitionStatus,
            REMARKS = :Remarks
        WHERE ROWID = CHARTOROWID(:Id)
        """;

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    AddStringParameter(
        command,
        ":Country",
        prospect.Country);

    AddStringParameter(
        command,
        ":CompanyName",
        prospect.CompanyName);

    AddStringParameter(
        command,
        ":ProspectName",
        prospect.ProspectName);

    AddStringParameter(
        command,
        ":ProspectType",
        prospect.ProspectType);

    AddStringParameter(
        command,
        ":Address",
        prospect.Address);

    AddStringParameter(
        command,
        ":State",
        prospect.State);

    AddStringParameter(
        command,
        ":Region",
        prospect.Region);

    AddStringParameter(
        command,
        ":Phone",
        prospect.Phone);

    AddStringParameter(
        command,
        ":Email",
        prospect.Email);

    AddStringParameter(
        command,
        ":Industry",
        prospect.Industry);

    AddStringParameter(
        command,
        ":MarketedBy",
        prospect.MarketedBy);

    if (prospect.MarketedDate.HasValue)
    {
        command.Parameters.Add(
            new OracleParameter(
                ":MarketedDate",
                OracleDbType.Date)
            {
                Value = prospect.MarketedDate.Value
            });
    }
    else
    {
        command.Parameters.Add(
            new OracleParameter(
                ":MarketedDate",
                OracleDbType.Date)
            {
                Value = DBNull.Value
            });
    }

    AddStringParameter(
        command,
        ":BranchId",
        prospect.BranchId);

    AddStringParameter(
        command,
        ":AcquisitionStatus",
        prospect.AcquisitionStatus ?? "New");

    AddStringParameter(
        command,
        ":Remarks",
        prospect.Remarks);

    AddStringParameter(
        command,
        ":Id",
        prospect.Id);

    await command.ExecuteNonQueryAsync();
}

    // =========================================================
    // CONVERT PROSPECT TO UBA CUSTOMER
    // =========================================================
    public async Task ConvertToCustomerAsync(string id)
{
    const string sql = """
        UPDATE BANKING.PROSPECT
        SET
            UBA_CUSTOMER = 'Yes',
            ACQUISITION_STATUS = 'Converted',
            CONVERTED_DATE = SYSDATE
        WHERE ROWID = CHARTOROWID(:Id)
        """;

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command = new OracleCommand(sql, connection);

    command.Parameters.Add(
        new OracleParameter(":Id", OracleDbType.Varchar2)
        {
            Value = id
        });

    await command.ExecuteNonQueryAsync();
}

    // =========================================================
    // DELETE
    // =========================================================
    public async Task DeleteAsync(string id)
    {
        const string sql = """
            DELETE FROM BANKING.PROSPECT
            WHERE ROWID = CHARTOROWID(:Id)
            """;

        await using var connection =
            new OracleConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new OracleCommand(sql, connection);

        command.BindByName = true;

        AddStringParameter(
            command,
            ":Id",
            id);

        await command.ExecuteNonQueryAsync();
    }


    // =========================================================
    // DOWNLOAD
    // =========================================================
public async Task<List<Prospect>> GetAllForDownloadAsync(
    string? searchTerm,
    string? country,
    string? region,
    string? prospectType,
    string? ubaCustomer)
{
    var whereConditions = new List<string>();

    var parameters = new List<OracleParameter>();

    // =====================================================
    // SEARCH
    // =====================================================
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        whereConditions.Add("""
            (
                LOWER(COMPANY_NAME) LIKE LOWER(:SearchTerm)
                OR LOWER(PROSPECT_NAME) LIKE LOWER(:SearchTerm)
                OR LOWER(PROSPECT_TYPE) LIKE LOWER(:SearchTerm)
                OR LOWER(INDUSTRY) LIKE LOWER(:SearchTerm)
                OR LOWER(COUNTRY) LIKE LOWER(:SearchTerm)
                OR LOWER(REGION) LIKE LOWER(:SearchTerm)
            )
            """);

        parameters.Add(
            new OracleParameter(
                ":SearchTerm",
                OracleDbType.Varchar2)
            {
                Value = $"%{searchTerm.Trim()}%"
            });
    }

    // =====================================================
    // COUNTRY
    // =====================================================
    if (!string.IsNullOrWhiteSpace(country))
    {
        whereConditions.Add(
            "LOWER(COUNTRY) = LOWER(:Country)");

        parameters.Add(
            new OracleParameter(
                ":Country",
                OracleDbType.Varchar2)
            {
                Value = country.Trim()
            });
    }

    // =====================================================
    // REGION
    // =====================================================
    if (!string.IsNullOrWhiteSpace(region))
    {
        whereConditions.Add(
            "LOWER(REGION) = LOWER(:Region)");

        parameters.Add(
            new OracleParameter(
                ":Region",
                OracleDbType.Varchar2)
            {
                Value = region.Trim()
            });
    }

    // =====================================================
    // PROSPECT TYPE
    // =====================================================
    if (!string.IsNullOrWhiteSpace(prospectType))
    {
        whereConditions.Add(
            "LOWER(PROSPECT_TYPE) = LOWER(:ProspectType)");

        parameters.Add(
            new OracleParameter(
                ":ProspectType",
                OracleDbType.Varchar2)
            {
                Value = prospectType.Trim()
            });
    }

    // =====================================================
    // UBA CUSTOMER
    // =====================================================
    if (!string.IsNullOrWhiteSpace(ubaCustomer))
    {
        whereConditions.Add(
            "LOWER(UBA_CUSTOMER) = LOWER(:UbaCustomer)");

        parameters.Add(
            new OracleParameter(
                ":UbaCustomer",
                OracleDbType.Varchar2)
            {
                Value = ubaCustomer.Trim()
            });
    }

    var whereClause =
        whereConditions.Count > 0
            ? "WHERE " + string.Join(
                " AND ",
                whereConditions)
            : "";

    // =====================================================
    // DOWNLOAD QUERY
    // =====================================================
    var sql = $"""
        SELECT
            ROWID AS ROW_ID,
            COUNTRY,
            COMPANY_NAME,
            PROSPECT_NAME,
            PROSPECT_TYPE,
            ADDRESS,
            STATE,
            REGION,
            PHONE,
            EMAIL,
            INDUSTRY,
            UBA_CUSTOMER,
            MARKETED_BY,
            MARKETED_DATE,
            BRANCH_ID,
            ACQUISITION_STATUS,
            UPLOADED_DATE,
            CONVERTED_DATE,
            REMARKS
        FROM BANKING.PROSPECT
        {whereClause}
        ORDER BY COUNTRY, REGION, PROSPECT_NAME
        """;

    var prospects = new List<Prospect>();

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    foreach (var parameter in parameters)
    {
        command.Parameters.Add(parameter);
    }

    await using var reader =
        await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        prospects.Add(MapProspect(reader));
    }

    return prospects;
}
    // =========================================================
    // CHECK DUPLICATE PROSPECT
    // =========================================================
// =========================================================
// CHECK DUPLICATE PROSPECT
// =========================================================
public async Task<bool> ExistsAsync(
    string country,
    string companyName,
    string prospectName,
    string? branchId,
    string? id = null)
{
    var sql = """
        SELECT COUNT(*)
        FROM BANKING.PROSPECT
        WHERE LOWER(COUNTRY) = LOWER(:Country)
          AND LOWER(COMPANY_NAME) = LOWER(:CompanyName)
          AND LOWER(PROSPECT_NAME) = LOWER(:ProspectName)
          AND (
                LOWER(BRANCH_ID) = LOWER(:BranchId)
                OR (
                    BRANCH_ID IS NULL
                    AND :BranchId IS NULL
                )
              )
        """;

    if (!string.IsNullOrWhiteSpace(id))
    {
        sql += """
            AND ROWID <> CHARTOROWID(:Id)
            """;
    }

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    AddStringParameter(
        command,
        ":Country",
        country);

    AddStringParameter(
        command,
        ":CompanyName",
        companyName);

    AddStringParameter(
        command,
        ":ProspectName",
        prospectName);

    AddStringParameter(
        command,
        ":BranchId",
        branchId);

    if (!string.IsNullOrWhiteSpace(id))
    {
        AddStringParameter(
            command,
            ":Id",
            id);
    }

    var count =
        Convert.ToInt32(
            await command.ExecuteScalarAsync());

    return count > 0;
}


// =========================================================
// DASHBOARD SUMMARY
// =========================================================
public async Task<DashboardSummary> GetDashboardSummaryAsync(
    string? country = null,
    string? region = null,
    string? branchId = null,
    DateTime? startDate = null,
    DateTime? endDate = null)
{
    var whereConditions = new List<string>();
    var parameters = new List<OracleParameter>();

    if (!string.IsNullOrWhiteSpace(country))
    {
        whereConditions.Add(
            "LOWER(COUNTRY) = LOWER(:Country)");

        parameters.Add(
            new OracleParameter(
                ":Country",
                OracleDbType.Varchar2)
            {
                Value = country.Trim()
            });
    }

    if (!string.IsNullOrWhiteSpace(region))
    {
        whereConditions.Add(
            "LOWER(REGION) = LOWER(:Region)");

        parameters.Add(
            new OracleParameter(
                ":Region",
                OracleDbType.Varchar2)
            {
                Value = region.Trim()
            });
    }

    if (!string.IsNullOrWhiteSpace(branchId))
    {
        whereConditions.Add(
            "LOWER(BRANCH_ID) = LOWER(:BranchId)");

        parameters.Add(
            new OracleParameter(
                ":BranchId",
                OracleDbType.Varchar2)
            {
                Value = branchId.Trim()
            });
    }

    if (startDate.HasValue)
{
    whereConditions.Add(
        "UPLOADED_DATE >= :StartDate");

    parameters.Add(
        new OracleParameter(
            ":StartDate",
            OracleDbType.Date)
        {
            Value = startDate.Value.Date
        });
}

if (endDate.HasValue)
{
    whereConditions.Add(
        "UPLOADED_DATE < :EndDate");

    parameters.Add(
        new OracleParameter(
            ":EndDate",
            OracleDbType.Date)
        {
            Value = endDate.Value.Date.AddDays(1)
        });
}

    var whereClause =
        whereConditions.Count > 0
            ? "WHERE " + string.Join(
                " AND ",
                whereConditions)
            : "";

    var sql = $"""
        SELECT
            COUNT(*) AS TOTAL_PROSPECTS,

            SUM(
                CASE
                    WHEN UBA_CUSTOMER = 'No'
                    THEN 1
                    ELSE 0
                END
            ) AS ACTIVE_PROSPECTS,

            SUM(
                CASE
                    WHEN UBA_CUSTOMER = 'Yes'
                    THEN 1
                    ELSE 0
                END
            ) AS CONVERTED_CUSTOMERS,

            SUM(
                CASE
                    WHEN ACQUISITION_STATUS = 'New'
                    THEN 1
                    ELSE 0
                END
            ) AS NEW_COUNT,

            SUM(
                CASE
                    WHEN ACQUISITION_STATUS = 'Contacted'
                    THEN 1
                    ELSE 0
                END
            ) AS CONTACTED_COUNT,

            SUM(
                CASE
                    WHEN ACQUISITION_STATUS = 'Engaged'
                    THEN 1
                    ELSE 0
                END
            ) AS ENGAGED_COUNT,

            SUM(
                CASE
                    WHEN ACQUISITION_STATUS = 'Interested'
                    THEN 1
                    ELSE 0
                END
            ) AS INTERESTED_COUNT,

            SUM(
                CASE
                    WHEN ACQUISITION_STATUS = 'Not Interested'
                    THEN 1
                    ELSE 0
                END
            ) AS NOT_INTERESTED_COUNT,

            SUM(
                CASE
                    WHEN ACQUISITION_STATUS = 'Lost'
                    THEN 1
                    ELSE 0
                END
            ) AS LOST_COUNT,

            SUM(
                CASE
                    WHEN ACQUISITION_STATUS = 'Converted'
                    THEN 1
                    ELSE 0
                END
            ) AS CONVERTED_COUNT,

        CAST(
                AVG(
                    CASE
                        WHEN UBA_CUSTOMER = 'Yes'
                             AND UPLOADED_DATE IS NOT NULL
                             AND CONVERTED_DATE IS NOT NULL
                        THEN CONVERTED_DATE - UPLOADED_DATE
                        ELSE NULL
                    END
                ) AS BINARY_DOUBLE
            ) AS AVG_CONVERSION_DAYS

        FROM BANKING.PROSPECT
        {whereClause}
        """;

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    foreach (var parameter in parameters)
    {
        command.Parameters.Add(parameter);
    }

    await using var reader =
        await command.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
    {
        return new DashboardSummary();
    }

    var totalProspects =
        reader.IsDBNull(
            reader.GetOrdinal("TOTAL_PROSPECTS"))
            ? 0
            : Convert.ToInt32(
                reader["TOTAL_PROSPECTS"]);

    var convertedCustomers =
        reader.IsDBNull(
            reader.GetOrdinal("CONVERTED_CUSTOMERS"))
            ? 0
            : Convert.ToInt32(
                reader["CONVERTED_CUSTOMERS"]);

    var averageConversionDays = 0m;

if (!reader.IsDBNull(
        reader.GetOrdinal("AVG_CONVERSION_DAYS")))
{
    averageConversionDays =
        Convert.ToDecimal(
            reader.GetDouble(
                reader.GetOrdinal(
                    "AVG_CONVERSION_DAYS")));
}


    var conversionRate =
        totalProspects > 0
            ? Math.Round(
                (decimal)convertedCustomers /
                totalProspects * 100,
                2)
            : 0;

    return new DashboardSummary
    {
        TotalProspects = totalProspects,

        ActiveProspects =
            reader.IsDBNull(
                reader.GetOrdinal("ACTIVE_PROSPECTS"))
                ? 0
                : Convert.ToInt32(
                    reader["ACTIVE_PROSPECTS"]),

        ConvertedCustomers = convertedCustomers,

        ConversionRate = conversionRate,

        AverageConversionDays =
            Math.Round(
                averageConversionDays,
                2),

        NewCount =
            reader.IsDBNull(
                reader.GetOrdinal("NEW_COUNT"))
                ? 0
                : Convert.ToInt32(
                    reader["NEW_COUNT"]),

        ContactedCount =
            reader.IsDBNull(
                reader.GetOrdinal("CONTACTED_COUNT"))
                ? 0
                : Convert.ToInt32(
                    reader["CONTACTED_COUNT"]),

        EngagedCount =
            reader.IsDBNull(
                reader.GetOrdinal("ENGAGED_COUNT"))
                ? 0
                : Convert.ToInt32(
                    reader["ENGAGED_COUNT"]),

        InterestedCount =
            reader.IsDBNull(
                reader.GetOrdinal("INTERESTED_COUNT"))
                ? 0
                : Convert.ToInt32(
                    reader["INTERESTED_COUNT"]),

        NotInterestedCount =
            reader.IsDBNull(
                reader.GetOrdinal("NOT_INTERESTED_COUNT"))
                ? 0
                : Convert.ToInt32(
                    reader["NOT_INTERESTED_COUNT"]),

        LostCount =
            reader.IsDBNull(
                reader.GetOrdinal("LOST_COUNT"))
                ? 0
                : Convert.ToInt32(
                    reader["LOST_COUNT"]),

        ConvertedCount =
            reader.IsDBNull(
                reader.GetOrdinal("CONVERTED_COUNT"))
                ? 0
                : Convert.ToInt32(
                    reader["CONVERTED_COUNT"])
    };
}

public async Task<List<BranchPerformance>>
    GetBranchPerformanceAsync(
        string? country = null,
        string? region = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
{
    var whereConditions = new List<string>();
    var parameters = new List<OracleParameter>();

    if (!string.IsNullOrWhiteSpace(country))
    {
        whereConditions.Add(
            "LOWER(COUNTRY) = LOWER(:Country)");

        parameters.Add(
            new OracleParameter(
                ":Country",
                OracleDbType.Varchar2)
            {
                Value = country.Trim()
            });
    }

    if (!string.IsNullOrWhiteSpace(region))
    {
        whereConditions.Add(
            "LOWER(REGION) = LOWER(:Region)");

        parameters.Add(
            new OracleParameter(
                ":Region",
                OracleDbType.Varchar2)
            {
                Value = region.Trim()
            });
    }

    if (startDate.HasValue)
    {
        whereConditions.Add(
            "UPLOADED_DATE >= :StartDate");

        parameters.Add(
            new OracleParameter(
                ":StartDate",
                OracleDbType.Date)
            {
                Value = startDate.Value.Date
            });
    }

    if (endDate.HasValue)
    {
        whereConditions.Add(
            "UPLOADED_DATE < :EndDate");

        parameters.Add(
            new OracleParameter(
                ":EndDate",
                OracleDbType.Date)
            {
                Value = endDate.Value.Date.AddDays(1)
            });
    }

    whereConditions.Add(
        "BRANCH_ID IS NOT NULL");

    whereConditions.Add(
        "TRIM(BRANCH_ID) IS NOT NULL");

    var whereClause =
        "WHERE " +
        string.Join(
            " AND ",
            whereConditions);

    var sql = $"""
        SELECT
            TRIM(BRANCH_ID) AS BRANCH_ID,

            COUNT(*) AS TOTAL_PROSPECTS,

            SUM(
                CASE
                    WHEN UBA_CUSTOMER = 'Yes'
                    THEN 1
                    ELSE 0
                END
            ) AS CONVERTED_CUSTOMERS

        FROM BANKING.PROSPECT

        {whereClause}

        GROUP BY TRIM(BRANCH_ID)

        ORDER BY TOTAL_PROSPECTS DESC
        """;

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    foreach (var parameter in parameters)
    {
        command.Parameters.Add(parameter);
    }

    var results =
        new List<BranchPerformance>();

    await using var reader =
        await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var totalProspects =
            Convert.ToInt32(
                reader["TOTAL_PROSPECTS"]);

        var convertedCustomers =
            reader.IsDBNull(
                reader.GetOrdinal("CONVERTED_CUSTOMERS"))
                ? 0
                : Convert.ToInt32(
                    reader["CONVERTED_CUSTOMERS"]);

        var conversionRate =
            totalProspects > 0
                ? Math.Round(
                    (decimal)convertedCustomers /
                    totalProspects * 100,
                    2)
                : 0;

        results.Add(
            new BranchPerformance
            {
                BranchId =
                    reader["BRANCH_ID"]?.ToString()
                    ?? string.Empty,

                TotalProspects =
                    totalProspects,

                ConvertedCustomers =
                    convertedCustomers,

                ConversionRate =
                    conversionRate
            });
    }

    return results;
}


    public async Task<List<MarketerPerformance>>
    GetMarketerPerformanceAsync(
        string? country = null,
        string? region = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
{
    var whereConditions = new List<string>();
    var parameters = new List<OracleParameter>();

    if (!string.IsNullOrWhiteSpace(country))
    {
        whereConditions.Add(
            "LOWER(COUNTRY) = LOWER(:Country)");

        parameters.Add(
            new OracleParameter(
                ":Country",
                OracleDbType.Varchar2)
            {
                Value = country.Trim()
            });
    }

    if (!string.IsNullOrWhiteSpace(region))
    {
        whereConditions.Add(
            "LOWER(REGION) = LOWER(:Region)");

        parameters.Add(
            new OracleParameter(
                ":Region",
                OracleDbType.Varchar2)
            {
                Value = region.Trim()
            });
    }

    if (startDate.HasValue)
    {
        whereConditions.Add(
            "UPLOADED_DATE >= :StartDate");

        parameters.Add(
            new OracleParameter(
                ":StartDate",
                OracleDbType.Date)
            {
                Value = startDate.Value.Date
            });
    }

    if (endDate.HasValue)
    {
        whereConditions.Add(
            "UPLOADED_DATE < :EndDate");

        parameters.Add(
            new OracleParameter(
                ":EndDate",
                OracleDbType.Date)
            {
                Value = endDate.Value.Date.AddDays(1)
            });
    }

    whereConditions.Add(
        "MARKETED_BY IS NOT NULL");

    whereConditions.Add(
        "TRIM(MARKETED_BY) IS NOT NULL");

    var whereClause =
        "WHERE " +
        string.Join(
            " AND ",
            whereConditions);

    var sql = $"""
        SELECT
            TRIM(MARKETED_BY) AS MARKETER,

            COUNT(*) AS TOTAL_PROSPECTS,

            SUM(
                CASE
                    WHEN UBA_CUSTOMER = 'Yes'
                    THEN 1
                    ELSE 0
                END
            ) AS CONVERTED_CUSTOMERS

        FROM BANKING.PROSPECT

        {whereClause}

        GROUP BY TRIM(MARKETED_BY)

        ORDER BY TOTAL_PROSPECTS DESC
        """;

    await using var connection =
        new OracleConnection(_connectionString);

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    command.BindByName = true;

    foreach (var parameter in parameters)
    {
        command.Parameters.Add(parameter);
    }

    var results =
        new List<MarketerPerformance>();

    await using var reader =
        await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var totalProspects =
            Convert.ToInt32(
                reader["TOTAL_PROSPECTS"]);

        var convertedCustomers =
            reader.IsDBNull(
                reader.GetOrdinal("CONVERTED_CUSTOMERS"))
                ? 0
                : Convert.ToInt32(
                    reader["CONVERTED_CUSTOMERS"]);

        var conversionRate =
            totalProspects > 0
                ? Math.Round(
                    (decimal)convertedCustomers /
                    totalProspects * 100,
                    2)
                : 0;

        results.Add(
            new MarketerPerformance
            {
                Marketer =
                    reader["MARKETER"]?.ToString()
                    ?? string.Empty,

                TotalProspects =
                    totalProspects,

                ConvertedCustomers =
                    convertedCustomers,

                ConversionRate =
                    conversionRate
            });
    }

    return results;
}

    // =========================================================
    // MAP ORACLE READER TO MODEL
    // =========================================================
    private static Prospect MapProspect(
    OracleDataReader reader)
    {
    return new Prospect
    {
        Id = GetString(reader, "ROW_ID"),
        Country = GetString(reader, "COUNTRY") ?? string.Empty,
        CompanyName = GetString(reader, "COMPANY_NAME") ?? string.Empty,
        ProspectName = GetString(reader, "PROSPECT_NAME") ?? string.Empty,
        ProspectType = GetString(reader, "PROSPECT_TYPE") ?? string.Empty,
        Address = GetString(reader, "ADDRESS"),
        State = GetString(reader, "STATE"),
        Region = GetString(reader, "REGION"),
        Phone = GetString(reader, "PHONE"),
        Email = GetString(reader, "EMAIL"),
        Industry = GetString(reader, "INDUSTRY") ?? string.Empty,

        UbaCustomer = GetString(reader, "UBA_CUSTOMER") ?? "No",

        MarketedBy = GetString(reader, "MARKETED_BY"),

        MarketedDate = GetDateTime(reader, "MARKETED_DATE"),

	BranchId = GetString(reader, "BRANCH_ID"),

        UploadedDate = GetDateTime(reader, "UPLOADED_DATE"),

        ConvertedDate = GetDateTime(reader, "CONVERTED_DATE"),

        AcquisitionStatus =
            GetString(reader, "ACQUISITION_STATUS") ?? "New",

        Remarks = GetString(reader, "REMARKS")
     };
    }


    // =========================================================
    // HELPER
    // =========================================================
    private static string? GetString(
        OracleDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetValue(ordinal)?.ToString();
    }

    private static DateTime? GetDateTime(
    OracleDataReader reader,
    string columnName)
    {
    var ordinal = reader.GetOrdinal(columnName);

    return reader.IsDBNull(ordinal)
        ? null
        : reader.GetDateTime(ordinal);
    }


    private static void AddStringParameter(
        OracleCommand command,
        string name,
        string? value)
    {
        command.Parameters.Add(
            new OracleParameter(
                name,
                OracleDbType.Varchar2)
            {
                Value = string.IsNullOrWhiteSpace(value)
                    ? DBNull.Value
                    : value.Trim()
            });
    }
}
