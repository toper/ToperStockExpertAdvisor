using Microsoft.Extensions.Logging;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Integrations;

public class MarketDataAggregator : IMarketDataAggregator
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IOptionsDataProvider _optionsDataProvider;
    private readonly ILogger<MarketDataAggregator> _logger;

    public MarketDataAggregator(
        IMarketDataProvider marketDataProvider,
        IOptionsDataProvider optionsDataProvider,
        ILogger<MarketDataAggregator> logger)
    {
        _marketDataProvider = marketDataProvider;
        _optionsDataProvider = optionsDataProvider;
        _logger = logger;
    }

    public async Task<AggregatedMarketData> GetFullMarketDataAsync(string symbol)
    {
        try
        {
            _logger.LogInformation("Aggregating full market data for {Symbol}", symbol);

            // Fetch all data components in parallel for better performance
            var marketDataTask = _marketDataProvider.GetMarketDataAsync(symbol);
            var trendAnalysisTask = _marketDataProvider.AnalyzeTrendAsync(symbol, 21);
            var shortTermPutOptionsTask = _optionsDataProvider.GetShortTermPutOptionsAsync(symbol, 14, 21);
            var dividendInfoTask = _marketDataProvider.GetDividendInfoAsync(symbol);

            // Wait for all tasks to complete
            await Task.WhenAll(marketDataTask, trendAnalysisTask, shortTermPutOptionsTask, dividendInfoTask);

            var marketData = await marketDataTask;
            var trendAnalysis = await trendAnalysisTask;
            var shortTermPutOptions = await shortTermPutOptionsTask;
            var dividendInfo = await dividendInfoTask;

            // Validate data quality
            if (marketData == null)
            {
                _logger.LogWarning("No market data available for {Symbol}", symbol);
            }

            if (trendAnalysis == null || trendAnalysis.Confidence == 0)
            {
                _logger.LogWarning("Trend analysis failed or has low confidence for {Symbol}", symbol);
            }

            if (!shortTermPutOptions?.Any() ?? true)
            {
                _logger.LogWarning("No short-term PUT options available for {Symbol}", symbol);
            }

            // Create aggregated data object
            var aggregatedData = new AggregatedMarketData
            {
                MarketData = marketData,
                TrendAnalysis = trendAnalysis,
                ShortTermPutOptions = shortTermPutOptions?.ToList() ?? new List<OptionContract>(),
                DividendInfo = dividendInfo
            };

            _logger.LogInformation(
                "Successfully aggregated market data for {Symbol}. " +
                "Market data: {HasMarketData}, Trend: {TrendDirection} ({Confidence:P}), " +
                "PUT options: {PutOptionsCount}, Dividend yield: {DividendYield:P}",
                symbol,
                marketData != null,
                trendAnalysis?.Direction,
                trendAnalysis?.Confidence ?? 0,
                aggregatedData.ShortTermPutOptions.Count,
                dividendInfo?.DividendYield ?? 0);

            return aggregatedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating market data for {Symbol}", symbol);

            // Return partial data even if some components fail
            return new AggregatedMarketData
            {
                MarketData = null,
                TrendAnalysis = new TrendAnalysis
                {
                    Symbol = symbol,
                    ExpectedGrowthPercent = 0,
                    TrendStrength = 0,
                    Direction = TrendDirection.Sideways,
                    Confidence = 0,
                    AnalysisPeriodDays = 21
                },
                ShortTermPutOptions = new List<OptionContract>(),
                DividendInfo = null
            };
        }
    }
}