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
        await db.CreateTableAsync<CompanyFinancial>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<StockData>(tableOptions: TableOptions.CreateIfNotExists);

        await MigrateSchemaAsync(db);
        await CreateIndexesAsync(db);
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

        // Check if ExanteSymbol column exists in Recommendations table
        columnCheckSql = "SELECT COUNT(*) FROM pragma_table_info('Recommendations') WHERE name='ExanteSymbol'";
        var exanteSymbolExists = await db.ExecuteAsync<long>(columnCheckSql) > 0;

        if (!exanteSymbolExists)
        {
            await db.ExecuteAsync("ALTER TABLE Recommendations ADD COLUMN ExanteSymbol TEXT NULL");
        }

        // Check if OptionPrice column exists in Recommendations table
        columnCheckSql = "SELECT COUNT(*) FROM pragma_table_info('Recommendations') WHERE name='OptionPrice'";
        var optionPriceExists = await db.ExecuteAsync<long>(columnCheckSql) > 0;

        if (!optionPriceExists)
        {
            await db.ExecuteAsync("ALTER TABLE Recommendations ADD COLUMN OptionPrice DECIMAL NULL");
        }

        // Check if Volume column exists in Recommendations table
        columnCheckSql = "SELECT COUNT(*) FROM pragma_table_info('Recommendations') WHERE name='Volume'";
        var volumeExists = await db.ExecuteAsync<long>(columnCheckSql) > 0;

        if (!volumeExists)
        {
            await db.ExecuteAsync("ALTER TABLE Recommendations ADD COLUMN Volume INTEGER NULL");
        }

        // Check if OpenInterest column exists in Recommendations table
        columnCheckSql = "SELECT COUNT(*) FROM pragma_table_info('Recommendations') WHERE name='OpenInterest'";
        var openInterestExists = await db.ExecuteAsync<long>(columnCheckSql) > 0;

        if (!openInterestExists)
        {
            await db.ExecuteAsync("ALTER TABLE Recommendations ADD COLUMN OpenInterest INTEGER NULL");
        }
    }

    private static async Task CreateIndexesAsync(TradingDbContext db)
    {
        // Create unique index on (Symbol, ReportDate) for CompanyFinancials
        // This prevents duplicate entries for the same company and reporting period
        var uniqueIndexSql = @"
            CREATE UNIQUE INDEX IF NOT EXISTS IX_CompanyFinancials_Symbol_ReportDate
            ON CompanyFinancials (Symbol, ReportDate)";
        await db.ExecuteAsync(uniqueIndexSql);

        // Create index on PiotroskiFScore for fast filtering (F-Score > 7)
        var fscoreIndexSql = @"
            CREATE INDEX IF NOT EXISTS IX_CompanyFinancials_PiotroskiFScore
            ON CompanyFinancials (PiotroskiFScore)";
        await db.ExecuteAsync(fscoreIndexSql);

        // Create index on FetchedAt for checking data staleness
        var fetchedAtIndexSql = @"
            CREATE INDEX IF NOT EXISTS IX_CompanyFinancials_FetchedAt
            ON CompanyFinancials (FetchedAt)";
        await db.ExecuteAsync(fetchedAtIndexSql);

        // ==================== StockData Indexes ====================

        // Create unique index on Symbol for StockData (one record per symbol)
        var stockDataSymbolIndexSql = @"
            CREATE UNIQUE INDEX IF NOT EXISTS IX_StockData_Symbol
            ON StockData (Symbol)";
        await db.ExecuteAsync(stockDataSymbolIndexSql);

        // Create index on ModificationTime for retention cleanup and sorting
        var stockDataModTimeIndexSql = @"
            CREATE INDEX IF NOT EXISTS IX_StockData_ModificationTime
            ON StockData (ModificationTime DESC)";
        await db.ExecuteAsync(stockDataModTimeIndexSql);

        // Create index on PiotroskiFScore for pre-filtering (F-Score > 7)
        var stockDataFScoreIndexSql = @"
            CREATE INDEX IF NOT EXISTS IX_StockData_PiotroskiFScore
            ON StockData (PiotroskiFScore DESC)";
        await db.ExecuteAsync(stockDataFScoreIndexSql);

        // Create index on Confidence for sorting recommendations
        var stockDataConfidenceIndexSql = @"
            CREATE INDEX IF NOT EXISTS IX_StockData_Confidence
            ON StockData (Confidence DESC)";
        await db.ExecuteAsync(stockDataConfidenceIndexSql);
    }
}
