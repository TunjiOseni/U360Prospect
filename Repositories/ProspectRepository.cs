using Oracle.ManagedDataAccess.Client;
using U360Prospect.Data;
using U360Prospect.Models;


namespace U360Prospect.Repositories;


public class ProspectRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public ProspectRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }



    public async Task<PagedResult<Prospect>> GetAllAsync(
    string? searchTerm = null,
    string? region = null,
    string? companyType = null,
    int pageNumber = 1,
    int pageSize = 10,
    string? sortColumn = null,
    string? sortDirection = "asc")

{
    var prospects = new List<Prospect>();

    using var connection = _connectionFactory.CreateConnection();

    await connection.OpenAsync();

    // Search condition used by both queries
    string searchCondition = @"
    WHERE
        (
            :SearchTerm IS NULL
            OR LOWER(prospect_name) LIKE LOWER(:ProspectNamePattern)
            OR LOWER(industry) LIKE LOWER(:IndustryPattern)
            OR LOWER(company_type) LIKE LOWER(:CompanyTypePattern)
            OR LOWER(region) LIKE LOWER(:RegionPattern)
        )";

    string regionCondition = @"
    AND (
        :Region IS NULL
        OR LOWER(region) = LOWER(:Region)
    )";

    string companyTypeCondition = @"
    AND (
        :CompanyType IS NULL
        OR LOWER(company_type) = LOWER(:CompanyType)
    )";

    object searchValue = string.IsNullOrWhiteSpace(searchTerm)
        ? DBNull.Value
        : searchTerm;

    object pattern = string.IsNullOrWhiteSpace(searchTerm)
        ? DBNull.Value
        : $"%{searchTerm}%";

   // Determine safe database column for sorting
string orderByColumn = sortColumn switch
{
    "ProspectName" => "prospect_name",
    "Industry" => "industry",
    "CompanyType" => "company_type",
    "Region" => "region",
    "LoadDate" => "load_date",
    _ => "id"
};

// Determine sort direction
string orderDirection =
    sortDirection?.ToLower() == "desc"
        ? "DESC"
        : "ASC"; 


    // 1. Get total number of matching records
    string countSql = $@"
    SELECT COUNT(*)
    FROM prospect
    {searchCondition}
    {regionCondition}
    {companyTypeCondition}";

  

    using var countCommand = new OracleCommand(countSql, connection)
    {
    BindByName = true
    };

    countCommand.Parameters.Add(
        new OracleParameter("SearchTerm", searchValue));

    countCommand.Parameters.Add(
        new OracleParameter("ProspectNamePattern", pattern));

    countCommand.Parameters.Add(
        new OracleParameter("IndustryPattern", pattern));

    countCommand.Parameters.Add(
        new OracleParameter("CompanyTypePattern", pattern));

    countCommand.Parameters.Add(
        new OracleParameter("RegionPattern", pattern));

    countCommand.Parameters.Add(
    new OracleParameter(
        "Region",
        string.IsNullOrWhiteSpace(region)
            ? DBNull.Value
            : region));

    countCommand.Parameters.Add(
    new OracleParameter(
        "CompanyType",
        string.IsNullOrWhiteSpace(companyType)
            ? DBNull.Value
            : companyType));

    int totalCount = Convert.ToInt32(
        await countCommand.ExecuteScalarAsync());


    // 2. Calculate how many records to skip
    int offset = (pageNumber - 1) * pageSize;


    // 3. Get records for current page
    string dataSql = $@"
        SELECT
            id,
            prospect_name,
            industry,
            company_type,
            region,
	    address,
	    email,
	    phone_number,
            load_by,
            load_date
        FROM prospect
        {searchCondition}
	{regionCondition}
	{companyTypeCondition}
        ORDER BY {orderByColumn} {orderDirection}
OFFSET :Offset ROWS
FETCH NEXT :PageSize ROWS ONLY";

    using var dataCommand = new OracleCommand(dataSql, connection)
    {
    BindByName = true
    };

    dataCommand.Parameters.Add(
        new OracleParameter("SearchTerm", searchValue));

    dataCommand.Parameters.Add(
        new OracleParameter("ProspectNamePattern", pattern));

    dataCommand.Parameters.Add(
        new OracleParameter("IndustryPattern", pattern));

    dataCommand.Parameters.Add(
        new OracleParameter("CompanyTypePattern", pattern));

    dataCommand.Parameters.Add(
        new OracleParameter("RegionPattern", pattern));

    dataCommand.Parameters.Add(
    new OracleParameter(
        "Region",
        string.IsNullOrWhiteSpace(region)
            ? DBNull.Value
            : region));
    dataCommand.Parameters.Add(
    new OracleParameter(
        "CompanyType",
        string.IsNullOrWhiteSpace(companyType)
            ? DBNull.Value
            : companyType));

    dataCommand.Parameters.Add(
        new OracleParameter("Offset", offset));

    dataCommand.Parameters.Add(
        new OracleParameter("PageSize", pageSize));


    using var reader = await dataCommand.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var prospect = new Prospect
        {
            Id = reader.GetInt32(reader.GetOrdinal("ID")),
            ProspectName = reader.GetString(reader.GetOrdinal("PROSPECT_NAME")),
            Industry = reader.GetString(reader.GetOrdinal("INDUSTRY")),
            CompanyType = reader.GetString(reader.GetOrdinal("COMPANY_TYPE")),
            Region = reader.GetString(reader.GetOrdinal("REGION")),
	    Address = reader.IsDBNull(reader.GetOrdinal("ADDRESS"))
   	    ? null
   	    : reader.GetString(reader.GetOrdinal("ADDRESS")),

            Email = reader.IsDBNull(reader.GetOrdinal("EMAIL"))
   	    ? null
   	    : reader.GetString(reader.GetOrdinal("EMAIL")),

	    PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PHONE_NUMBER"))
   	    ? null
   	    : reader.GetString(reader.GetOrdinal("PHONE_NUMBER")),
            LoadBy = reader.GetString(reader.GetOrdinal("LOAD_BY")),
            LoadDate = reader.IsDBNull(reader.GetOrdinal("LOAD_DATE"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LOAD_DATE"))
        };

        prospects.Add(prospect);
    }


    // 4. Return paginated result
    return new PagedResult<Prospect>
    {
        Items = prospects,
        TotalCount = totalCount,
        PageNumber = pageNumber,
        PageSize = pageSize
    };
}


public async Task<List<string>> GetRegionsAsync()
{
    var regions = new List<string>();

    using var connection = _connectionFactory.CreateConnection();

    await connection.OpenAsync();

    string sql = @"
        SELECT DISTINCT region
        FROM prospect
        WHERE region IS NOT NULL
          AND TRIM(region) IS NOT NULL
        ORDER BY region";

    using var command = new OracleCommand(sql, connection);

    using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        regions.Add(reader.GetString(0));
    }

    return regions;
}

public async Task<List<string>> GetCompanyTypesAsync()
{
    var companyTypes = new List<string>();

    using var connection = _connectionFactory.CreateConnection();

    await connection.OpenAsync();

    string sql = @"
        SELECT DISTINCT company_type
        FROM prospect
        WHERE company_type IS NOT NULL
          AND TRIM(company_type) IS NOT NULL
        ORDER BY company_type";

    using var command = new OracleCommand(sql, connection);

    using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        companyTypes.Add(reader.GetString(0));
    }

    return companyTypes;
}

public async Task<Prospect?> GetByIdAsync(int id)
{
    using var connection = _connectionFactory.CreateConnection();

    string sql = @"
        SELECT
            id,
            prospect_name,
            industry,
            company_type,
            region,
	    address,
	    email,
	    phone_number,
            load_by,
            load_date
        FROM prospect
        WHERE id = :Id";

    using var command = new OracleCommand(sql, connection);

    command.Parameters.Add(new OracleParameter("Id", id));

    await connection.OpenAsync();

    using var reader = await command.ExecuteReaderAsync();

    if (await reader.ReadAsync())
    {
        return new Prospect
        {
            Id = reader.GetInt32(reader.GetOrdinal("ID")),
            ProspectName = reader.GetString(reader.GetOrdinal("PROSPECT_NAME")),
            Industry = reader.GetString(reader.GetOrdinal("INDUSTRY")),
            CompanyType = reader.GetString(reader.GetOrdinal("COMPANY_TYPE")),
            Region = reader.GetString(reader.GetOrdinal("REGION")),

	    Address = reader.IsDBNull(reader.GetOrdinal("ADDRESS"))
		  ? null
            : reader.GetString(reader.GetOrdinal("ADDRESS")),

	    Email = reader.IsDBNull(reader.GetOrdinal("EMAIL"))
		   ? null
            : reader.GetString(reader.GetOrdinal("EMAIL")),

	    PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PHONE_NUMBER"))
		    ? null
            : reader.GetString(reader.GetOrdinal("PHONE_NUMBER")),

            LoadBy = reader.GetString(reader.GetOrdinal("LOAD_BY")),

            LoadDate = reader.IsDBNull(reader.GetOrdinal("LOAD_DATE"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LOAD_DATE"))
        };
    }

    return null;
}

public async Task AddAsync(Prospect prospect)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO BANKING.PROSPECT
            (
                PROSPECT_NAME,
                INDUSTRY,
                COMPANY_TYPE,
                REGION,
		ADDRESS,
		EMAIL,
		PHONE_NUMBER,
                LOAD_BY
            )
            VALUES
            (
                :ProspectName,
                :Industry,
                :CompanyType,
                :Region,
		:Address,
		:Email,
		:PhoneNumber,
                :LoadBy
            )";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.Add(new OracleParameter("ProspectName", prospect.ProspectName));
        command.Parameters.Add(new OracleParameter("Industry", prospect.Industry));
        command.Parameters.Add(new OracleParameter("CompanyType", prospect.CompanyType));
        command.Parameters.Add(new OracleParameter("Region", prospect.Region));
	command.Parameters.Add(new OracleParameter("Address", prospect.Address));
	command.Parameters.Add(new OracleParameter("Email", prospect.Email));
	command.Parameters.Add(new OracleParameter("PhoneNumber", prospect.PhoneNumber));
        command.Parameters.Add(new OracleParameter("LoadBy", prospect.LoadBy));

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

public async Task UpdateAsync(Prospect prospect)
{
    using var connection = _connectionFactory.CreateConnection();

    string sql = @"
        UPDATE BANKING.PROSPECT
        SET
            PROSPECT_NAME = :ProspectName,
            INDUSTRY = :Industry,
            COMPANY_TYPE = :CompanyType,
            REGION = :Region,
	    ADDRESS = :Address,
       	    EMAIL = :Email,
            PHONE_NUMBER = :PhoneNumber
            WHERE ID = :Id";

    using var command = new OracleCommand(sql, connection);

    command.Parameters.Add(new OracleParameter("ProspectName", prospect.ProspectName));
    command.Parameters.Add(new OracleParameter("Industry", prospect.Industry));
    command.Parameters.Add(new OracleParameter("CompanyType", prospect.CompanyType));
    command.Parameters.Add(new OracleParameter("Region", prospect.Region));
    command.Parameters.Add(new OracleParameter("Address", prospect.Address));
    command.Parameters.Add(new OracleParameter("Email", prospect.Email));
    command.Parameters.Add(new OracleParameter("PhoneNumber", prospect.PhoneNumber));
    command.Parameters.Add(new OracleParameter("Id", prospect.Id));

    await connection.OpenAsync();

    await command.ExecuteNonQueryAsync();
}

public async Task DeleteAsync(int id)
{
    using var connection = _connectionFactory.CreateConnection();

    string sql = @"
        DELETE FROM BANKING.PROSPECT
        WHERE ID = :Id";

    using var command = new OracleCommand(sql, connection);

    command.Parameters.Add(new OracleParameter("Id", id));

    await connection.OpenAsync();

    await command.ExecuteNonQueryAsync();
}

public async Task<List<Prospect>> GetAllForDownloadAsync()
{
    var prospects = new List<Prospect>();

    using var connection = _connectionFactory.CreateConnection();

    string sql = @"
        SELECT
            id,
            prospect_name,
            industry,
            company_type,
            region,
            address,
            email,
            phone_number,
            load_by,
            load_date
        FROM BANKING.PROSPECT
        ORDER BY prospect_name";

    using var command = new OracleCommand(sql, connection);

    await connection.OpenAsync();

    using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        prospects.Add(new Prospect
        {
            Id = reader.GetInt32(reader.GetOrdinal("ID")),
            ProspectName = reader.GetString(reader.GetOrdinal("PROSPECT_NAME")),
            Industry = reader.GetString(reader.GetOrdinal("INDUSTRY")),
            CompanyType = reader.GetString(reader.GetOrdinal("COMPANY_TYPE")),
            Region = reader.GetString(reader.GetOrdinal("REGION")),

            Address = reader.IsDBNull(reader.GetOrdinal("ADDRESS"))
                ? null
                : reader.GetString(reader.GetOrdinal("ADDRESS")),

            Email = reader.IsDBNull(reader.GetOrdinal("EMAIL"))
                ? null
                : reader.GetString(reader.GetOrdinal("EMAIL")),

            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PHONE_NUMBER"))
                ? null
                : reader.GetString(reader.GetOrdinal("PHONE_NUMBER")),

            LoadBy = reader.GetString(reader.GetOrdinal("LOAD_BY")),

            LoadDate = reader.IsDBNull(reader.GetOrdinal("LOAD_DATE"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LOAD_DATE"))
        });
    }

    return prospects;
}

public async Task<bool> ExistsAsync(string prospectName)
{
    using var connection = _connectionFactory.CreateConnection();

    string sql = @"
        SELECT COUNT(*)
        FROM BANKING.PROSPECT
        WHERE LOWER(PROSPECT_NAME) = LOWER(:ProspectName)";

    using var command = new OracleCommand(sql, connection);

    command.Parameters.Add(
        new OracleParameter("ProspectName", prospectName));

    await connection.OpenAsync();

    var count = Convert.ToInt32(
        await command.ExecuteScalarAsync());

    return count > 0;
}

}
