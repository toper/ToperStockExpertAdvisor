using LinqToDB;
using LinqToDB.Data;
using TradingService.Data.Entities;

namespace TradingService.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(TradingDbContext db)
    {
        await db.CreateTableAsync<PutRecommendation>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<ScanLog>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<WatchlistItem>(tableOptions: TableOptions.CreateIfNotExists);
    }
}
