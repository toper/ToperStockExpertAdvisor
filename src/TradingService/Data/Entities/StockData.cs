using LinqToDB.Mapping;

namespace TradingService.Data.Entities;

/// <summary>
/// Unified stock data entity combining SimFin financial metrics and Exante options data
/// Single record per symbol with UPDATE-based pipeline
/// </summary>
[Table("StockData")]
public class StockData
{
    // ==================== Identity ====================

    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [NotNull]
    [Column("Symbol")]
    public string Symbol { get; set; } = string.Empty;

    // ==================== Timestamps ====================

    /// <summary>
    /// Last modification time (updated by any data source)
    /// Used for 90-day retention cleanup
    /// </summary>
    [NotNull]
    [Column("ModificationTime")]
    public DateTime ModificationTime { get; set; }

    /// <summary>
    /// Last time SimFin data was updated
    /// </summary>
    [Column("SimFinUpdatedAt")]
    public DateTime? SimFinUpdatedAt { get; set; }

    /// <summary>
    /// Last time Exante/Yahoo data was updated
    /// </summary>
    [Column("ExanteUpdatedAt")]
    public DateTime? ExanteUpdatedAt { get; set; }

    // ==================== SimFin Data ====================

    /// <summary>
    /// Financial report period date
    /// </summary>
    [Column("ReportDate")]
    public DateTime? ReportDate { get; set; }

    // Financial Health Metrics (calculated from SimFin)

    /// <summary>
    /// Piotroski F-Score (0-9 scale)
    /// >7 indicates fundamentally strong company
    /// </summary>
    [Column("PiotroskiFScore")]
    public decimal? PiotroskiFScore { get; set; }

    /// <summary>
    /// Altman Z-Score (bankruptcy prediction)
    /// >2.99 = safe zone, 1.81-2.99 = grey zone, <1.81 = distress zone
    /// </summary>
    [Column("AltmanZScore")]
    public decimal? AltmanZScore { get; set; }

    /// <summary>
    /// Return on Assets
    /// </summary>
    [Column("ROA")]
    public decimal? ROA { get; set; }

    /// <summary>
    /// Debt-to-Equity ratio
    /// </summary>
    [Column("DebtToEquity")]
    public decimal? DebtToEquity { get; set; }

    /// <summary>
    /// Current Ratio (Current Assets / Current Liabilities)
    /// </summary>
    [Column("CurrentRatio")]
    public decimal? CurrentRatio { get; set; }

    /// <summary>
    /// Market capitalization in billions
    /// </summary>
    [Column("MarketCapBillions")]
    public decimal? MarketCapBillions { get; set; }

    // Fundamental Data (from SimFin)

    [Column("TotalAssets")]
    public decimal? TotalAssets { get; set; }

    [Column("TotalLiabilities")]
    public decimal? TotalLiabilities { get; set; }

    [Column("TotalEquity")]
    public decimal? TotalEquity { get; set; }

    [Column("Revenue")]
    public decimal? Revenue { get; set; }

    [Column("NetIncome")]
    public decimal? NetIncome { get; set; }

    [Column("OperatingCashFlow")]
    public decimal? OperatingCashFlow { get; set; }

    [Column("SharesOutstanding")]
    public decimal? SharesOutstanding { get; set; }

    // ==================== Yahoo Finance Data ====================

    /// <summary>
    /// Current spot price of the stock
    /// </summary>
    [Column("CurrentPrice")]
    public decimal? CurrentPrice { get; set; }

    // ==================== Exante Data (PUT Options) ====================

    /// <summary>
    /// PUT option strike price
    /// </summary>
    [Column("StrikePrice")]
    public decimal? StrikePrice { get; set; }

    /// <summary>
    /// PUT option expiration date
    /// </summary>
    [Column("Expiry")]
    public DateTime? Expiry { get; set; }

    /// <summary>
    /// Days until option expiration
    /// </summary>
    [Column("DaysToExpiry")]
    public int? DaysToExpiry { get; set; }

    /// <summary>
    /// Option premium (price of the PUT contract)
    /// </summary>
    [Column("Premium")]
    public decimal? Premium { get; set; }

    /// <summary>
    /// Breakeven price (StrikePrice - Premium)
    /// </summary>
    [Column("Breakeven")]
    public decimal? Breakeven { get; set; }

    /// <summary>
    /// Strategy confidence score (0-1 scale)
    /// </summary>
    [Column("Confidence")]
    public decimal? Confidence { get; set; }

    /// <summary>
    /// Expected growth percentage
    /// </summary>
    [Column("ExpectedGrowthPercent")]
    public decimal? ExpectedGrowthPercent { get; set; }

    /// <summary>
    /// Strategy name that generated this recommendation
    /// (ShortTermPut, DividendMomentum, VolatilityCrush, etc.)
    /// </summary>
    [Column("StrategyName")]
    public string? StrategyName { get; set; }

    // Exante-specific fields

    /// <summary>
    /// Exante-specific symbol for the option contract
    /// </summary>
    [Column("ExanteSymbol")]
    public string? ExanteSymbol { get; set; }

    /// <summary>
    /// Option contract price from Exante
    /// </summary>
    [Column("OptionPrice")]
    public decimal? OptionPrice { get; set; }

    /// <summary>
    /// Trading volume
    /// </summary>
    [Column("Volume")]
    public int? Volume { get; set; }

    /// <summary>
    /// Open interest (number of outstanding contracts)
    /// </summary>
    [Column("OpenInterest")]
    public int? OpenInterest { get; set; }
}
