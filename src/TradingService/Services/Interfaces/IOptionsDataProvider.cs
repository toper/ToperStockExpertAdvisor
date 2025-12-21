using TradingService.Models;

namespace TradingService.Services.Interfaces;

public interface IOptionsDataProvider
{
    Task<OptionsChain?> GetOptionsChainAsync(string symbol);
    Task<IEnumerable<OptionContract>> GetShortTermPutOptionsAsync(string symbol, int minDays = 14, int maxDays = 21);
}
