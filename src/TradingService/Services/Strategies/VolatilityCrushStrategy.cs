using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data.Entities;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Strategies;

/// <summary>
/// Volatility Crush strategy that targets stocks with elevated implied volatility
/// that is expected to decrease, allowing PUT sellers to capture premium decay.
/// Best suited for post-earnings or event-driven scenarios where IV is artificially high.
/// </summary>
public class VolatilityCrushStrategy : IStrategy
{
    private readonly ILogger<VolatilityCrushStrategy> _logger;
    private readonly StrategySettings _settings;

    // IV thresholds for this strategy
    private const decimal MinImpliedVolatility = 0.25m;  // Minimum IV to consider
    private const decimal MaxImpliedVolatility = 0.60m;  // Maximum IV (too risky above this)
    private const decimal OptimalIVMin = 0.30m;          // Optimal IV range start
    private const decimal OptimalIVMax = 0.50m;          // Optimal IV range end

    public string Name => "VolatilityCrush";
    public string Description => "Targets stocks with elevated IV expected to decrease, capturing premium from volatility contraction";
    public int TargetExpiryMinDays => _settings.MinExpiryDays;
    public int TargetExpiryMaxDays => _settings.MaxExpiryDays;

    public VolatilityCrushStrategy(
        ILogger<VolatilityCrushStrategy> logger,
        IOptions<AppSettings> appSettings)
    {
        _logger = logger;
        _settings = appSettings.Value.Strategy;
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
                _logger.LogWarning("No market data available for VolatilityCrush analysis");
                return recommendations;
            }

            if (!data.ShortTermPutOptions.Any())
            {
                _logger.LogWarning("No short-term PUT options available for {Symbol}", data.MarketData.Symbol);
                return recommendations;
            }

            var symbol = data.MarketData.Symbol;
            var currentPrice = data.MarketData.CurrentPrice;

            // Calculate average IV across available options
            var avgIV = data.ShortTermPutOptions.Average(o => o.ImpliedVolatility);

            // Only proceed if IV is elevated enough for this strategy
            if (avgIV < MinImpliedVolatility)
            {
                _logger.LogDebug(
                    "Skipping {Symbol} - Average IV ({IV:P}) below threshold ({Threshold:P})",
                    symbol, avgIV, MinImpliedVolatility);
                return recommendations;
            }

            // Skip if IV is dangerously high (might indicate fundamental problems)
            if (avgIV > MaxImpliedVolatility)
            {
                _logger.LogDebug(
                    "Skipping {Symbol} - Average IV ({IV:P}) above maximum threshold ({Threshold:P})",
                    symbol, avgIV, MaxImpliedVolatility);
                return recommendations;
            }

            // Check trend stability - we want stable or upward trends
            if (!IsStableTrend(data.TrendAnalysis))
            {
                _logger.LogDebug(
                    "Skipping {Symbol} - Trend not stable enough for volatility crush",
                    symbol);
                return recommendations;
            }

            _logger.LogInformation(
                "Analyzing {Symbol} for VolatilityCrush - Avg IV: {IV:P}, Price: ${Price}",
                symbol, avgIV, currentPrice);

            // Filter and analyze options
            foreach (var option in data.ShortTermPutOptions)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var recommendation = await AnalyzePutOptionAsync(
                    data, option, avgIV, cancellationToken);

                if (recommendation != null)
                {
                    recommendations.Add(recommendation);
                }
            }

            // Sort by confidence and return top recommendations
            var topRecommendations = recommendations
                .OrderByDescending(r => r.Confidence)
                .ThenByDescending(r => r.Premium) // Prefer higher premiums for this strategy
                .Take(3)
                .ToList();

            if (topRecommendations.Any())
            {
                _logger.LogInformation(
                    "VolatilityCrush: Generated {Count} recommendations for {Symbol} " +
                    "(Best IV: {IV:P}, Premium: ${Premium})",
                    topRecommendations.Count,
                    symbol,
                    data.ShortTermPutOptions.First(o => o.Strike == topRecommendations.First().StrikePrice).ImpliedVolatility,
                    topRecommendations.First().Premium);
            }

            return topRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing {Symbol} with VolatilityCrushStrategy",
                data.MarketData?.Symbol ?? "Unknown");
            return recommendations;
        }
    }

    private bool IsStableTrend(TrendAnalysis? trend)
    {
        if (trend == null)
            return true; // Proceed without trend data but with caution

        // Accept upward and sideways trends
        // Reject strong downward trends
        return trend.Direction != TrendDirection.Down ||
               (trend.Direction == TrendDirection.Down && trend.TrendStrength < 0.3m);
    }

    private Task<PutRecommendation?> AnalyzePutOptionAsync(
        AggregatedMarketData data,
        OptionContract option,
        decimal avgIV,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var marketData = data.MarketData!;
                var currentPrice = marketData.CurrentPrice;

                // Calculate safety margin
                var safetyMargin = (currentPrice - option.Strike) / currentPrice;

                // For volatility crush, we can be slightly more aggressive (4-18% OTM)
                // because high IV provides extra cushion
                if (safetyMargin < 0.04m)
                {
                    return null;
                }

                if (safetyMargin > 0.18m)
                {
                    return null;
                }

                // Require minimum IV on the specific option
                if (option.ImpliedVolatility < MinImpliedVolatility)
                {
                    return null;
                }

                // Calculate premium-based return
                var premium = option.Mid;
                var daysToExpiry = option.DaysToExpiry;
                var returnOnRisk = premium / option.Strike;
                var annualizedReturn = (returnOnRisk * 365m) / daysToExpiry;

                // For volatility crush, we expect higher returns due to elevated IV
                // Minimum 15% annualized (higher than ShortTermPut's 10%)
                if (annualizedReturn < 0.15m)
                {
                    return null;
                }

                // Calculate confidence score
                var confidence = CalculateConfidence(
                    data, option, safetyMargin, annualizedReturn, avgIV);

                if (confidence < _settings.MinConfidence)
                {
                    return null;
                }

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
                    ExpectedGrowthPercent = data.TrendAnalysis?.ExpectedGrowthPercent ?? 0m,
                    StrategyName = Name,
                    ScannedAt = DateTime.UtcNow,
                    IsActive = true
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
        decimal annualizedReturn,
        decimal avgIV)
    {
        var marketData = data.MarketData!;

        // IV Score (most important for this strategy - 35%)
        var ivScore = CalculateIVScore(option.ImpliedVolatility, avgIV);

        // Technical Score (20%)
        var technicalScore = CalculateTechnicalScore(marketData);

        // Option Metrics Score (25%)
        var optionScore = CalculateOptionScore(option, safetyMargin, annualizedReturn);

        // Trend Score (15%)
        var trendScore = CalculateTrendScore(data.TrendAnalysis);

        // Time Decay Score - theta matters for volatility crush (5%)
        var thetaScore = CalculateThetaScore(option);

        // Weight the scores - IV is most important for this strategy
        var confidence = (ivScore * 0.35m) +
                        (optionScore * 0.25m) +
                        (technicalScore * 0.20m) +
                        (trendScore * 0.15m) +
                        (thetaScore * 0.05m);

        return Math.Round(Math.Min(1m, Math.Max(0m, confidence)), 3);
    }

    private decimal CalculateIVScore(decimal optionIV, decimal avgIV)
    {
        // Best score when IV is in optimal range
        if (optionIV >= OptimalIVMin && optionIV <= OptimalIVMax)
        {
            return 0.9m;
        }
        // Good score when IV is elevated but not in optimal range
        else if (optionIV >= MinImpliedVolatility && optionIV < OptimalIVMin)
        {
            return 0.7m;
        }
        // Moderate score when IV is higher than optimal (more risk)
        else if (optionIV > OptimalIVMax && optionIV <= MaxImpliedVolatility)
        {
            return 0.6m;
        }

        return 0.4m;
    }

    private decimal CalculateTechnicalScore(MarketData marketData)
    {
        var score = 0m;
        var factors = 0;

        // Price above key moving averages
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

        // RSI in normal range (not overbought or oversold)
        if (marketData.RSI > 35 && marketData.RSI < 65)
        {
            score += 0.3m;
            factors++;
        }

        // MACD momentum
        if (marketData.MACD > marketData.MACDSignal)
        {
            score += 0.2m;
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

        // Safety margin (5-12% is optimal for vol crush)
        if (safetyMargin >= 0.05m && safetyMargin <= 0.12m)
        {
            score += 0.4m;
        }
        else if (safetyMargin >= 0.04m && safetyMargin <= 0.18m)
        {
            score += 0.25m;
        }

        // Annualized return (higher is better for vol crush)
        if (annualizedReturn >= 0.40m)
        {
            score += 0.35m;
        }
        else if (annualizedReturn >= 0.30m)
        {
            score += 0.30m;
        }
        else if (annualizedReturn >= 0.20m)
        {
            score += 0.25m;
        }
        else if (annualizedReturn >= 0.15m)
        {
            score += 0.15m;
        }

        // Delta preference (slightly higher delta acceptable for vol crush: -0.25 to -0.40)
        var absDelta = Math.Abs(option.Delta);
        if (absDelta >= 0.25m && absDelta <= 0.40m)
        {
            score += 0.25m;
        }
        else if (absDelta >= 0.20m && absDelta <= 0.45m)
        {
            score += 0.15m;
        }

        return Math.Min(1m, score);
    }

    private decimal CalculateTrendScore(TrendAnalysis? trend)
    {
        if (trend == null)
            return 0.5m; // Neutral

        // Prefer upward or stable sideways trends
        return trend.Direction switch
        {
            TrendDirection.Up => 0.8m + (trend.TrendStrength * 0.2m),
            TrendDirection.Sideways => 0.6m,
            TrendDirection.Down => trend.TrendStrength < 0.3m ? 0.4m : 0.2m,
            _ => 0.5m
        };
    }

    private decimal CalculateThetaScore(OptionContract option)
    {
        // Higher theta (more negative) is better for option sellers
        var absTheta = Math.Abs(option.Theta);

        if (absTheta >= 0.05m)
            return 0.9m;
        else if (absTheta >= 0.03m)
            return 0.7m;
        else if (absTheta >= 0.02m)
            return 0.5m;
        else
            return 0.3m;
    }
}
