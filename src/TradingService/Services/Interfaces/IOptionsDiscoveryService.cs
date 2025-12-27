namespace TradingService.Services.Interfaces;

/// <summary>
/// Service for discovering underlying symbols from available options on a broker platform
/// </summary>
public interface IOptionsDiscoveryService
{
    /// <summary>
    /// Discover underlying symbols from available options based on liquidity filters
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of unique underlying symbols that have liquid options available</returns>
    Task<IEnumerable<string>> DiscoverUnderlyingSymbolsAsync(
        CancellationToken cancellationToken = default);
}
