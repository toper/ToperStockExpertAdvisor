using LinqToDB.Configuration;

namespace TradingService.Data;

public class DbConnectionSettings : ILinqToDBSettings
{
    private readonly string _connectionString;

    public DbConnectionSettings(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IEnumerable<IDataProviderSettings> DataProviders => [];

    public string DefaultConfiguration => "TradingDB";
    public string DefaultDataProvider => LinqToDB.ProviderName.SQLite;

    public IEnumerable<IConnectionStringSettings> ConnectionStrings
    {
        get
        {
            yield return new ConnectionStringSettings
            {
                Name = "TradingDB",
                ProviderName = LinqToDB.ProviderName.SQLite,
                ConnectionString = _connectionString
            };
        }
    }
}

public class ConnectionStringSettings : IConnectionStringSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public bool IsGlobal => false;
}
