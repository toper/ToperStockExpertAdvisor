using TradingService.Data.Entities;
using TradingService.Models;

namespace TradingService.Services.Interfaces;

public interface IStrategy
{
    string Name { get; }
    string Description { get; }
    int TargetExpiryMinDays { get; }
    int TargetExpiryMaxDays { get; }
    Task<IEnumerable<PutRecommendation>> AnalyzeAsync(
        AggregatedMarketData data,
        CancellationToken cancellationToken = default);
}
