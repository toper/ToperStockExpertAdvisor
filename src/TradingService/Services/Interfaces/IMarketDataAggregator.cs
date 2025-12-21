using TradingService.Models;

namespace TradingService.Services.Interfaces;

public interface IMarketDataAggregator
{
    Task<AggregatedMarketData> GetFullMarketDataAsync(string symbol);
}
