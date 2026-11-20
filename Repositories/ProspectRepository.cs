using Oracle.ManagedDataAccess.Client;
using System.Data;
using U360Prospect.Models;
using U360Prospect.Data;

namespace U360Prospect.Repositories;

public class ProspectRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

public ProspectRepository(
    OracleConnectionFactory connectionFactory)
{
    _connectionFactory = connectionFactory;
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
	string? industry = null,
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
// INDUSTRY FILTER
// =====================================================
if (!string.IsNullOrWhiteSpace(industry))
{
    whereConditions.Add(
        "LOWER(INDUSTRY) = LOWER(:Industry)");

    parameters.Add(
        new OracleParameter(
            ":Industry",
            OracleDbType.Varchar2)
        {
            Value = industry.Trim()
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
            FROM U360PROSPECT.PROSPECT
            {whereClause}
            """;


        await using (var connection =
            _connectionFactory.CreateConnection())
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
                FUNDED,
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
            FROM U360PROSPECT.PROSPECT
            {whereClause}
            ORDER BY {orderColumn} {orderDirection}
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY
            """;


        await using (var connection =
            _connectionFactory.CreateConnection())
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
public Task<List<string>> GetCountriesAsync()
{
    var countries = CountryCatalog.All
        .Select(country => country.Code)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
        .ToList();

    return Task.FromResult(countries);
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
                "UPPER(TRIM(COUNTRY)) = UPPER(TRIM(:Country))");

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
            FROM U360PROSPECT.PROSPECT
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY REGION
            """;

        var regions = new List<string>();

        await using var connection =
            _connectionFactory.CreateConnection();

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
            "UPPER(TRIM(COUNTRY)) = UPPER(TRIM(:Country))");

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
        FROM U360PROSPECT.PROSPECT
        {whereClause}
        ORDER BY TRIM(BRANCH_ID)
        """;

    await using var connection =
        _connectionFactory.CreateConnection();

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
            FROM U360PROSPECT.PROSPECT
            WHERE PROSPECT_TYPE IS NOT NULL
              AND TRIM(PROSPECT_TYPE) IS NOT NULL
            ORDER BY PROSPECT_TYPE
            """;

        var prospectTypes = new List<string>();

        await using var connection =
            _connectionFactory.CreateConnection();

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
// GET INDUSTRIES
// =========================================================
public async Task<List<string>> GetIndustriesAsync()
{
    const string sql = """
        SELECT DISTINCT INDUSTRY
        FROM U360PROSPECT.PROSPECT
        WHERE INDUSTRY IS NOT NULL
          AND TRIM(INDUSTRY) IS NOT NULL
        ORDER BY INDUSTRY
        """;

    var industries = new List<string>();

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync();

    await using var command =
        new OracleCommand(sql, connection);

    await using var reader =
        await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        industries.Add(reader.GetString(0));
    }

    return industries;
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
                FUNDED,
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
            FROM U360PROSPECT.PROSPECT
            WHERE ROWID = CHARTOROWID(:Id)
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

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
        INSERT INTO U360PROSPECT.PROSPECT
        (
            COUNTRY,
            COMPANY_NAME,
            PROSPECT_NAME,
            PROSPECT_TYPE,
            FUNDED,
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
            :Funded,
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
        _connectionFactory.CreateConnection();

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
    ":Funded",
    prospect.Funded);

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



public async Task<(int Inserted, int Skipped)> BulkAddAsync(
    IReadOnlyCollection<Prospect> prospects)
{
    if (prospects.Count == 0)
        return (0, 0);

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync();

    // Load existing duplicate keys once.
    var existingKeys =
        new HashSet<(string Country, string Company,
                      string Prospect, string Branch)>();

    const string existingSql = """
        SELECT COUNTRY, COMPANY_NAME, PROSPECT_NAME, BRANCH_ID
        FROM U360PROSPECT.PROSPECT
        """;

    await using (var existingCommand =
        new OracleCommand(existingSql, connection))
    await using (var reader =
        await existingCommand.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            existingKeys.Add((
                NormalizeKeyPart(reader.GetValue(0)),
                NormalizeKeyPart(reader.GetValue(1)),
                NormalizeKeyPart(reader.GetValue(2)),
                NormalizeKeyPart(reader.GetValue(3))
            ));
        }
    }

    var pending = new List<Prospect>();
    int skipped = 0;

    // Detect duplicates both in Oracle and within this workbook.
    foreach (var prospect in prospects)
    {
        var key = (
            NormalizeKeyPart(prospect.Country),
            NormalizeKeyPart(prospect.CompanyName),
            NormalizeKeyPart(prospect.ProspectName),
            NormalizeKeyPart(prospect.BranchId)
        );

        if (!existingKeys.Add(key))
        {
            skipped++;
            continue;
        }

        pending.Add(prospect);
    }

    if (pending.Count == 0)
        return (0, skipped);

    const string insertSql = """
        INSERT INTO U360PROSPECT.PROSPECT
        (
            COUNTRY,
            COMPANY_NAME,
            PROSPECT_NAME,
            PROSPECT_TYPE,
            FUNDED,
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
            :Funded,
            :Address,
            :State,
            :Region,
            :Phone,
            :Email,
            :Industry,
            'No',
            :MarketedBy,
            :MarketedDate,
            :BranchId,
            :AcquisitionStatus,
            SYSDATE,
            NULL,
            :Remarks
        )
        """;

    using var transaction = connection.BeginTransaction();

    try
    {
        const int batchSize = 500;

        for (int start = 0; start < pending.Count; start += batchSize)
        {
            var batch = pending
                .Skip(start)
                .Take(batchSize)
                .ToArray();

            await using var command =
                new OracleCommand(insertSql, connection)
                {
                    BindByName = true,
                    ArrayBindCount = batch.Length,
                    Transaction = transaction
                };

            void AddStringArray(
                string name,
                Func<Prospect, string?> selector)
            {
                var values = batch
                    .Select(p => selector(p) ?? string.Empty)
                    .ToArray();

                var parameter = command.Parameters.Add(
                    name, OracleDbType.Varchar2);

                parameter.Value = values;

                parameter.ArrayBindSize = values
                    .Select(v => Math.Max(
                        1,
                        System.Text.Encoding.UTF8.GetByteCount(v)))
                    .ToArray();
            }

            AddStringArray("Country", p => p.Country);
            AddStringArray("CompanyName", p => p.CompanyName);
            AddStringArray("ProspectName", p => p.ProspectName);
            AddStringArray("ProspectType", p => p.ProspectType);
            AddStringArray("Address", p => p.Address);
            AddStringArray("State", p => p.State);
            AddStringArray("Region", p => p.Region);
            AddStringArray("Phone", p => p.Phone);
            AddStringArray("Email", p => p.Email);
            AddStringArray("Industry", p => p.Industry);
	    AddStringArray("Funded", p => p.Funded);
            AddStringArray("MarketedBy", p => p.MarketedBy);
            AddStringArray("BranchId", p => p.BranchId);
            AddStringArray(
                "AcquisitionStatus",
                p => p.AcquisitionStatus ?? "New");
            AddStringArray("Remarks", p => p.Remarks);

            var marketedDate = command.Parameters.Add(
                "MarketedDate", OracleDbType.Date);

            marketedDate.Value = batch
                .Select(p => p.MarketedDate.HasValue
                    ? (object)p.MarketedDate.Value
                    : DBNull.Value)
                .ToArray();

            await command.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }
    catch
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Preserve the original database error.
        }

        throw;
    }

    return (pending.Count, skipped);
}

private static string NormalizeKeyPart(object? value)
{
    return (Convert.ToString(value)?.Trim() ?? string.Empty)
        .ToUpperInvariant();
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
        UPDATE U360PROSPECT.PROSPECT
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
        _connectionFactory.CreateConnection();

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
        UPDATE U360PROSPECT.PROSPECT
        SET
            UBA_CUSTOMER = 'Yes',
            ACQUISITION_STATUS = 'Converted',
            CONVERTED_DATE = SYSDATE
        WHERE ROWID = CHARTOROWID(:Id)
        """;

    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync();

    await using var command = new OracleCommand(sql, connection);

    command.Parameters.Add(
        new OracleParameter(":Id", OracleDbType.Varchar2)
        {
            Value = id
        });

    await command.ExecuteNonQueryAsync();
    }



public async Task<string?> VerifyAndConvertToCustomerAsync(
    string id,
    string accountNumber,
    string solId,
    string country,
    DateTime submittedOpenDate,
    string convertedBy)
{
    await using var connection =
        _connectionFactory.CreateConnection();

    await connection.OpenAsync();

    using var transaction =
        connection.BeginTransaction(IsolationLevel.ReadCommitted);

    try
    {
        // 1. Lock the application-owned prospect record.
        const string prospectSql = """
            SELECT
                UPLOADED_DATE,
                UBA_CUSTOMER,
                ACQUISITION_STATUS
            FROM U360PROSPECT.PROSPECT
            WHERE ROWID = CHARTOROWID(:Id)
            FOR UPDATE
            """;

        DateTime? uploadedDate;
        string? ubaCustomer;
        string? acquisitionStatus;

        await using (var command =
            new OracleCommand(prospectSql, connection))
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.Parameters.Add(
                "Id", OracleDbType.Varchar2, 30).Value = id;

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return "The selected prospect could not be found.";

            uploadedDate = reader.IsDBNull(0)
                ? null
                : reader.GetDateTime(0);

            ubaCustomer = reader.IsDBNull(1)
                ? null
                : reader.GetString(1);

            acquisitionStatus = reader.IsDBNull(2)
                ? null
                : reader.GetString(2);
        }

        if (!uploadedDate.HasValue)
            return "The prospect has no recorded upload date.";

        if (string.Equals(
                ubaCustomer, "Yes",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                acquisitionStatus, "Converted",
                StringComparison.OrdinalIgnoreCase))
        {
            return "This prospect has already been converted.";
        }

        // 2. Verify the banking account through the read-only connection.
        const string accountSql = """
            SELECT
                TRUNC(ACCOUNT_OPEN_DATE) AS OPEN_DATE,
                CURRENCY,
                ACCOUNT_TYPE,
                CASE
                    WHEN CLEARED_BALANCE >= 0 THEN 'Yes'
                    ELSE 'No'
                END AS FUNDED,
                CUSTOMER_ID,
                ACCOUNT_OFFICER,
                DATE_LAST_MODIFIED,
                ACCOUNT_ATTRIBUTE_2 AS SCHM_CODE,
                COUNT(*) OVER() AS MATCH_COUNT
            FROM BANKING.ACCOUNTS_DLY
            WHERE LE_BOOK = '01'
              AND TRIM(ACCOUNT_NO) = TRIM(:AccountNo)
              AND SUBSTR(VISION_OUC, 7, 4) = :SolId
              AND UPPER(TRIM(COUNTRY)) =
                  CASE UPPER(TRIM(:Country))
                      WHEN 'NIGERIA' THEN 'NG'
                      ELSE UPPER(TRIM(:Country))
                  END
              AND ACCOUNT_OPEN_DATE IS NOT NULL
            """;

        DateTime? actualOpenDate;
        string? currency;
        string? accountType;
        string? funded;
        string? customerId;
        string? accountOfficer;
        DateTime? dateLastModified;
        string? schmCode;
        long matchCount;

        await using (var bankingConnection =
            _connectionFactory.CreateVisionReadOnlyConnection())
        {
            await bankingConnection.OpenAsync();

            await using var command =
                new OracleCommand(accountSql, bankingConnection);

            command.BindByName = true;

            command.Parameters.Add(
                "AccountNo", OracleDbType.Varchar2, 50)
                .Value = accountNumber.Trim();

            command.Parameters.Add(
                "SolId", OracleDbType.Varchar2, 4)
                .Value = solId.Trim();

            command.Parameters.Add(
                "Country", OracleDbType.Varchar2, 100)
                .Value = country.Trim();

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                matchCount = 0;
                actualOpenDate = null;
                currency = null;
                accountType = null;
                funded = null;
                customerId = null;
                accountOfficer = null;
                dateLastModified = null;
                schmCode = null;
            }
            else
            {
                actualOpenDate = reader.IsDBNull(
                    reader.GetOrdinal("OPEN_DATE"))
                    ? null
                    : reader.GetDateTime(
                        reader.GetOrdinal("OPEN_DATE"));

                currency = reader.IsDBNull(
                    reader.GetOrdinal("CURRENCY"))
                    ? null
                    : reader.GetString(
                        reader.GetOrdinal("CURRENCY"));

                accountType = reader.IsDBNull(
                    reader.GetOrdinal("ACCOUNT_TYPE"))
                    ? null
                    : reader.GetString(
                        reader.GetOrdinal("ACCOUNT_TYPE"));

                funded = reader.IsDBNull(
                    reader.GetOrdinal("FUNDED"))
                    ? null
                    : reader.GetString(
                        reader.GetOrdinal("FUNDED"));

                customerId = reader.IsDBNull(
                    reader.GetOrdinal("CUSTOMER_ID"))
                    ? null
                    : reader.GetString(
                        reader.GetOrdinal("CUSTOMER_ID"));

                accountOfficer = reader.IsDBNull(
                    reader.GetOrdinal("ACCOUNT_OFFICER"))
                    ? null
                    : reader.GetString(
                        reader.GetOrdinal("ACCOUNT_OFFICER"));

                dateLastModified = reader.IsDBNull(
                    reader.GetOrdinal("DATE_LAST_MODIFIED"))
                    ? null
                    : reader.GetDateTime(
                        reader.GetOrdinal("DATE_LAST_MODIFIED"));

                schmCode = reader.IsDBNull(
                    reader.GetOrdinal("SCHM_CODE"))
                    ? null
                    : reader.GetString(
                        reader.GetOrdinal("SCHM_CODE"));

                matchCount = Convert.ToInt64(
                    reader["MATCH_COUNT"]);
            }
        }

        // 3. Validate the banking account.
        if (matchCount == 0 || !actualOpenDate.HasValue)
        {
            return "No matching account was found. Check the account " +
                   "number, SOL ID and country.";
        }

        if (actualOpenDate.Value.Date != submittedOpenDate.Date)
        {
            return "The account opening date entered does not match " +
                   "the date held in the banking database.";
        }

        if (actualOpenDate.Value.Date <= uploadedDate.Value.Date)
        {
            return "The account opening date must be later than the " +
                   "prospect upload date (" +
                   uploadedDate.Value.ToString("dd-MMM-yyyy") + ").";
        }

        // 4. Update the application-owned prospect record.
        const string updateSql = """
            UPDATE U360PROSPECT.PROSPECT
            SET
                UBA_CUSTOMER = 'Yes',
                ACQUISITION_STATUS = 'Converted',
                CONVERTED_DATE = SYSDATE,
                CONVERTED_ACCOUNT_NO = :AccountNo,
                CONVERTED_SOL_ID = :SolId,
                CONVERTED_COUNTRY = :Country,
                ACCOUNT_OPEN_DATE = :OpenDate,
                CURRENCY = :Currency,
                ACCOUNT_TYPE = :AccountType,
                FUNDED = :Funded,
                CUSTOMER_ID = :CustomerId,
                ACCOUNT_OFFICER = :AccountOfficer,
                DATE_LAST_MODIFIED = :DateLastModified,
                SCHM_CODE = :SchmCode,
                CONVERTED_BY = :ConvertedBy
            WHERE ROWID = CHARTOROWID(:Id)
              AND NVL(UBA_CUSTOMER, 'No') <> 'Yes'
              AND NVL(ACQUISITION_STATUS, 'New') <> 'Converted'
            """;

        await using (var command =
            new OracleCommand(updateSql, connection))
        {
            command.Transaction = transaction;
            command.BindByName = true;

            command.Parameters.Add(
                "AccountNo", OracleDbType.Varchar2, 50)
                .Value = accountNumber.Trim();

            command.Parameters.Add(
                "SolId", OracleDbType.Varchar2, 4)
                .Value = solId.Trim();

            command.Parameters.Add(
                "Country", OracleDbType.Varchar2, 100)
                .Value = country.Trim();

            command.Parameters.Add(
                "OpenDate", OracleDbType.Date)
                .Value = actualOpenDate.Value.Date;

            command.Parameters.Add(
                "Currency", OracleDbType.Varchar2, 3)
                .Value = string.IsNullOrWhiteSpace(currency)
                    ? DBNull.Value
                    : currency.Trim();

            command.Parameters.Add(
                "AccountType", OracleDbType.Varchar2, 10)
                .Value = string.IsNullOrWhiteSpace(accountType)
                    ? DBNull.Value
                    : accountType.Trim();

            command.Parameters.Add(
                "Funded", OracleDbType.Varchar2, 15)
                .Value = string.IsNullOrWhiteSpace(funded)
                    ? DBNull.Value
                    : funded.Trim();

            command.Parameters.Add(
                "CustomerId", OracleDbType.Varchar2, 15)
                .Value = string.IsNullOrWhiteSpace(customerId)
                    ? DBNull.Value
                    : customerId.Trim();

            command.Parameters.Add(
                "AccountOfficer", OracleDbType.Varchar2, 10)
                .Value = string.IsNullOrWhiteSpace(accountOfficer)
                    ? DBNull.Value
                    : accountOfficer.Trim();

            command.Parameters.Add(
                "DateLastModified", OracleDbType.TimeStamp)
                .Value = dateLastModified.HasValue
                    ? dateLastModified.Value
                    : DBNull.Value;

            command.Parameters.Add(
                "SchmCode", OracleDbType.Varchar2, 10)
                .Value = string.IsNullOrWhiteSpace(schmCode)
                    ? DBNull.Value
                    : schmCode.Trim();

            command.Parameters.Add(
                "ConvertedBy", OracleDbType.Varchar2, 256)
                .Value = string.IsNullOrWhiteSpace(convertedBy)
                    ? "Unknown"
                    : convertedBy.Trim();

            command.Parameters.Add(
                "Id", OracleDbType.Varchar2, 30)
                .Value = id;

            int rowsUpdated =
                await command.ExecuteNonQueryAsync();

            if (rowsUpdated != 1)
                return "The prospect was not converted. Refresh and retry.";
        }

        // 5. Commit the prospect update.
        transaction.Commit();

        return null;
    }
    catch
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Preserve the original exception.
        }

        throw;
    }
}




    // =========================================================
    // DELETE
    // =========================================================
    public async Task DeleteAsync(string id)
    {
        const string sql = """
            DELETE FROM U360PROSPECT.PROSPECT
            WHERE ROWID = CHARTOROWID(:Id)
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

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
            FUNDED,
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
        FROM U360PROSPECT.PROSPECT
        {whereClause}
        ORDER BY COUNTRY, REGION, PROSPECT_NAME
        """;

    var prospects = new List<Prospect>();

    await using var connection =
        _connectionFactory.CreateConnection();

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
        FROM U360PROSPECT.PROSPECT
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
        _connectionFactory.CreateConnection();

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

        FROM U360PROSPECT.PROSPECT
        {whereClause}
        """;

    await using var connection =
        _connectionFactory.CreateConnection();

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

        FROM U360PROSPECT.PROSPECT

        {whereClause}

        GROUP BY TRIM(BRANCH_ID)

        ORDER BY TOTAL_PROSPECTS DESC
        """;

    await using var connection =
        _connectionFactory.CreateConnection();

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

        FROM U360PROSPECT.PROSPECT

        {whereClause}

        GROUP BY TRIM(MARKETED_BY)

        ORDER BY TOTAL_PROSPECTS DESC
        """;

    await using var connection =
        _connectionFactory.CreateConnection();

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
	Funded = GetString(reader, "FUNDED"),
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
