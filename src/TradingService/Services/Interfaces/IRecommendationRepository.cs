using TradingService.Data.Entities;

namespace TradingService.Services.Interfaces;

public interface IRecommendationRepository
{
    Task<IEnumerable<PutRecommendation>> GetActiveRecommendationsAsync();
    Task<IEnumerable<PutRecommendation>> GetBySymbolAsync(string symbol);
    Task<IEnumerable<PutRecommendation>> GetShortTermRecommendationsAsync(int minDays = 14, int maxDays = 21);
    Task<int> AddRangeAsync(IEnumerable<PutRecommendation> recommendations);
    Task DeactivateOldRecommendationsAsync(DateTime before);
}
