using Oracle.ManagedDataAccess.Client;

namespace U360Prospect.Data;

public class OracleConnectionFactory
{
    private readonly string _connectionString;

    public OracleConnectionFactory(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException(
                "Oracle connection string is not configured.");
    }

    public OracleConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }
}
