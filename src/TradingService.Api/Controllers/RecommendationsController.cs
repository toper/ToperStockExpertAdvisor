using Microsoft.AspNetCore.Mvc;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Api.Controllers;

public class RecommendationsController : BaseController
{
    private readonly ILogger<RecommendationsController> _logger;
    private readonly IStockDataRepository _stockDataRepository;

    public RecommendationsController(
        ILogger<RecommendationsController> logger,
        IStockDataRepository stockDataRepository)
    {
        _logger = logger;
        _stockDataRepository = stockDataRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecommendations([FromQuery] int? minDays = 14, [FromQuery] int? maxDays = 21)
    {
        var result = new Result<List<StockDataDto>>();

        try
        {
            _logger.LogInformation("Getting recommendations with minDays={MinDays}, maxDays={MaxDays}", minDays, maxDays);

            // Get all stock data with options
            var allStockData = await _stockDataRepository.GetWithOptionsDataAsync();

            // Filter by days to expiry
            var filtered = allStockData
                .Where(s => s.DaysToExpiry >= (minDays ?? 14) && s.DaysToExpiry <= (maxDays ?? 21))
                .OrderByDescending(s => s.Confidence)
                .ThenBy(s => s.Symbol)
                .ToList();

            result.Data = filtered.Select(MapStockDataToDto).ToList();

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
        var result = new Result<StockDataDto?>();

        try
        {
            _logger.LogInformation("Getting recommendation for symbol: {Symbol}", symbol);

            var stockData = await _stockDataRepository.GetBySymbolAsync(symbol);

            if (stockData == null)
            {
                return NotFound(new { message = $"No data found for symbol {symbol}" });
            }

            result.Data = MapStockDataToDto(stockData);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendation for symbol {Symbol}", symbol);
            result.Errors.Add("Error retrieving recommendation");
            return InternalServerError(result);
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var result = new GridResult<List<StockDataDto>>();

        try
        {
            _logger.LogInformation("Getting active recommendations");

            // Get all stock data with options data (Confidence not null)
            var stockDataList = await _stockDataRepository.GetWithOptionsDataAsync();
            var dtoList = stockDataList.Select(MapStockDataToDto).ToList();

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

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            _logger.LogInformation("Getting recommendations statistics");

            // Get healthy stocks count (F-Score >= 7)
            var healthyCount = await _stockDataRepository.GetHealthyCountAsync(7.0m);

            var stats = new
            {
                HealthyStocksCount = healthyCount,
                MinFScore = 7.0m
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations statistics");
            return InternalServerError(new { error = "Error retrieving statistics" });
        }
    }

    private static StockDataDto MapStockDataToDto(Data.Entities.StockData entity) => new()
    {
        Id = entity.Id,
        Symbol = entity.Symbol,
        ModificationTime = entity.ModificationTime,

        // SimFin metrics
        PiotroskiFScore = entity.PiotroskiFScore,
        AltmanZScore = entity.AltmanZScore,
        ROA = entity.ROA,
        DebtToEquity = entity.DebtToEquity,
        CurrentRatio = entity.CurrentRatio,
        MarketCapBillions = entity.MarketCapBillions,

        // Options data
        CurrentPrice = entity.CurrentPrice,
        StrikePrice = entity.StrikePrice,
        Expiry = entity.Expiry,
        DaysToExpiry = entity.DaysToExpiry,
        Premium = entity.Premium,
        Breakeven = entity.Breakeven,
        Confidence = entity.Confidence,
        ExpectedGrowthPercent = entity.ExpectedGrowthPercent,
        StrategyName = entity.StrategyName,
        ExanteSymbol = entity.ExanteSymbol,
        OptionPrice = entity.OptionPrice,
        Volume = entity.Volume,
        OpenInterest = entity.OpenInterest,

        // Calculated fields
        PotentialReturn = entity.Premium.HasValue && entity.CurrentPrice.HasValue
            ? (entity.Premium.Value / entity.CurrentPrice.Value) * 100
            : 0,
        OtmPercent = entity.StrikePrice.HasValue && entity.CurrentPrice.HasValue
            ? ((entity.CurrentPrice.Value - entity.StrikePrice.Value) / entity.CurrentPrice.Value) * 100
            : 0
    };
}
