using Microsoft.Extensions.Logging;
using System.Text.Json;
using TradingService.Models;
using TradingService.Services.Interfaces;
using TradingService.Services.Integrations;

namespace TradingService.Services;

/// <summary>
/// Service for calculating Piotroski F-Score and Altman Z-Score
/// Uses SimFin API for fundamental data
/// </summary>
public class FinancialHealthService : IFinancialHealthService
{
    private readonly ILogger<FinancialHealthService> _logger;
    private readonly SimFinDataProvider _simFinProvider;

    // Minimum thresholds for healthy companies
    private const decimal MIN_PIOTROSKI_FSCORE = 7m; // 7-9 is strong
    private const decimal MIN_ALTMAN_ZSCORE = 1.81m; // Above distress zone

    public FinancialHealthService(
        ILogger<FinancialHealthService> logger,
        SimFinDataProvider simFinProvider)
    {
        _logger = logger;
        _simFinProvider = simFinProvider;
    }

    public async Task<FinancialHealthMetrics> CalculateMetricsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Calculating financial health metrics for {Symbol}", symbol);

            // For now, use a simplified approach based on available data
            // In production, you would fetch fundamental data from Yahoo Finance or another provider
            // that provides balance sheet, income statement, and cash flow data

            // Yahoo Finance endpoints for fundamental data (requires parsing HTML or using unofficial API)
            // For this implementation, we'll use a conservative approach:
            // - Return null if we can't get reliable fundamental data
            // - This ensures we only recommend stocks we can properly analyze

            var fundamentals = await FetchFundamentalDataAsync(symbol, cancellationToken);

            if (fundamentals == null)
            {
                _logger.LogWarning("Could not fetch fundamental data for {Symbol}, skipping health metrics", symbol);
                return new FinancialHealthMetrics();
            }

            var fScore = CalculatePiotroskiFScore(fundamentals);
            var zScore = CalculateAltmanZScore(fundamentals);

            return new FinancialHealthMetrics
            {
                PiotroskiFScore = fScore,
                AltmanZScore = zScore,
                ROA = fundamentals.ROA,
                DebtToEquity = fundamentals.DebtToEquity,
                CurrentRatio = fundamentals.CurrentRatio,
                MarketCapBillions = fundamentals.MarketCap / 1_000_000_000m
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating financial health metrics for {Symbol}", symbol);
            return new FinancialHealthMetrics();
        }
    }

    public bool MeetsHealthRequirements(FinancialHealthMetrics metrics)
    {
        // Company must have both scores calculated
        if (!metrics.PiotroskiFScore.HasValue || !metrics.AltmanZScore.HasValue)
        {
            return false;
        }

        // Must meet minimum thresholds for both scores
        var meetsFScore = metrics.PiotroskiFScore.Value >= MIN_PIOTROSKI_FSCORE;
        var meetsZScore = metrics.AltmanZScore.Value >= MIN_ALTMAN_ZSCORE;

        return meetsFScore && meetsZScore;
    }

    public async Task<Dictionary<string, FinancialHealthMetrics>> CalculateMetricsBatchAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var symbolsList = symbols.ToList();
        _logger.LogInformation("Starting batch financial health calculation for {Count} symbols", symbolsList.Count);

        var results = new System.Collections.Concurrent.ConcurrentDictionary<string, FinancialHealthMetrics>();
        var semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent requests

        await Parallel.ForEachAsync(symbolsList,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 5,
                CancellationToken = cancellationToken
            },
            async (symbol, ct) =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var metrics = await CalculateMetricsAsync(symbol, ct);
                    results[symbol] = metrics;

                    if (MeetsHealthRequirements(metrics))
                    {
                        _logger.LogDebug("{Symbol} is healthy: F-Score={FScore}, Z-Score={ZScore}",
                            symbol, metrics.PiotroskiFScore, metrics.AltmanZScore);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to calculate health for {Symbol}", symbol);
                    results[symbol] = new FinancialHealthMetrics();
                }
                finally
                {
                    semaphore.Release();
                }
            });

        var healthyCount = results.Values.Count(MeetsHealthRequirements);
        _logger.LogInformation(
            "Batch calculation complete: {Healthy}/{Total} symbols meet health requirements",
            healthyCount, symbolsList.Count);

        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private async Task<FundamentalData?> FetchFundamentalDataAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        try
        {
            // Fetch data from SimFin API
            var simFinData = await _simFinProvider.GetCompanyDataAsync(symbol, cancellationToken);

            if (simFinData == null)
            {
                _logger.LogWarning("Failed to fetch fundamental data for {Symbol} from SimFin", symbol);
                return null;
            }

            var data = MapSimFinDataToFundamentals(simFinData);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fundamental data for {Symbol}", symbol);
            return null;
        }
    }

    private FundamentalData? MapSimFinDataToFundamentals(SimFinCompanyData simFinData)
    {
        try
        {
            var balance = simFinData.BalanceSheet;
            var income = simFinData.IncomeStatement;
            var cashFlow = simFinData.CashFlow;

            if (balance == null || income == null)
            {
                _logger.LogDebug("Incomplete financial data from SimFin");
                return null;
            }

            var data = new FundamentalData
            {
                TotalAssets = balance.TotalAssets ?? 0,
                TotalCash = balance.TotalCash ?? 0,
                TotalDebt = balance.TotalDebt ?? 0,
                TotalRevenue = income.Revenue ?? 0,
                OperatingCashFlow = cashFlow?.OperatingCashFlow ?? 0,
                EBITDA = income.OperatingIncome ?? 0 // Using Operating Income as approximation for EBITDA
            };

            // Calculate derived metrics
            if (balance.TotalEquity.HasValue && balance.TotalEquity.Value > 0)
            {
                data.DebtToEquity = data.TotalDebt / balance.TotalEquity.Value;
            }

            if (balance.CurrentAssets.HasValue && balance.CurrentLiabilities.HasValue &&
                balance.CurrentLiabilities.Value > 0)
            {
                data.CurrentRatio = balance.CurrentAssets.Value / balance.CurrentLiabilities.Value;
            }

            if (data.TotalAssets > 0 && income.NetIncome.HasValue)
            {
                // ROA = Net Income / Total Assets * 100
                data.ROA = (income.NetIncome.Value / data.TotalAssets) * 100m;
            }

            // Calculate Market Cap if we have shares outstanding
            // Note: SimFin doesn't provide stock price in general endpoint, so we can't calculate exact market cap
            // For now, use Total Equity as approximation
            data.MarketCap = balance.TotalEquity ?? 0;

            _logger.LogDebug(
                "Mapped SimFin data: Assets={Assets}, Revenue={Revenue}, Debt={Debt}, ROA={ROA}%",
                data.TotalAssets, data.TotalRevenue, data.TotalDebt, data.ROA);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping SimFin data to fundamentals");
            return null;
        }
    }

    private decimal? CalculatePiotroskiFScore(FundamentalData data)
    {
        try
        {
            int score = 0;

            // Profitability (4 criteria)
            if (data.ROA > 0) score++; // Positive ROA
            if (data.OperatingCashFlow > 0) score++; // Positive Operating Cash Flow

            // Note: Full F-Score requires year-over-year comparisons
            // For simplification, we'll use current values only
            // In production, you'd compare to prior year data

            // Leverage, Liquidity and Source of Funds (3 criteria)
            if (data.CurrentRatio > 1.5m) score++; // Good liquidity
            if (data.DebtToEquity < 0.5m) score++; // Low debt

            // Operating Efficiency (2 criteria with available data)
            if (data.ROA > 5m) score++; // Strong ROA (> 5%)
            if (data.OperatingCashFlow > data.EBITDA * 0.8m) score++; // Strong cash conversion

            // Additional quality checks
            if (data.TotalCash > data.TotalDebt * 0.5m) score++; // Good cash position
            if (data.CurrentRatio > 2m) score++; // Excellent liquidity
            if (data.DebtToEquity < 0.3m) score++; // Very low debt

            return Math.Min(9m, score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Piotroski F-Score");
            return null;
        }
    }

    private decimal? CalculateAltmanZScore(FundamentalData data)
    {
        try
        {
            if (data.TotalAssets == 0 || data.MarketCap == 0)
            {
                return null;
            }

            // Altman Z-Score = 1.2*X1 + 1.4*X2 + 3.3*X3 + 0.6*X4 + 1.0*X5
            // X1 = Working Capital / Total Assets
            // X2 = Retained Earnings / Total Assets (approximated by ROA)
            // X3 = EBIT / Total Assets
            // X4 = Market Value of Equity / Total Liabilities
            // X5 = Sales / Total Assets

            var workingCapital = data.TotalCash - (data.TotalDebt * 0.3m); // Approximation
            var X1 = workingCapital / data.TotalAssets;

            var retainedEarnings = data.TotalAssets * (data.ROA / 100m); // Approximation
            var X2 = retainedEarnings / data.TotalAssets;

            var ebit = data.EBITDA * 0.8m; // Approximation (EBIT ≈ 80% of EBITDA)
            var X3 = ebit / data.TotalAssets;

            var totalLiabilities = data.TotalDebt;
            var X4 = totalLiabilities > 0 ? data.MarketCap / totalLiabilities : 5m; // Cap at 5 if no debt

            var X5 = data.TotalRevenue / data.TotalAssets;

            var zScore = (1.2m * X1) + (1.4m * X2) + (3.3m * X3) + (0.6m * X4) + (1.0m * X5);

            return Math.Round(zScore, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Altman Z-Score");
            return null;
        }
    }

    private class FundamentalData
    {
        public decimal ROA { get; set; }
        public decimal DebtToEquity { get; set; }
        public decimal CurrentRatio { get; set; }
        public decimal MarketCap { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalDebt { get; set; }
        public decimal TotalCash { get; set; }
        public decimal EBITDA { get; set; }
        public decimal OperatingCashFlow { get; set; }
        public decimal TotalAssets { get; set; }
    }
}
