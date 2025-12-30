using TradingService.Data.Entities;

namespace TradingService.Services.Interfaces;

/// <summary>
/// Repository for managing company financial data from SimFin
/// </summary>
public interface ICompanyFinancialRepository
{
    /// <summary>
    /// Get financial data for a specific symbol (most recent report)
    /// </summary>
    Task<CompanyFinancial?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all companies with F-Score above the specified threshold
    /// </summary>
    /// <param name="minFScore">Minimum F-Score threshold (e.g., 7)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of companies with F-Score >= minFScore</returns>
    Task<List<CompanyFinancial>> GetByFScoreThresholdAsync(decimal minFScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk insert or update financial data for multiple companies
    /// Uses UPSERT logic based on unique index (Symbol, ReportDate)
    /// </summary>
    Task BulkInsertOrUpdateAsync(IEnumerable<CompanyFinancial> financials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of companies with F-Score above threshold
    /// </summary>
    Task<int> GetCountByFScoreAsync(decimal minFScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if financial data is stale (older than specified max age)
    /// </summary>
    /// <param name="maxAge">Maximum age (e.g., 7 days)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if data is stale or missing, false if fresh</returns>
    Task<bool> IsDataStaleAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all companies ordered by F-Score descending
    /// </summary>
    Task<List<CompanyFinancial>> GetAllOrderedByFScoreAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete financial data older than specified date
    /// </summary>
    Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
