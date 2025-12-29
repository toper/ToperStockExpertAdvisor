using LinqToDB;
using Microsoft.Extensions.Logging;
using TradingService.Data;
using TradingService.Data.Entities;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Repositories;

public class RecommendationRepository : IRecommendationRepository
{
    private readonly IDbContextFactory _dbContextFactory;
    private readonly ILogger<RecommendationRepository> _logger;

    public RecommendationRepository(
        IDbContextFactory dbContextFactory,
        ILogger<RecommendationRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<PutRecommendation>> GetActiveRecommendationsAsync()
    {
        try
        {
            using var db = _dbContextFactory.Create();

            // Get the latest scan date
            var latestScanDate = await db.Recommendations
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.ScannedAt)
                .Select(r => r.ScannedAt)
                .FirstOrDefaultAsync();

            if (latestScanDate == default)
            {
                _logger.LogInformation("No active recommendations found");
                return Enumerable.Empty<PutRecommendation>();
            }

            // Return only recommendations from the latest scan (within 1 minute window)
            var scanWindow = latestScanDate.AddMinutes(-1);

            var recommendations = await db.Recommendations
                .Where(r => r.IsActive && r.ScannedAt >= scanWindow)
                .OrderByDescending(r => r.Confidence)
                .ThenBy(r => r.Symbol)
                .ToListAsync();

            _logger.LogInformation(
                "Retrieved {Count} active recommendations from latest scan at {ScanDate:yyyy-MM-dd HH:mm:ss}",
                recommendations.Count,
                latestScanDate);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active recommendations");
            throw;
        }
    }

    public async Task<IEnumerable<PutRecommendation>> GetBySymbolAsync(string symbol)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var recommendations = await db.Recommendations
                .Where(r => r.Symbol == symbol && r.IsActive)
                .OrderByDescending(r => r.ScannedAt)
                .ThenBy(r => r.DaysToExpiry)
                .ToListAsync();

            _logger.LogInformation(
                "Retrieved {Count} active recommendations for symbol {Symbol}",
                recommendations.Count, symbol);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recommendations for symbol {Symbol}", symbol);
            throw;
        }
    }

    public async Task<IEnumerable<PutRecommendation>> GetShortTermRecommendationsAsync(
        int minDays = 14,
        int maxDays = 21)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var today = DateTime.UtcNow.Date;

            var recommendations = await db.Recommendations
                .Where(r => r.IsActive &&
                           r.DaysToExpiry >= minDays &&
                           r.DaysToExpiry <= maxDays)
                .OrderByDescending(r => r.Confidence)
                .ThenBy(r => r.DaysToExpiry)
                .ThenBy(r => r.Symbol)
                .ToListAsync();

            _logger.LogInformation(
                "Retrieved {Count} short-term recommendations ({MinDays}-{MaxDays} days)",
                recommendations.Count, minDays, maxDays);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving short-term recommendations for {MinDays}-{MaxDays} days",
                minDays, maxDays);
            throw;
        }
    }

    public async Task<int> AddRangeAsync(IEnumerable<PutRecommendation> recommendations)
    {
        try
        {
            var recommendationsList = recommendations.ToList();

            if (!recommendationsList.Any())
            {
                _logger.LogWarning("No recommendations to add");
                return 0;
            }

            using var db = _dbContextFactory.Create();

            // Deactivate existing recommendations for the same symbols
            var symbols = recommendationsList.Select(r => r.Symbol).Distinct().ToList();

            await db.Recommendations
                .Where(r => symbols.Contains(r.Symbol) && r.IsActive)
                .Set(r => r.IsActive, false)
                .UpdateAsync();

            // Insert new recommendations one by one
            var insertedCount = 0;
            foreach (var recommendation in recommendationsList)
            {
                // Ensure IsActive is set to true
                recommendation.IsActive = true;
                await db.InsertAsync(recommendation);
                insertedCount++;
            }

            // Explicitly set IsActive=true for recommendations just inserted
            // (they have ScannedAt within last 10 seconds)
            var now = DateTime.UtcNow;
            var cutoffTime = now.AddSeconds(-10);
            var affectedCount = await db.Recommendations
                .Where(r => symbols.Contains(r.Symbol) && r.ScannedAt >= cutoffTime)
                .Set(r => r.IsActive, true)
                .UpdateAsync();

            _logger.LogInformation(
                "Updated IsActive=true for {Count} recently scanned recommendations",
                affectedCount);

            _logger.LogInformation(
                "Added {Count} new recommendations for symbols: {Symbols}",
                insertedCount, string.Join(", ", symbols));

            return insertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding recommendations");
            throw;
        }
    }

    public async Task DeactivateOldRecommendationsAsync(DateTime before)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var deactivatedCount = await db.Recommendations
                .Where(r => r.IsActive &&
                           (r.Expiry < before || r.ScannedAt < before.AddDays(-7)))
                .Set(r => r.IsActive, false)
                .UpdateAsync();

            if (deactivatedCount > 0)
            {
                _logger.LogInformation(
                    "Deactivated {Count} old recommendations (before {Date:yyyy-MM-dd} or expired)",
                    deactivatedCount, before);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deactivating old recommendations before {Date:yyyy-MM-dd}",
                before);
            throw;
        }
    }
}