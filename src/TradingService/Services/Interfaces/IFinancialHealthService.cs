using TradingService.Models;

namespace TradingService.Services.Interfaces;

/// <summary>
/// Service for calculating financial health metrics
/// </summary>
public interface IFinancialHealthService
{
    /// <summary>
    /// Calculates Piotroski F-Score and Altman Z-Score for a given stock
    /// Returns null for metrics that cannot be calculated due to missing data
    /// </summary>
    Task<FinancialHealthMetrics> CalculateMetricsAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if a stock passes the minimum financial health requirements
    /// </summary>
    bool MeetsHealthRequirements(FinancialHealthMetrics metrics);

    /// <summary>
    /// Calculates financial health metrics for multiple symbols in parallel
    /// Returns empty metrics for symbols that fail or timeout
    /// </summary>
    Task<Dictionary<string, FinancialHealthMetrics>> CalculateMetricsBatchAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);
}
