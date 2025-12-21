using TradingService.Models;

namespace TradingService.Services.Interfaces;

public interface IMarketDataProvider
{
    Task<MarketData?> GetMarketDataAsync(string symbol);
    Task<IEnumerable<HistoricalQuote>> GetHistoricalDataAsync(string symbol, int days);
    Task<DividendInfo?> GetDividendInfoAsync(string symbol);
    Task<TrendAnalysis> AnalyzeTrendAsync(string symbol, int days = 21);
}
