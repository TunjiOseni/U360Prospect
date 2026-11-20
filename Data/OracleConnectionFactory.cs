
using Oracle.ManagedDataAccess.Client;

namespace U360Prospect.Data;

public class OracleConnectionFactory
{
    private readonly string _applicationConnectionString;
    private readonly string _visionReadOnlyConnectionString;

    public OracleConnectionFactory(IConfiguration configuration)
    {
        _applicationConnectionString =
            configuration.GetConnectionString("U360Prospect")
            ?? throw new InvalidOperationException(
                "U360Prospect connection string is not configured.");

        _visionReadOnlyConnectionString =
            configuration.GetConnectionString("VisionReadOnly")
            ?? throw new InvalidOperationException(
                "VisionReadOnly connection string is not configured.");
    }

    // Application-owned U360Prospect database.
    public OracleConnection CreateConnection()
    {
        return new OracleConnection(_applicationConnectionString);
    }

    // Read-only banking verification database.
    public OracleConnection CreateVisionReadOnlyConnection()
    {
        return new OracleConnection(_visionReadOnlyConnectionString);
    }
}
