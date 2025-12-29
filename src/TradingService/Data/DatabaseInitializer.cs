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

        await MigrateSchemaAsync(db);
    }

    private static async Task MigrateSchemaAsync(TradingDbContext db)
    {
        // Check if PiotroskiFScore column exists in Recommendations table
        var columnCheckSql = "SELECT COUNT(*) FROM pragma_table_info('Recommendations') WHERE name='PiotroskiFScore'";
        var piotroskiExists = await db.ExecuteAsync<long>(columnCheckSql) > 0;

        if (!piotroskiExists)
        {
            await db.ExecuteAsync("ALTER TABLE Recommendations ADD COLUMN PiotroskiFScore DECIMAL NULL");
        }

        // Check if AltmanZScore column exists in Recommendations table
        columnCheckSql = "SELECT COUNT(*) FROM pragma_table_info('Recommendations') WHERE name='AltmanZScore'";
        var altmanExists = await db.ExecuteAsync<long>(columnCheckSql) > 0;

        if (!altmanExists)
        {
            await db.ExecuteAsync("ALTER TABLE Recommendations ADD COLUMN AltmanZScore DECIMAL NULL");
        }
    }
}
