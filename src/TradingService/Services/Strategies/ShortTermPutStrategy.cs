using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data.Entities;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Strategies;

/// <summary>
/// Short-term PUT selling strategy focusing on 2-3 week expirations
/// Targets stocks with upward trend and high confidence levels
/// Filters by Piotroski F-Score and Altman Z-Score for financial health
/// </summary>
public class ShortTermPutStrategy : IStrategy
{
    private readonly ILogger<ShortTermPutStrategy> _logger;
    private readonly StrategySettings _settings;
    private readonly IFinancialHealthService _financialHealthService;

    public string Name => "ShortTermPut";
    public string Description => "Sells PUT options 2-3 weeks out on financially healthy stocks with strong upward trends";
    public int TargetExpiryMinDays => _settings.MinExpiryDays;
    public int TargetExpiryMaxDays => _settings.MaxExpiryDays;

    public ShortTermPutStrategy(
        ILogger<ShortTermPutStrategy> logger,
        IOptions<AppSettings> appSettings,
        IFinancialHealthService financialHealthService)
    {
        _logger = logger;
        _settings = appSettings.Value.Strategy;
        _financialHealthService = financialHealthService;
    }

    public async Task<IEnumerable<PutRecommendation>> AnalyzeAsync(
        AggregatedMarketData data,
        CancellationToken cancellationToken = default)
    {
        var recommendations = new List<PutRecommendation>();

        try
        {
            if (data.MarketData == null)
            {
                _logger.LogWarning("No market data available for {Symbol}", data.TrendAnalysis?.Symbol);
                return recommendations;
            }

            if (data.TrendAnalysis == null)
            {
                _logger.LogWarning("No trend analysis available for {Symbol}", data.MarketData.Symbol);
                return recommendations;
            }

            if (!data.ShortTermPutOptions.Any())
            {
                _logger.LogWarning("No short-term PUT options available for {Symbol}", data.MarketData.Symbol);
                return recommendations;
            }

            var symbol = data.MarketData.Symbol;
            var currentPrice = data.MarketData.CurrentPrice;

            // FIRST: Check financial health (Piotroski F-Score & Altman Z-Score)
            _logger.LogInformation("Checking financial health for {Symbol}", symbol);
            var healthMetrics = await _financialHealthService.CalculateMetricsAsync(symbol, cancellationToken);

            if (!_financialHealthService.MeetsHealthRequirements(healthMetrics))
            {
                _logger.LogInformation(
                    "Skipping {Symbol} - Does not meet financial health requirements. " +
                    "F-Score: {FScore}, Z-Score: {ZScore}",
                    symbol,
                    healthMetrics.PiotroskiFScore?.ToString("F1") ?? "N/A",
                    healthMetrics.AltmanZScore?.ToString("F2") ?? "N/A");
                return recommendations;
            }

            _logger.LogInformation(
                "{Symbol} passes financial health check - F-Score: {FScore}, Z-Score: {ZScore}",
                symbol,
                healthMetrics.PiotroskiFScore,
                healthMetrics.AltmanZScore);

            // Filter based on trend direction and confidence
            if (!ShouldAnalyze(data.TrendAnalysis))
            {
                _logger.LogInformation(
                    "Skipping {Symbol} - Trend: {Direction}, Confidence: {Confidence:P}",
                    symbol, data.TrendAnalysis.Direction, data.TrendAnalysis.Confidence);
                return recommendations;
            }

            // Analyze each PUT option
            foreach (var option in data.ShortTermPutOptions)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var recommendation = await AnalyzePutOptionAsync(
                    data, option, healthMetrics, cancellationToken);

                if (recommendation != null)
                {
                    recommendations.Add(recommendation);
                }
            }

            // Sort by confidence and return top recommendations
            var topRecommendations = recommendations
                .OrderByDescending(r => r.Confidence)
                .ThenBy(r => r.DaysToExpiry)
                .Take(1) // Top 1 recommendation per symbol (only best opportunity)
                .ToList();

            if (topRecommendations.Any())
            {
                _logger.LogInformation(
                    "Generated {Count} recommendations for {Symbol} " +
                    "(Top confidence: {TopConfidence:P}, Strike: ${Strike})",
                    topRecommendations.Count,
                    symbol,
                    topRecommendations.First().Confidence,
                    topRecommendations.First().StrikePrice);
            }

            return topRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing {Symbol} with ShortTermPutStrategy",
                data.MarketData?.Symbol ?? "Unknown");
            return recommendations;
        }
    }

    private bool ShouldAnalyze(TrendAnalysis trend)
    {
        // Only analyze stocks with upward or sideways trend and sufficient confidence
        return trend.Direction != TrendDirection.Down &&
               trend.Confidence >= _settings.MinConfidence;
    }

    private Task<PutRecommendation?> AnalyzePutOptionAsync(
        AggregatedMarketData data,
        OptionContract option,
        FinancialHealthMetrics healthMetrics,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var marketData = data.MarketData!;
                var trendAnalysis = data.TrendAnalysis!;
                var currentPrice = marketData.CurrentPrice;

                // Calculate safety margin (how far OTM the strike is)
                var safetyMargin = (currentPrice - option.Strike) / currentPrice;

                // Skip if strike is too close to current price (less than 5% OTM)
                if (safetyMargin < 0.05m)
                {
                    return null;
                }

                // Skip if strike is too far from current price (more than 20% OTM)
                if (safetyMargin > 0.20m)
                {
                    return null;
                }

                // Skip if delta is too high (closer to ITM, more risky)
                // For PUT options, delta is negative; -0.30 is safer than -0.40
                if (option.Delta < -0.30m)
                {
                    return null;
                }

                // Calculate annualized return
                var premium = option.Mid;
                var daysToExpiry = option.DaysToExpiry;
                var returnOnRisk = premium / option.Strike;
                var annualizedReturn = (returnOnRisk * 365m) / daysToExpiry;

                // Skip if annualized return is too low (less than 10%)
                if (annualizedReturn < 0.10m)
                {
                    return null;
                }

                // Calculate confidence score based on multiple factors
                var confidence = CalculateConfidence(
                    data, option, safetyMargin, annualizedReturn);

                // Only recommend if confidence meets minimum threshold
                if (confidence < _settings.MinConfidence)
                {
                    return null;
                }

                // Calculate breakeven point
                var breakeven = option.Strike - premium;

                return new PutRecommendation
                {
                    Symbol = marketData.Symbol,
                    CurrentPrice = currentPrice,
                    StrikePrice = option.Strike,
                    Expiry = option.Expiry,
                    DaysToExpiry = daysToExpiry,
                    Premium = premium,
                    Breakeven = breakeven,
                    Confidence = confidence,
                    ExpectedGrowthPercent = trendAnalysis.ExpectedGrowthPercent,
                    StrategyName = Name,
                    ScannedAt = DateTime.UtcNow,
                    IsActive = true,
                    PiotroskiFScore = healthMetrics.PiotroskiFScore,
                    AltmanZScore = healthMetrics.AltmanZScore
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing PUT option {Symbol} Strike: {Strike}",
                    data.MarketData?.Symbol, option.Strike);
                return null;
            }
        }, cancellationToken);
    }

    private decimal CalculateConfidence(
        AggregatedMarketData data,
        OptionContract option,
        decimal safetyMargin,
        decimal annualizedReturn)
    {
        var marketData = data.MarketData!;
        var trendAnalysis = data.TrendAnalysis!;

        // Base confidence from trend analysis
        var trendConfidence = trendAnalysis.Confidence;

        // Technical indicator score
        var technicalScore = CalculateTechnicalScore(marketData);

        // Option-specific score
        var optionScore = CalculateOptionScore(option, safetyMargin, annualizedReturn);

        // Volatility score (prefer lower IV for PUT selling)
        var volatilityScore = CalculateVolatilityScore(option.ImpliedVolatility);

        // Dividend score (if applicable)
        var dividendScore = CalculateDividendScore(data.DividendInfo);

        // Weight the scores - empirically selected :)
        var confidence = (trendConfidence * 0.30m) +      // 30% trend
                        (technicalScore * 0.25m) +         // 25% technicals
                        (optionScore * 0.20m) +            // 20% option metrics
                        (volatilityScore * 0.15m) +        // 15% volatility
                        (dividendScore * 0.10m);           // 10% dividend

        return Math.Round(Math.Min(1m, Math.Max(0m, confidence)), 3);
    }

    private decimal CalculateTechnicalScore(MarketData marketData)
    {
        var score = 0m;
        var factors = 0;

        // Price above moving averages
        if (marketData.CurrentPrice > marketData.MovingAverage20)
        {
            score += 0.25m;
            factors++;
        }
        if (marketData.CurrentPrice > marketData.MovingAverage50)
        {
            score += 0.25m;
            factors++;
        }
        if (marketData.CurrentPrice > marketData.MovingAverage200)
        {
            score += 0.25m;
            factors++;
        }

        // RSI in healthy range (30-70)
        if (marketData.RSI > 30 && marketData.RSI < 70)
        {
            score += 0.25m;
            factors++;
        }
        else if (marketData.RSI <= 30) // Oversold might bounce
        {
            score += 0.15m;
            factors++;
        }

        // MACD positive
        if (marketData.MACD > marketData.MACDSignal)
        {
            score += 0.25m;
            factors++;
        }

        // Price position within 52-week range
        var rangePosition = (marketData.CurrentPrice - marketData.Low52Week) /
                           (marketData.High52Week - marketData.Low52Week);

        if (rangePosition > 0.5m) // Upper half of range
        {
            score += 0.25m;
            factors++;
        }

        return factors > 0 ? score / factors : 0.5m;
    }

    private decimal CalculateOptionScore(
        OptionContract option,
        decimal safetyMargin,
        decimal annualizedReturn)
    {
        var score = 0m;

        // Safety margin score (optimal between 7-15%)
        if (safetyMargin >= 0.07m && safetyMargin <= 0.15m)
        {
            score += 0.4m;
        }
        else if (safetyMargin >= 0.05m && safetyMargin <= 0.20m)
        {
            score += 0.2m;
        }

        // Return score (higher is better, up to a point)
        if (annualizedReturn >= 0.30m)
        {
            score += 0.3m;
        }
        else if (annualizedReturn >= 0.20m)
        {
            score += 0.25m;
        }
        else if (annualizedReturn >= 0.15m)
        {
            score += 0.2m;
        }
        else if (annualizedReturn >= 0.10m)
        {
            score += 0.1m;
        }

        // Delta score (prefer delta between -0.20 and -0.35)
        var absDelta = Math.Abs(option.Delta);
        if (absDelta >= 0.20m && absDelta <= 0.35m)
        {
            score += 0.3m;
        }
        else if (absDelta >= 0.15m && absDelta <= 0.40m)
        {
            score += 0.15m;
        }

        return Math.Min(1m, score);
    }

    private decimal CalculateVolatilityScore(decimal impliedVolatility)
    {
        // Prefer moderate IV (not too low, not too high)
        // Optimal range: 15-30%
        if (impliedVolatility >= 0.15m && impliedVolatility <= 0.30m)
        {
            return 0.8m;
        }
        else if (impliedVolatility >= 0.10m && impliedVolatility <= 0.40m)
        {
            return 0.6m;
        }
        else if (impliedVolatility < 0.10m)
        {
            return 0.3m; // Too low - not enough premium
        }
        else
        {
            return 0.4m; // Too high - risky
        }
    }

    private decimal CalculateDividendScore(DividendInfo? dividendInfo)
    {
        if (dividendInfo == null || dividendInfo.DividendYield == 0)
        {
            return 0.5m; // Neutral score for non-dividend stocks
        }

        // Prefer stocks with moderate dividend yield (1-4%)
        if (dividendInfo.DividendYield >= 1m && dividendInfo.DividendYield <= 4m)
        {
            return 0.8m;
        }
        else if (dividendInfo.DividendYield > 0m && dividendInfo.DividendYield < 6m)
        {
            return 0.6m;
        }
        else
        {
            return 0.3m; // Very high yield might indicate risk
        }
    }
}
