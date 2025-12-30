using LinqToDB;
using Microsoft.Extensions.Logging;
using TradingService.Data;
using TradingService.Data.Entities;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Repositories;

/// <summary>
/// Repository for managing unified stock data (SimFin + Exante)
/// Implements UPSERT pattern - single record per symbol
/// </summary>
public class StockDataRepository : IStockDataRepository
{
    private readonly IDbContextFactory _dbContextFactory;
    private readonly ILogger<StockDataRepository> _logger;

    public StockDataRepository(
        IDbContextFactory dbContextFactory,
        ILogger<StockDataRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    // ==================== Read Operations ====================

    public async Task<StockData?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var stockData = await db.StockData
                .Where(s => s.Symbol == symbol)
                .FirstOrDefaultAsync(cancellationToken);

            return stockData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock data for symbol {Symbol}", symbol);
            throw;
        }
    }

    public async Task<List<StockData>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var allData = await db.StockData
                .OrderByDescending(s => s.ModificationTime)
                .ToListAsync(cancellationToken);

            return allData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all stock data");
            throw;
        }
    }

    public async Task<List<StockData>> GetHealthySymbolsAsync(decimal minFScore, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var healthyStocks = await db.StockData
                .Where(s => s.PiotroskiFScore != null && s.PiotroskiFScore >= minFScore)
                .OrderByDescending(s => s.PiotroskiFScore)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Found {Count} stocks with F-Score >= {MinFScore}",
                healthyStocks.Count, minFScore);

            return healthyStocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting healthy symbols with F-Score >= {MinFScore}", minFScore);
            throw;
        }
    }

    public async Task<List<StockData>> GetWithOptionsDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var withOptions = await db.StockData
                .Where(s => s.Confidence != null)
                .OrderByDescending(s => s.Confidence)
                .ThenBy(s => s.Symbol)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} stocks with options data", withOptions.Count);

            return withOptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stocks with options data");
            throw;
        }
    }

    // ==================== Write Operations (UPSERT by Symbol) ====================

    public async Task UpsertSimFinDataAsync(StockData data, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var existing = await db.StockData
                .Where(s => s.Symbol == data.Symbol)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing != null)
            {
                // UPDATE - SimFin fields only
                existing.ModificationTime = DateTime.UtcNow;
                existing.SimFinUpdatedAt = DateTime.UtcNow;
                existing.ReportDate = data.ReportDate;
                existing.PiotroskiFScore = data.PiotroskiFScore;
                existing.AltmanZScore = data.AltmanZScore;
                existing.ROA = data.ROA;
                existing.DebtToEquity = data.DebtToEquity;
                existing.CurrentRatio = data.CurrentRatio;
                existing.MarketCapBillions = data.MarketCapBillions;
                existing.TotalAssets = data.TotalAssets;
                existing.TotalLiabilities = data.TotalLiabilities;
                existing.TotalEquity = data.TotalEquity;
                existing.Revenue = data.Revenue;
                existing.NetIncome = data.NetIncome;
                existing.OperatingCashFlow = data.OperatingCashFlow;
                existing.SharesOutstanding = data.SharesOutstanding;

                await db.UpdateAsync(existing, token: cancellationToken);
                _logger.LogDebug("Updated SimFin data for {Symbol}", data.Symbol);
            }
            else
            {
                // INSERT - new record with SimFin data
                data.ModificationTime = DateTime.UtcNow;
                data.SimFinUpdatedAt = DateTime.UtcNow;
                await db.InsertAsync(data, token: cancellationToken);
                _logger.LogDebug("Inserted new SimFin data for {Symbol}", data.Symbol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting SimFin data for symbol {Symbol}", data.Symbol);
            throw;
        }
    }

    public async Task UpsertExanteDataAsync(StockData data, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var existing = await db.StockData
                .Where(s => s.Symbol == data.Symbol)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing != null)
            {
                // UPDATE - Exante + Yahoo Finance fields only
                existing.ModificationTime = DateTime.UtcNow;
                existing.ExanteUpdatedAt = DateTime.UtcNow;
                existing.CurrentPrice = data.CurrentPrice;
                existing.StrikePrice = data.StrikePrice;
                existing.Expiry = data.Expiry;
                existing.DaysToExpiry = data.DaysToExpiry;
                existing.Premium = data.Premium;
                existing.Breakeven = data.Breakeven;
                existing.Confidence = data.Confidence;
                existing.ExpectedGrowthPercent = data.ExpectedGrowthPercent;
                existing.StrategyName = data.StrategyName;
                existing.ExanteSymbol = data.ExanteSymbol;
                existing.OptionPrice = data.OptionPrice;
                existing.Volume = data.Volume;
                existing.OpenInterest = data.OpenInterest;

                await db.UpdateAsync(existing, token: cancellationToken);
                _logger.LogDebug("Updated Exante data for {Symbol}", data.Symbol);
            }
            else
            {
                // INSERT - fallback case (symbol not in SimFin bulk data)
                data.ModificationTime = DateTime.UtcNow;
                data.ExanteUpdatedAt = DateTime.UtcNow;
                await db.InsertAsync(data, token: cancellationToken);
                _logger.LogWarning(
                    "Inserted new Exante data for {Symbol} (no SimFin data exists - young company or missing from CSV)",
                    data.Symbol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting Exante data for symbol {Symbol}", data.Symbol);
            throw;
        }
    }

    public async Task BulkUpsertSimFinDataAsync(IEnumerable<StockData> dataList, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var stockDataList = dataList.ToList();
            if (!stockDataList.Any())
            {
                _logger.LogWarning("No SimFin data to upsert");
                return;
            }

            var inserted = 0;
            var updated = 0;
            var fetchedAt = DateTime.UtcNow;

            foreach (var data in stockDataList)
            {
                // Check if record exists by Symbol
                var existing = await db.StockData
                    .Where(s => s.Symbol == data.Symbol)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing != null)
                {
                    // UPDATE - SimFin fields only
                    existing.ModificationTime = fetchedAt;
                    existing.SimFinUpdatedAt = fetchedAt;
                    existing.ReportDate = data.ReportDate;
                    existing.PiotroskiFScore = data.PiotroskiFScore;
                    existing.AltmanZScore = data.AltmanZScore;
                    existing.ROA = data.ROA;
                    existing.DebtToEquity = data.DebtToEquity;
                    existing.CurrentRatio = data.CurrentRatio;
                    existing.MarketCapBillions = data.MarketCapBillions;
                    existing.TotalAssets = data.TotalAssets;
                    existing.TotalLiabilities = data.TotalLiabilities;
                    existing.TotalEquity = data.TotalEquity;
                    existing.Revenue = data.Revenue;
                    existing.NetIncome = data.NetIncome;
                    existing.OperatingCashFlow = data.OperatingCashFlow;
                    existing.SharesOutstanding = data.SharesOutstanding;

                    await db.UpdateAsync(existing, token: cancellationToken);
                    updated++;
                }
                else
                {
                    // INSERT - new record
                    data.ModificationTime = fetchedAt;
                    data.SimFinUpdatedAt = fetchedAt;
                    await db.InsertAsync(data, token: cancellationToken);
                    inserted++;
                }

                if ((inserted + updated) % 100 == 0)
                {
                    _logger.LogDebug(
                        "Processed {Count}/{Total} stock data records ({Inserted} inserted, {Updated} updated)",
                        inserted + updated, stockDataList.Count, inserted, updated);
                }
            }

            _logger.LogInformation(
                "Bulk processed {Total} SimFin stock data records ({Inserted} inserted, {Updated} updated)",
                inserted + updated, inserted, updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk upserting SimFin stock data");
            throw;
        }
    }

    // ==================== Cleanup ====================

    public async Task DeleteStaleRecordsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var cutoffDate = DateTime.UtcNow - maxAge;

            var deletedCount = await db.StockData
                .Where(s => s.ModificationTime < cutoffDate)
                .DeleteAsync(cancellationToken);

            _logger.LogInformation(
                "Deleted {Count} stale stock records (not modified since {CutoffDate})",
                deletedCount, cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting stale stock data");
            throw;
        }
    }

    // ==================== Stats ====================

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var count = await db.StockData.CountAsync(cancellationToken);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total stock data count");
            throw;
        }
    }

    public async Task<int> GetHealthyCountAsync(decimal minFScore, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var count = await db.StockData
                .Where(s => s.PiotroskiFScore != null && s.PiotroskiFScore >= minFScore)
                .CountAsync(cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting healthy stocks with F-Score >= {MinFScore}", minFScore);
            throw;
        }
    }
}
