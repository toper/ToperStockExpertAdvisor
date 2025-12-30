using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data.Entities;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Strategies;

/// <summary>
/// Dividend Momentum strategy focusing on dividend-paying stocks with positive momentum
/// Targets stocks with stable dividends and upward price trends for conservative PUT selling
/// Filters by Piotroski F-Score and Altman Z-Score for financial health
/// </summary>
public class DividendMomentumStrategy : IStrategy
{
    private readonly ILogger<DividendMomentumStrategy> _logger;
    private readonly StrategySettings _settings;
    private readonly IFinancialHealthService _financialHealthService;

    public string Name => "DividendMomentum";
    public string Description => "Conservative PUT selling on financially healthy dividend stocks with positive momentum";
    public int TargetExpiryMinDays => _settings.MinExpiryDays;
    public int TargetExpiryMaxDays => _settings.MaxExpiryDays;

    // Strategy-specific thresholds
    private const decimal MinDividendYield = 1.0m;  // Minimum 1% dividend yield
    private const decimal MaxDividendYield = 8.0m;  // Maximum 8% (avoid dividend traps)
    private const decimal MinMomentumScore = 0.6m;  // Minimum momentum score required

    public DividendMomentumStrategy(
        ILogger<DividendMomentumStrategy> logger,
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
            // Validate required data
            if (!IsDataValid(data))
            {
                return recommendations;
            }

            var symbol = data.MarketData!.Symbol;

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

            // Check dividend criteria
            if (!MeetsDividendCriteria(data.DividendInfo))
            {
                _logger.LogInformation(
                    "Skipping {Symbol} - Does not meet dividend criteria",
                    symbol);
                return recommendations;
            }

            // Calculate momentum score
            var momentumScore = CalculateMomentumScore(data);
            if (momentumScore < MinMomentumScore)
            {
                _logger.LogInformation(
                    "Skipping {Symbol} - Low momentum score: {Score:P}",
                    symbol, momentumScore);
                return recommendations;
            }

            // Analyze PUT options with dividend-aware criteria
            foreach (var option in data.ShortTermPutOptions)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var recommendation = await AnalyzeDividendPutOptionAsync(
                    data, option, momentumScore, healthMetrics, cancellationToken);

                if (recommendation != null)
                {
                    recommendations.Add(recommendation);
                }
            }

            // Select best recommendations (more conservative selection)
            var topRecommendations = recommendations
                .Where(r => r.Confidence >= _settings.MinConfidence + 0.05m) // Higher threshold
                .OrderByDescending(r => r.Confidence)
                .ThenBy(r => r.DaysToExpiry)
                .Take(1) // Only best recommendation per symbol
                .ToList();

            if (topRecommendations.Any())
            {
                _logger.LogInformation(
                    "Generated {Count} dividend momentum recommendations for {Symbol} " +
                    "(Dividend Yield: {Yield:P}, Momentum: {Momentum:P})",
                    topRecommendations.Count,
                    symbol,
                    data.DividendInfo!.DividendYield / 100,
                    momentumScore);
            }

            return topRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing {Symbol} with DividendMomentumStrategy",
                data.MarketData?.Symbol ?? "Unknown");
            return recommendations;
        }
    }

    private bool IsDataValid(AggregatedMarketData data)
    {
        if (data.MarketData == null)
        {
            _logger.LogWarning("No market data available");
            return false;
        }

        if (data.TrendAnalysis == null)
        {
            _logger.LogWarning("No trend analysis available for {Symbol}", data.MarketData.Symbol);
            return false;
        }

        if (!data.ShortTermPutOptions.Any())
        {
            _logger.LogWarning("No short-term PUT options available for {Symbol}", data.MarketData.Symbol);
            return false;
        }

        if (data.DividendInfo == null)
        {
            _logger.LogInformation("{Symbol} has no dividend information", data.MarketData.Symbol);
            return false;
        }

        return true;
    }

    private bool MeetsDividendCriteria(DividendInfo? dividendInfo)
    {
        if (dividendInfo == null)
            return false;

        // Check dividend yield range
        if (dividendInfo.DividendYield < MinDividendYield ||
            dividendInfo.DividendYield > MaxDividendYield)
        {
            return false;
        }

        // Check if dividend is recent (not suspended)
        if (dividendInfo.ExDividendDate.HasValue)
        {
            var daysSinceExDividend = (DateTime.Today - dividendInfo.ExDividendDate.Value).Days;
            if (daysSinceExDividend > 180) // No dividend in last 6 months
            {
                return false;
            }
        }

        return true;
    }

    private decimal CalculateMomentumScore(AggregatedMarketData data)
    {
        var marketData = data.MarketData!;
        var score = 0m;
        var weights = 0m;

        // Price vs Moving Averages (40% weight)
        var maScore = 0m;
        if (marketData.CurrentPrice > marketData.MovingAverage20)
            maScore += 0.33m;
        if (marketData.CurrentPrice > marketData.MovingAverage50)
            maScore += 0.33m;
        if (marketData.CurrentPrice > marketData.MovingAverage200)
            maScore += 0.34m;

        score += maScore * 0.4m;
        weights += 0.4m;

        // Trend strength (30% weight)
        if (data.TrendAnalysis!.Direction == TrendDirection.Up)
        {
            score += data.TrendAnalysis.TrendStrength * 0.3m;
        }
        else if (data.TrendAnalysis.Direction == TrendDirection.Sideways)
        {
            score += data.TrendAnalysis.TrendStrength * 0.15m; // Half credit for sideways
        }
        weights += 0.3m;

        // Position in 52-week range (20% weight)
        var range = marketData.High52Week - marketData.Low52Week;
        if (range > 0)
        {
            var position = (marketData.CurrentPrice - marketData.Low52Week) / range;
            if (position > 0.6m) // Upper 40% of range
            {
                score += 0.2m;
            }
            else if (position > 0.4m) // Middle range
            {
                score += 0.1m;
            }
        }
        weights += 0.2m;

        // MACD Signal (10% weight)
        if (marketData.MACD > marketData.MACDSignal)
        {
            score += 0.1m;
        }
        weights += 0.1m;

        return weights > 0 ? score / weights : 0;
    }

    private Task<PutRecommendation?> AnalyzeDividendPutOptionAsync(
        AggregatedMarketData data,
        OptionContract option,
        decimal momentumScore,
        FinancialHealthMetrics healthMetrics,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var marketData = data.MarketData!;
                var dividendInfo = data.DividendInfo!;
                var currentPrice = marketData.CurrentPrice;

                // More conservative strike selection for dividend stocks
                var safetyMargin = (currentPrice - option.Strike) / currentPrice;

                // Require larger safety margin (3-18% OTM)
                if (safetyMargin < 0.03m || safetyMargin > 0.18m)
                {
                    return null;
                }

                // Calculate total return (premium + potential dividends)
                var premium = option.Mid;
                var daysToExpiry = option.DaysToExpiry;

                // Estimate dividends during holding period
                var annualDividend = dividendInfo.AnnualDividend;
                var dividendsDuringPeriod = (annualDividend * daysToExpiry) / 365m;

                // Total return includes premium and dividends
                var totalReturn = premium + dividendsDuringPeriod;
                var returnOnRisk = totalReturn / option.Strike;
                var annualizedReturn = (returnOnRisk * 365m) / daysToExpiry;

                // More conservative return requirement (8% minimum)
                if (annualizedReturn < 0.08m)
                {
                    return null;
                }

                // Calculate confidence with dividend-specific factors
                var confidence = CalculateDividendConfidence(
                    data, option, safetyMargin, annualizedReturn, momentumScore);

                if (confidence < _settings.MinConfidence)
                {
                    return null;
                }

                // Check ex-dividend date conflicts
                if (HasDividendConflict(dividendInfo, option.Expiry))
                {
                    // Reduce confidence if ex-dividend date is near expiry
                    confidence *= 0.9m;
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
                    ExpectedGrowthPercent = data.TrendAnalysis!.ExpectedGrowthPercent,
                    StrategyName = Name,
                    ScannedAt = DateTime.UtcNow,
                    IsActive = true,
                    PiotroskiFScore = healthMetrics.PiotroskiFScore,
                    AltmanZScore = healthMetrics.AltmanZScore,
                    ExanteSymbol = option.ExanteSymbol,
                    OptionPrice = option.Ask,
                    Volume = option.Volume,
                    OpenInterest = option.OpenInterest
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing dividend PUT option {Symbol} Strike: {Strike}",
                    data.MarketData?.Symbol, option.Strike);
                return null;
            }
        }, cancellationToken);
    }

    private decimal CalculateDividendConfidence(
        AggregatedMarketData data,
        OptionContract option,
        decimal safetyMargin,
        decimal annualizedReturn,
        decimal momentumScore)
    {
        var dividendInfo = data.DividendInfo!;

        // Base confidence from momentum
        var baseConfidence = momentumScore;

        // Dividend quality score (25% weight)
        var dividendScore = CalculateDividendQualityScore(dividendInfo);

        // Safety margin score (20% weight)
        var safetyScore = 0m;
        if (safetyMargin >= 0.10m && safetyMargin <= 0.15m)
        {
            safetyScore = 1.0m; // Optimal range
        }
        else if (safetyMargin >= 0.08m && safetyMargin <= 0.18m)
        {
            safetyScore = 0.7m;
        }
        else
        {
            safetyScore = 0.3m;
        }

        // Return score (15% weight) - more conservative
        var returnScore = 0m;
        if (annualizedReturn >= 0.15m)
        {
            returnScore = 1.0m;
        }
        else if (annualizedReturn >= 0.12m)
        {
            returnScore = 0.8m;
        }
        else if (annualizedReturn >= 0.10m)
        {
            returnScore = 0.6m;
        }
        else if (annualizedReturn >= 0.08m)
        {
            returnScore = 0.4m;
        }

        // Volatility penalty (10% weight)
        var volatilityScore = 0m;
        if (option.ImpliedVolatility <= 0.25m)
        {
            volatilityScore = 1.0m; // Low volatility preferred
        }
        else if (option.ImpliedVolatility <= 0.35m)
        {
            volatilityScore = 0.7m;
        }
        else
        {
            volatilityScore = 0.3m; // High volatility risky for dividend stocks
        }

        // Technical health (10% weight)
        var technicalScore = CalculateTechnicalHealth(data.MarketData!);

        // Weight the scores
        var confidence = (baseConfidence * 0.20m) +       // 20% momentum
                        (dividendScore * 0.25m) +          // 25% dividend quality
                        (safetyScore * 0.20m) +            // 20% safety margin
                        (returnScore * 0.15m) +            // 15% return
                        (volatilityScore * 0.10m) +        // 10% volatility
                        (technicalScore * 0.10m);          // 10% technicals

        return Math.Round(Math.Min(1m, Math.Max(0m, confidence)), 3);
    }

    private decimal CalculateDividendQualityScore(DividendInfo dividendInfo)
    {
        var score = 0m;

        // Yield quality (not too high, not too low)
        if (dividendInfo.DividendYield >= 2m && dividendInfo.DividendYield <= 4m)
        {
            score += 0.5m; // Optimal yield range
        }
        else if (dividendInfo.DividendYield >= 1.5m && dividendInfo.DividendYield <= 5m)
        {
            score += 0.3m;
        }
        else
        {
            score += 0.1m;
        }

        // Recent dividend payment
        if (dividendInfo.ExDividendDate.HasValue)
        {
            var daysSinceExDividend = (DateTime.Today - dividendInfo.ExDividendDate.Value).Days;
            if (daysSinceExDividend <= 90)
            {
                score += 0.3m; // Recent dividend
            }
            else if (daysSinceExDividend <= 120)
            {
                score += 0.2m;
            }
            else
            {
                score += 0.1m;
            }
        }

        // Dividend consistency (if annual dividend > 0)
        if (dividendInfo.AnnualDividend > 0)
        {
            score += 0.2m;
        }

        return Math.Min(1m, score);
    }

    private decimal CalculateTechnicalHealth(MarketData marketData)
    {
        var score = 0m;
        var factors = 0;

        // RSI in healthy range for dividend stocks (40-65)
        if (marketData.RSI >= 40 && marketData.RSI <= 65)
        {
            score += 0.4m;
            factors++;
        }

        // Above key moving averages
        if (marketData.CurrentPrice > marketData.MovingAverage50)
        {
            score += 0.3m;
            factors++;
        }
        if (marketData.CurrentPrice > marketData.MovingAverage200)
        {
            score += 0.3m;
            factors++;
        }

        return factors > 0 ? score / factors : 0.5m;
    }

    private bool HasDividendConflict(DividendInfo dividendInfo, DateTime optionExpiry)
    {
        if (!dividendInfo.ExDividendDate.HasValue)
            return false;

        // Check if ex-dividend date is within 5 days of option expiry
        var daysDifference = Math.Abs((dividendInfo.ExDividendDate.Value - optionExpiry).Days);
        return daysDifference <= 5;
    }
}