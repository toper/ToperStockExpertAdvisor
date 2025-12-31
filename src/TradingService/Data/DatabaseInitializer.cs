using LinqToDB;
using LinqToDB.Data;
using TradingService.Data.Entities;

namespace TradingService.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(TradingDbContext db)
    {
        // Active tables (used in production)
        await db.CreateTableAsync<ScanLog>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<WatchlistItem>(tableOptions: TableOptions.CreateIfNotExists);
        await db.CreateTableAsync<StockData>(tableOptions: TableOptions.CreateIfNotExists);

        // REMOVED: PutRecommendation (Recommendations table) - replaced by StockData
        // REMOVED: CompanyFinancial - replaced by StockData

        // Cleanup: Drop legacy tables if they exist
        await DropLegacyTablesAsync(db);

        // Add new columns to StockData for complete financial metrics
        await MigrateStockDataSchemaAsync(db);

        await CreateIndexesAsync(db);
    }

    private static async Task DropLegacyTablesAsync(TradingDbContext db)
    {
        try
        {
            // Drop Recommendations table (replaced by StockData)
            await db.ExecuteAsync("DROP TABLE IF EXISTS Recommendations");

            // Drop CompanyFinancials table (replaced by StockData)
            await db.ExecuteAsync("DROP TABLE IF EXISTS CompanyFinancials");

            // Drop related indexes
            await db.ExecuteAsync("DROP INDEX IF EXISTS IX_CompanyFinancials_Symbol_ReportDate");
            await db.ExecuteAsync("DROP INDEX IF EXISTS IX_CompanyFinancials_PiotroskiFScore");
            await db.ExecuteAsync("DROP INDEX IF EXISTS IX_CompanyFinancials_FetchedAt");
        }
        catch (Exception ex)
        {
            // Log but don't fail if tables don't exist or can't be dropped
            Console.WriteLine($"Warning: Failed to drop legacy tables: {ex.Message}");
        }
    }

    private static async Task MigrateStockDataSchemaAsync(TradingDbContext db)
    {
        try
        {
            // Add new financial metrics columns for complete F-Score and Z-Score calculations
            var newColumns = new[]
            {
                ("RetainedEarnings", "DECIMAL NULL"),
                ("TotalDebt", "DECIMAL NULL"),
                ("EBITDA", "DECIMAL NULL"),
                ("CurrentAssets", "DECIMAL NULL"),
                ("CurrentLiabilities", "DECIMAL NULL")
            };

            foreach (var (columnName, columnType) in newColumns)
            {
                var checkSql = $"SELECT COUNT(*) FROM pragma_table_info('StockData') WHERE name='{columnName}'";
                var exists = await db.ExecuteAsync<long>(checkSql) > 0;

                if (!exists)
                {
                    await db.ExecuteAsync($"ALTER TABLE StockData ADD COLUMN {columnName} {columnType}");
                    Console.WriteLine($"Added column {columnName} to StockData table");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to migrate StockData schema: {ex.Message}");
        }
    }


    private static async Task CreateIndexesAsync(TradingDbContext db)
    {
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
