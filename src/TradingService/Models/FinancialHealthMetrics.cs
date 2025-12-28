namespace TradingService.Models;

/// <summary>
/// Financial health metrics for fundamental analysis
/// </summary>
public record FinancialHealthMetrics
{
    /// <summary>
    /// Piotroski F-Score (0-9): Measures financial strength
    /// 8-9: Strong, 5-7: Moderate, 0-4: Weak
    /// </summary>
    public decimal? PiotroskiFScore { get; init; }

    /// <summary>
    /// Altman Z-Score: Predicts bankruptcy risk
    /// > 2.99: Safe zone, 1.81-2.99: Gray zone, < 1.81: Distress zone
    /// </summary>
    public decimal? AltmanZScore { get; init; }

    /// <summary>
    /// Return on Assets (%)
    /// </summary>
    public decimal? ROA { get; init; }

    /// <summary>
    /// Debt to Equity ratio
    /// </summary>
    public decimal? DebtToEquity { get; init; }

    /// <summary>
    /// Current Ratio (Current Assets / Current Liabilities)
    /// </summary>
    public decimal? CurrentRatio { get; init; }

    /// <summary>
    /// Market Cap in billions
    /// </summary>
    public decimal? MarketCapBillions { get; init; }
}
