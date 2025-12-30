namespace TradingService.Models;

/// <summary>
/// DTO for stock data API responses
/// Combines SimFin financial metrics and Exante options data
/// </summary>
public class StockDataDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Last modification time - replaces ScannedAt/ScanId
    /// </summary>
    public DateTime ModificationTime { get; set; }

    // ==================== SimFin Metrics ====================

    /// <summary>
    /// Piotroski F-Score (0-9 scale, >7 = fundamentally strong)
    /// </summary>
    public decimal? PiotroskiFScore { get; set; }

    /// <summary>
    /// Altman Z-Score (bankruptcy prediction)
    /// </summary>
    public decimal? AltmanZScore { get; set; }

    public decimal? ROA { get; set; }
    public decimal? DebtToEquity { get; set; }
    public decimal? CurrentRatio { get; set; }
    public decimal? MarketCapBillions { get; set; }

    // ==================== Options Data ====================

    public decimal? CurrentPrice { get; set; }
    public decimal? StrikePrice { get; set; }
    public DateTime? Expiry { get; set; }
    public int? DaysToExpiry { get; set; }
    public decimal? Premium { get; set; }
    public decimal? Breakeven { get; set; }

    /// <summary>
    /// Strategy confidence score (0-1 scale)
    /// </summary>
    public decimal? Confidence { get; set; }

    public decimal? ExpectedGrowthPercent { get; set; }
    public string? StrategyName { get; set; }

    public string? ExanteSymbol { get; set; }
    public decimal? OptionPrice { get; set; }
    public int? Volume { get; set; }
    public int? OpenInterest { get; set; }

    // ==================== Calculated Fields ====================

    /// <summary>
    /// Potential return percentage (Premium / CurrentPrice * 100)
    /// </summary>
    public decimal PotentialReturn { get; set; }

    /// <summary>
    /// Out-of-the-money percentage ((CurrentPrice - StrikePrice) / CurrentPrice * 100)
    /// </summary>
    public decimal OtmPercent { get; set; }
}
