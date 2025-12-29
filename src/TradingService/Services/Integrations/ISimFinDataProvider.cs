namespace TradingService.Services.Integrations;

/// <summary>
/// Interface for SimFin data provider
/// </summary>
public interface ISimFinDataProvider
{
    /// <summary>
    /// Fetches fundamental data for a given ticker symbol
    /// Includes both current period and previous period (for year-over-year comparisons)
    /// </summary>
    Task<SimFinCompanyData?> GetCompanyDataAsync(
        string ticker,
        CancellationToken cancellationToken = default);
}
