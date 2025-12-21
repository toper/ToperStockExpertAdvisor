using LinqToDB.Mapping;

namespace TradingService.Data.Entities;

[Table("Recommendations")]
public class PutRecommendation
{
    [PrimaryKey, Identity]
    public int Id { get; set; }

    [Column, NotNull]
    public string Symbol { get; set; } = string.Empty;

    [Column]
    public decimal CurrentPrice { get; set; }

    [Column]
    public decimal StrikePrice { get; set; }

    [Column]
    public DateTime Expiry { get; set; }

    [Column]
    public int DaysToExpiry { get; set; }

    [Column]
    public decimal Premium { get; set; }

    [Column]
    public decimal Breakeven { get; set; }

    [Column]
    public decimal Confidence { get; set; }

    [Column]
    public decimal ExpectedGrowthPercent { get; set; }

    [Column]
    public string StrategyName { get; set; } = string.Empty;

    [Column]
    public DateTime ScannedAt { get; set; }

    [Column]
    public bool IsActive { get; set; } = true;
}
