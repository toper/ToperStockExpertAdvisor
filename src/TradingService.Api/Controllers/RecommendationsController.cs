using Microsoft.AspNetCore.Mvc;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Api.Controllers;

public class RecommendationsController : BaseController
{
    private readonly ILogger<RecommendationsController> _logger;
    private readonly IRecommendationRepository _recommendationRepository;

    public RecommendationsController(
        ILogger<RecommendationsController> logger,
        IRecommendationRepository recommendationRepository)
    {
        _logger = logger;
        _recommendationRepository = recommendationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecommendations([FromQuery] int? minDays = 14, [FromQuery] int? maxDays = 21)
    {
        var result = new Result<List<PutRecommendationDto>>();

        try
        {
            _logger.LogInformation("Getting recommendations with minDays={MinDays}, maxDays={MaxDays}", minDays, maxDays);

            var recommendations = await _recommendationRepository.GetShortTermRecommendationsAsync(
                minDays ?? 14, maxDays ?? 21);

            result.Data = recommendations.Select(MapToDto).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations");
            result.Errors.Add("Error retrieving recommendations");
            return InternalServerError(result);
        }
    }

    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetBySymbol(string symbol)
    {
        var result = new Result<List<PutRecommendationDto>>();

        try
        {
            _logger.LogInformation("Getting recommendations for symbol: {Symbol}", symbol);

            var recommendations = await _recommendationRepository.GetBySymbolAsync(symbol);
            result.Data = recommendations.Select(MapToDto).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for symbol {Symbol}", symbol);
            result.Errors.Add("Error retrieving recommendations");
            return InternalServerError(result);
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var result = new GridResult<List<PutRecommendationDto>>();

        try
        {
            _logger.LogInformation("Getting active recommendations");

            var recommendations = await _recommendationRepository.GetActiveRecommendationsAsync();
            var dtoList = recommendations.Select(MapToDto).ToList();

            result.Data = dtoList;
            result.TotalCount = dtoList.Count;

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active recommendations");
            result.Errors.Add("Error retrieving active recommendations");
            return InternalServerError(result);
        }
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedTestData()
    {
        if (!Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            return NotFound();
        }

        var testData = new List<Data.Entities.PutRecommendation>
        {
            new() { Symbol = "SPY", CurrentPrice = 595.50m, StrikePrice = 570m, Expiry = DateTime.UtcNow.AddDays(14), DaysToExpiry = 14, Premium = 2.45m, Breakeven = 567.55m, Confidence = 0.78m, ExpectedGrowthPercent = 2.5m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
            new() { Symbol = "SPY", CurrentPrice = 595.50m, StrikePrice = 565m, Expiry = DateTime.UtcNow.AddDays(21), DaysToExpiry = 21, Premium = 3.10m, Breakeven = 561.90m, Confidence = 0.72m, ExpectedGrowthPercent = 2.8m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
            new() { Symbol = "QQQ", CurrentPrice = 520.25m, StrikePrice = 500m, Expiry = DateTime.UtcNow.AddDays(14), DaysToExpiry = 14, Premium = 2.80m, Breakeven = 497.20m, Confidence = 0.82m, ExpectedGrowthPercent = 3.1m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
            new() { Symbol = "AAPL", CurrentPrice = 248.50m, StrikePrice = 240m, Expiry = DateTime.UtcNow.AddDays(18), DaysToExpiry = 18, Premium = 1.95m, Breakeven = 238.05m, Confidence = 0.75m, ExpectedGrowthPercent = 2.2m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
            new() { Symbol = "MSFT", CurrentPrice = 438.20m, StrikePrice = 420m, Expiry = DateTime.UtcNow.AddDays(16), DaysToExpiry = 16, Premium = 3.25m, Breakeven = 416.75m, Confidence = 0.68m, ExpectedGrowthPercent = 1.9m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
            new() { Symbol = "GOOGL", CurrentPrice = 192.80m, StrikePrice = 185m, Expiry = DateTime.UtcNow.AddDays(14), DaysToExpiry = 14, Premium = 1.45m, Breakeven = 183.55m, Confidence = 0.71m, ExpectedGrowthPercent = 2.4m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
            new() { Symbol = "NVDA", CurrentPrice = 134.50m, StrikePrice = 125m, Expiry = DateTime.UtcNow.AddDays(21), DaysToExpiry = 21, Premium = 2.15m, Breakeven = 122.85m, Confidence = 0.85m, ExpectedGrowthPercent = 4.2m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
            new() { Symbol = "NVDA", CurrentPrice = 134.50m, StrikePrice = 120m, Expiry = DateTime.UtcNow.AddDays(14), DaysToExpiry = 14, Premium = 1.80m, Breakeven = 118.20m, Confidence = 0.79m, ExpectedGrowthPercent = 3.8m, StrategyName = "ShortTermPutStrategy", ScannedAt = DateTime.UtcNow, IsActive = true },
        };

        await _recommendationRepository.AddRangeAsync(testData);
        _logger.LogInformation("Seeded {Count} test recommendations", testData.Count);

        return Ok(new { message = $"Seeded {testData.Count} test recommendations" });
    }

    private static PutRecommendationDto MapToDto(Data.Entities.PutRecommendation entity) => new()
    {
        Id = entity.Id,
        Symbol = entity.Symbol,
        CurrentPrice = entity.CurrentPrice,
        StrikePrice = entity.StrikePrice,
        Expiry = entity.Expiry,
        DaysToExpiry = entity.DaysToExpiry,
        Premium = entity.Premium,
        Breakeven = entity.Breakeven,
        Confidence = entity.Confidence,
        ExpectedGrowthPercent = entity.ExpectedGrowthPercent,
        StrategyName = entity.StrategyName,
        ScannedAt = entity.ScannedAt,
        PiotroskiFScore = entity.PiotroskiFScore,
        AltmanZScore = entity.AltmanZScore
    };
}
