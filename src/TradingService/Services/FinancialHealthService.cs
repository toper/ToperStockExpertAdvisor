using Microsoft.Extensions.Logging;
using System.Text.Json;
using TradingService.Models;
using TradingService.Services.Interfaces;
using TradingService.Services.Integrations;

namespace TradingService.Services;

/// <summary>
/// Service for calculating Modified Piotroski F-Score and Altman Z-Score
/// Uses SimFin API for fundamental data
///
/// MODIFIED PIOTROSKI F-SCORE:
/// - 7 out of 9 criteria are 100% accurate to original methodology
/// - F5 (Long-term Debt Ratio): ✓ Accurate using actual long-term debt
/// - F8 (Gross Margin): ⚠️ Uses operating margin as proxy (COGS unavailable in SimFin bulk CSV)
/// </summary>
public class FinancialHealthService : IFinancialHealthService
{
    private readonly ILogger<FinancialHealthService> _logger;
    private readonly ISimFinDataProvider _simFinProvider;
    private readonly IMarketDataProvider _marketDataProvider;

    // Minimum thresholds for healthy companies
    private const decimal MIN_PIOTROSKI_FSCORE = 7m; // 7-9 is strong
    private const decimal MIN_ALTMAN_ZSCORE = 1.81m; // Original Z-Score for public companies: Above distress zone (< 1.81 = distress, 1.81-2.99 = grey, > 2.99 = safe)

    public FinancialHealthService(
        ILogger<FinancialHealthService> logger,
        ISimFinDataProvider simFinProvider,
        IMarketDataProvider marketDataProvider)
    {
        _logger = logger;
        _simFinProvider = simFinProvider;
        _marketDataProvider = marketDataProvider;
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
            // Fetch fundamental data from SimFin API
            var simFinData = await _simFinProvider.GetCompanyDataAsync(symbol, cancellationToken);

            if (simFinData == null)
            {
                _logger.LogWarning("Failed to fetch fundamental data for {Symbol} from SimFin", symbol);
                return null;
            }

            // Fetch current market price from Yahoo Finance for accurate Market Cap calculation
            var marketData = await _marketDataProvider.GetMarketDataAsync(symbol);
            decimal currentPrice = marketData?.CurrentPrice ?? 0;

            if (currentPrice == 0)
            {
                _logger.LogWarning("Failed to fetch current stock price for {Symbol} from Yahoo Finance", symbol);
            }

            var data = MapSimFinDataToFundamentals(simFinData, currentPrice);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fundamental data for {Symbol}", symbol);
            return null;
        }
    }

    private FundamentalData? MapSimFinDataToFundamentals(SimFinCompanyData simFinData, decimal currentStockPrice)
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
                EBITDA = income.OperatingIncome ?? 0, // Using Operating Income as approximation for EBITDA

                // Fields for accurate Altman Z-Score calculation
                RetainedEarnings = balance.RetainedEarnings ?? 0,
                TotalLiabilities = balance.TotalLiabilities ?? 0,
                CurrentAssets = balance.CurrentAssets ?? 0,
                CurrentLiabilities = balance.CurrentLiabilities ?? 0,
                BookValueOfEquity = balance.TotalEquity ?? 0,
                OperatingIncome = income.OperatingIncome ?? 0,
                SharesOutstanding = income.SharesOutstanding ?? 0,
                CurrentStockPrice = currentStockPrice,
                NetIncome = income.NetIncome ?? 0
            };

            // Calculate derived metrics for current period
            if (balance.TotalEquity.HasValue && balance.TotalEquity.Value > 0)
            {
                data.DebtToEquity = data.TotalDebt / balance.TotalEquity.Value;
            }

            if (balance.CurrentAssets.HasValue && balance.CurrentLiabilities.HasValue &&
                balance.CurrentLiabilities.Value > 0)
            {
                data.CurrentRatio = balance.CurrentAssets.Value / balance.CurrentLiabilities.Value;
            }

            if (data.TotalAssets > 0)
            {
                if (income.NetIncome.HasValue)
                {
                    // ROA = Net Income / Total Assets * 100
                    data.ROA = (income.NetIncome.Value / data.TotalAssets) * 100m;
                }

                // Asset Turnover = Revenue / Total Assets
                data.AssetTurnover = data.TotalRevenue / data.TotalAssets;

                // Long-term Debt Ratio = Long-term Debt / Total Assets (Piotroski F5)
                // Using actual long-term debt (not total debt) for accurate F-Score calculation
                var longTermDebt = balance.LongTermDebt ?? 0;
                data.LongTermDebtRatio = longTermDebt / data.TotalAssets;
            }

            // Operating Margin (used as proxy for Gross Margin in Piotroski F8)
            // Note: True Gross Margin = (Revenue - COGS) / Revenue, but SimFin bulk CSV doesn't provide COGS
            // Using Operating Margin = Operating Income / Revenue as approximation
            // This is a limitation but acceptable for screening purposes
            if (data.TotalRevenue > 0 && income.OperatingIncome.HasValue)
            {
                data.GrossMargin = income.OperatingIncome.Value / data.TotalRevenue;
            }

            // Calculate Market Cap = Current Stock Price × Shares Outstanding
            // This is the TRUE market value of equity for publicly traded companies
            if (currentStockPrice > 0 && data.SharesOutstanding > 0)
            {
                data.MarketCap = currentStockPrice * data.SharesOutstanding;
                _logger.LogDebug(
                    "Calculated Market Cap: ${MarketCap:N0} (Price: ${Price:F2} × Shares: {Shares:N0})",
                    data.MarketCap, currentStockPrice, data.SharesOutstanding);
            }
            else
            {
                // Fallback to Book Value if we can't get current price
                data.MarketCap = balance.TotalEquity ?? 0;
                _logger.LogWarning(
                    "Using Book Value as Market Cap fallback (Price={Price}, Shares={Shares})",
                    currentStockPrice, data.SharesOutstanding);
            }

            // Map previous period data if available (for year-over-year comparisons)
            var prevBalance = simFinData.PreviousBalanceSheet;
            var prevIncome = simFinData.PreviousIncomeStatement;

            if (prevBalance != null && prevIncome != null)
            {
                var prevTotalAssets = prevBalance.TotalAssets ?? 0;

                if (prevTotalAssets > 0 && prevIncome.NetIncome.HasValue)
                {
                    data.PreviousROA = (prevIncome.NetIncome.Value / prevTotalAssets) * 100m;
                }

                if (prevBalance.CurrentAssets.HasValue && prevBalance.CurrentLiabilities.HasValue &&
                    prevBalance.CurrentLiabilities.Value > 0)
                {
                    data.PreviousCurrentRatio = prevBalance.CurrentAssets.Value / prevBalance.CurrentLiabilities.Value;
                }

                if (prevTotalAssets > 0)
                {
                    data.PreviousAssetTurnover = (prevIncome.Revenue ?? 0) / prevTotalAssets;
                    // Use actual long-term debt (not total debt) for accurate F-Score F5
                    data.PreviousLongTermDebtRatio = (prevBalance.LongTermDebt ?? 0) / prevTotalAssets;
                }

                var prevRevenue = prevIncome.Revenue ?? 0;
                if (prevRevenue > 0 && prevIncome.OperatingIncome.HasValue)
                {
                    data.PreviousGrossMargin = prevIncome.OperatingIncome.Value / prevRevenue;
                }

                data.PreviousSharesOutstanding = prevIncome.SharesOutstanding;
            }

            _logger.LogDebug(
                "Mapped SimFin data: Assets={Assets}, Revenue={Revenue}, Debt={Debt}, ROA={ROA}%, RetainedEarnings={RetainedEarnings}, MarketCap={MarketCap}",
                data.TotalAssets, data.TotalRevenue, data.TotalDebt, data.ROA, data.RetainedEarnings, data.MarketCap);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping SimFin data to fundamentals");
            return null;
        }
    }

    /// <summary>
    /// Calculates the Modified Piotroski F-Score (0-9)
    /// A score of 7-9 indicates a strong financial position
    /// Based on Joseph Piotroski's 2000 paper "Value Investing: The Use of Historical Financial Statement Information to Separate Winners from Losers"
    ///
    /// IMPORTANT: This is a MODIFIED version due to data limitations:
    /// - F5 (ΔLever): Uses long-term debt ratio ✓ ACCURATE
    /// - F8 (ΔMargin): Uses operating margin as proxy for gross margin ⚠️ APPROXIMATION (COGS not available)
    /// - All other 7 criteria are fully accurate
    /// </summary>
    private decimal? CalculatePiotroskiFScore(FundamentalData data)
    {
        try
        {
            int score = 0;
            var criteriaLog = new List<string>();

            // ====================================
            // PROFITABILITY SIGNALS (4 criteria)
            // ====================================

            // 1. ROA > 0 (Net Income is positive)
            if (data.ROA > 0)
            {
                score++;
                criteriaLog.Add("✓ F1: ROA > 0");
            }
            else
            {
                criteriaLog.Add("✗ F1: ROA ≤ 0");
            }

            // 2. CFO > 0 (Operating Cash Flow is positive)
            if (data.OperatingCashFlow > 0)
            {
                score++;
                criteriaLog.Add("✓ F2: CFO > 0");
            }
            else
            {
                criteriaLog.Add("✗ F2: CFO ≤ 0");
            }

            // 3. ΔROA > 0 (ROA increased compared to previous year)
            if (data.PreviousROA.HasValue && data.ROA > data.PreviousROA.Value)
            {
                score++;
                criteriaLog.Add($"✓ F3: ΔROA > 0 ({data.ROA:F2}% vs {data.PreviousROA:F2}%)");
            }
            else if (!data.PreviousROA.HasValue)
            {
                criteriaLog.Add("⚠ F3: ΔROA - No prior data");
            }
            else
            {
                criteriaLog.Add($"✗ F3: ΔROA ≤ 0 ({data.ROA:F2}% vs {data.PreviousROA:F2}%)");
            }

            // 4. Accruals < 0 (Quality of Earnings: CFO > Net Income)
            // This measures whether earnings are backed by actual cash flow
            if (data.OperatingCashFlow > data.NetIncome)
            {
                score++;
                criteriaLog.Add("✓ F4: Accruals < 0 (CFO > Net Income)");
            }
            else
            {
                criteriaLog.Add("✗ F4: Accruals ≥ 0 (CFO ≤ Net Income)");
            }

            // ====================================
            // LEVERAGE, LIQUIDITY, SOURCE OF FUNDS (3 criteria)
            // ====================================

            // 5. ΔLever < 0 (Long-term debt ratio decreased)
            if (data.PreviousLongTermDebtRatio.HasValue && data.LongTermDebtRatio < data.PreviousLongTermDebtRatio.Value)
            {
                score++;
                criteriaLog.Add($"✓ F5: ΔLever < 0 (debt ratio decreased)");
            }
            else if (!data.PreviousLongTermDebtRatio.HasValue)
            {
                criteriaLog.Add("⚠ F5: ΔLever - No prior data");
            }
            else
            {
                criteriaLog.Add($"✗ F5: ΔLever ≥ 0 (debt ratio increased/unchanged)");
            }

            // 6. ΔLiquid > 0 (Current ratio increased)
            if (data.PreviousCurrentRatio.HasValue && data.CurrentRatio > data.PreviousCurrentRatio.Value)
            {
                score++;
                criteriaLog.Add($"✓ F6: ΔLiquid > 0 (current ratio increased)");
            }
            else if (!data.PreviousCurrentRatio.HasValue)
            {
                criteriaLog.Add("⚠ F6: ΔLiquid - No prior data");
            }
            else
            {
                criteriaLog.Add($"✗ F6: ΔLiquid ≤ 0 (current ratio decreased/unchanged)");
            }

            // 7. Eq_Offer = 0 (No new equity issued - shares outstanding did not increase)
            if (data.PreviousSharesOutstanding.HasValue && data.SharesOutstanding <= data.PreviousSharesOutstanding.Value)
            {
                score++;
                criteriaLog.Add("✓ F7: Eq_Offer = 0 (no dilution)");
            }
            else if (!data.PreviousSharesOutstanding.HasValue)
            {
                criteriaLog.Add("⚠ F7: Eq_Offer - No prior data");
            }
            else
            {
                criteriaLog.Add("✗ F7: Eq_Offer > 0 (shares diluted)");
            }

            // ====================================
            // OPERATING EFFICIENCY (2 criteria)
            // ====================================

            // 8. ΔMargin > 0 (Gross margin increased)
            // LIMITATION: Using Operating Margin as proxy (Operating Income / Revenue)
            // True Gross Margin requires COGS which SimFin doesn't provide in bulk CSV
            if (data.PreviousGrossMargin.HasValue && data.GrossMargin > data.PreviousGrossMargin.Value)
            {
                score++;
                criteriaLog.Add($"✓ F8: ΔMargin > 0 (operating margin improved) PROXY");
            }
            else if (!data.PreviousGrossMargin.HasValue)
            {
                criteriaLog.Add("⚠ F8: ΔMargin - No prior data");
            }
            else
            {
                criteriaLog.Add($"✗ F8: ΔMargin ≤ 0 (operating margin declined/unchanged) PROXY");
            }

            // 9. ΔTurn > 0 (Asset turnover increased)
            if (data.PreviousAssetTurnover.HasValue && data.AssetTurnover > data.PreviousAssetTurnover.Value)
            {
                score++;
                criteriaLog.Add($"✓ F9: ΔTurn > 0 (efficiency improved)");
            }
            else if (!data.PreviousAssetTurnover.HasValue)
            {
                criteriaLog.Add("⚠ F9: ΔTurn - No prior data");
            }
            else
            {
                criteriaLog.Add($"✗ F9: ΔTurn ≤ 0 (efficiency declined/unchanged)");
            }

            // Log the detailed breakdown
            _logger.LogDebug(
                "Piotroski F-Score calculation:\n{Criteria}\nTotal Score: {Score}/9",
                string.Join("\n", criteriaLog),
                score);

            return score;
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
            if (data.TotalAssets == 0)
            {
                return null;
            }

            // Altman Z-Score for Public Companies = 1.2*X1 + 1.4*X2 + 3.3*X3 + 0.6*X4 + 1.0*X5
            // This is the ORIGINAL Z-Score designed for publicly traded manufacturing companies
            //
            // X1 = Working Capital / Total Assets
            // X2 = Retained Earnings / Total Assets
            // X3 = EBIT / Total Assets
            // X4 = Market Value of Equity / Total Liabilities
            // X5 = Sales / Total Assets
            //
            // Interpretation for Z-Score:
            // Z > 2.99: Safe Zone (low bankruptcy risk)
            // 1.81 < Z < 2.99: Grey Zone (moderate risk)
            // Z < 1.81: Distress Zone (high probability of bankruptcy)

            // X1: Working Capital / Total Assets
            // Working Capital = Current Assets - Current Liabilities
            var workingCapital = data.CurrentAssets - data.CurrentLiabilities;
            var X1 = workingCapital / data.TotalAssets;

            // X2: Retained Earnings / Total Assets
            // Using actual Retained Earnings from balance sheet (SimFin data)
            var X2 = data.RetainedEarnings / data.TotalAssets;

            // X3: EBIT / Total Assets
            // Using Operating Income as proxy for EBIT (very close approximation)
            var ebit = data.OperatingIncome;
            var X3 = ebit / data.TotalAssets;

            // X4: Market Value of Equity / Total Liabilities
            // For PUBLIC companies, we use MARKET VALUE (Market Cap) not book value
            // Market Cap = Current Stock Price × Shares Outstanding
            var X4 = data.TotalLiabilities > 0
                ? data.MarketCap / data.TotalLiabilities
                : 10m; // Cap at 10 if no liabilities (extremely strong position)

            // X5: Sales / Total Assets (Asset Turnover)
            var X5 = data.TotalRevenue / data.TotalAssets;

            // Calculate Z-Score using ORIGINAL coefficients for public companies
            var zScore = (1.2m * X1) + (1.4m * X2) + (3.3m * X3) + (0.6m * X4) + (1.0m * X5);

            _logger.LogDebug(
                "Altman Z-Score components: X1={X1:F3} (WC/TA), X2={X2:F3} (RE/TA), X3={X3:F3} (EBIT/TA), X4={X4:F3} (MV/TL), X5={X5:F3} (Sales/TA) => Z={ZScore:F2}",
                X1, X2, X3, X4, X5, zScore);

            _logger.LogDebug(
                "Market Cap used: ${MarketCap:N0}, Total Liabilities: ${TotalLiab:N0}, Stock Price: ${Price:F2}, Shares: {Shares:N0}",
                data.MarketCap, data.TotalLiabilities, data.CurrentStockPrice, data.SharesOutstanding);

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
        // Current period data
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
        public decimal RetainedEarnings { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal CurrentAssets { get; set; }
        public decimal CurrentLiabilities { get; set; }
        public decimal BookValueOfEquity { get; set; }
        public decimal OperatingIncome { get; set; }
        public decimal SharesOutstanding { get; set; }
        public decimal CurrentStockPrice { get; set; }
        public decimal NetIncome { get; set; }
        public decimal GrossMargin { get; set; }
        public decimal AssetTurnover { get; set; }
        public decimal LongTermDebtRatio { get; set; }

        // Previous period data (for year-over-year comparisons)
        public decimal? PreviousROA { get; set; }
        public decimal? PreviousCurrentRatio { get; set; }
        public decimal? PreviousGrossMargin { get; set; }
        public decimal? PreviousAssetTurnover { get; set; }
        public decimal? PreviousLongTermDebtRatio { get; set; }
        public decimal? PreviousSharesOutstanding { get; set; }
    }
}
