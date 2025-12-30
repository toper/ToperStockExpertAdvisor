using LinqToDB.Mapping;

namespace TradingService.Data.Entities;

/// <summary>
/// Entity for storing bulk financial data from SimFin with calculated health metrics
/// Used for pre-filtering symbols before querying Exante API for options
/// </summary>
[Table("CompanyFinancials")]
public class CompanyFinancial
{
    /// <summary>
    /// Primary key
    /// </summary>
    [PrimaryKey, Identity]
    public int Id { get; set; }

    /// <summary>
    /// Stock ticker symbol (e.g., "AAPL", "MSFT")
    /// </summary>
    [Column(CanBeNull = false), NotNull]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Financial reporting period date (e.g., Q4 2024)
    /// </summary>
    [Column(CanBeNull = false)]
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// When this financial data was fetched from SimFin
    /// </summary>
    [Column(CanBeNull = false)]
    public DateTime FetchedAt { get; set; }

    // ==================== CALCULATED HEALTH METRICS ====================

    /// <summary>
    /// Piotroski F-Score (0-9 scale) - measures financial strength
    /// Score > 7 indicates fundamentally strong company
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? PiotroskiFScore { get; set; }

    /// <summary>
    /// Altman Z-Score - bankruptcy risk indicator
    /// Z > 2.99: Safe, 1.81-2.99: Grey Zone, < 1.81: Distress
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? AltmanZScore { get; set; }

    /// <summary>
    /// Return on Assets (ROA) - profitability metric
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? ROA { get; set; }

    /// <summary>
    /// Debt-to-Equity ratio - leverage indicator
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? DebtToEquity { get; set; }

    /// <summary>
    /// Current Ratio - liquidity indicator
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? CurrentRatio { get; set; }

    /// <summary>
    /// Market capitalization in billions
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? MarketCapBillions { get; set; }

    // ==================== FUNDAMENTAL FINANCIAL DATA ====================

    /// <summary>
    /// Total assets from balance sheet
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? TotalAssets { get; set; }

    /// <summary>
    /// Total liabilities from balance sheet
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? TotalLiabilities { get; set; }

    /// <summary>
    /// Total equity from balance sheet
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? TotalEquity { get; set; }

    /// <summary>
    /// Revenue from income statement
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? Revenue { get; set; }

    /// <summary>
    /// Net income from income statement
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? NetIncome { get; set; }

    /// <summary>
    /// Operating cash flow from cash flow statement
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? OperatingCashFlow { get; set; }

    /// <summary>
    /// Shares outstanding (basic) for market cap calculation
    /// </summary>
    [Column(CanBeNull = true)]
    public decimal? SharesOutstanding { get; set; }
}
