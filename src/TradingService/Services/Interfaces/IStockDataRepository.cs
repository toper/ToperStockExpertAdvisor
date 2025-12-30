using TradingService.Data.Entities;

namespace TradingService.Services.Interfaces;

/// <summary>
/// Repository for managing unified stock data (SimFin + Exante)
/// Uses UPSERT pattern - single record per symbol
/// </summary>
public interface IStockDataRepository
{
    // ==================== Read Operations ====================

    /// <summary>
    /// Get stock data by symbol
    /// </summary>
    Task<StockData?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stock data records
    /// </summary>
    Task<List<StockData>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stocks with F-Score >= threshold
    /// Returns most recent data per symbol
    /// </summary>
    Task<List<StockData>> GetHealthySymbolsAsync(decimal minFScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stocks that have options data (Confidence not null)
    /// Used for displaying active recommendations
    /// </summary>
    Task<List<StockData>> GetWithOptionsDataAsync(CancellationToken cancellationToken = default);

    // ==================== Write Operations (UPSERT by Symbol) ====================

    /// <summary>
    /// Insert or update SimFin financial data
    /// Updates: ModificationTime, SimFinUpdatedAt, and all SimFin fields
    /// Preserves: Exante data fields
    /// </summary>
    Task UpsertSimFinDataAsync(StockData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert or update Exante options data
    /// Updates: ModificationTime, ExanteUpdatedAt, CurrentPrice, and all Exante fields
    /// Preserves: SimFin data fields
    /// </summary>
    Task UpsertExanteDataAsync(StockData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk insert or update SimFin data
    /// More efficient than calling UpsertSimFinDataAsync in a loop
    /// </summary>
    Task BulkUpsertSimFinDataAsync(IEnumerable<StockData> dataList, CancellationToken cancellationToken = default);

    // ==================== Cleanup ====================

    /// <summary>
    /// Delete records not modified within maxAge timespan
    /// Used for 90-day retention policy
    /// </summary>
    Task DeleteStaleRecordsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    // ==================== Stats ====================

    /// <summary>
    /// Get total count of all stock records
    /// </summary>
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of stocks with F-Score >= threshold
    /// </summary>
    Task<int> GetHealthyCountAsync(decimal minFScore, CancellationToken cancellationToken = default);
}
