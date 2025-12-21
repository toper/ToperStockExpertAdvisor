using LinqToDB;
using LinqToDB.Data;
using TradingService.Data.Entities;

namespace TradingService.Data;

public class TradingDbContext : DataConnection
{
    public TradingDbContext(DataOptions<TradingDbContext> options)
        : base(options.Options) { }

    public TradingDbContext(string connectionString)
        : base(ProviderName.SQLite, connectionString) { }

    public ITable<PutRecommendation> Recommendations => this.GetTable<PutRecommendation>();
    public ITable<ScanLog> ScanLogs => this.GetTable<ScanLog>();
    public ITable<WatchlistItem> Watchlist => this.GetTable<WatchlistItem>();
}

public interface IDbContextFactory
{
    TradingDbContext Create();
}

public class DbContextFactory : IDbContextFactory
{
    private readonly string _connectionString;

    public DbContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public TradingDbContext Create() => new TradingDbContext(_connectionString);
}
