namespace TradingService.Models;

public record PutRecommendationDto
{
    public int Id { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public decimal CurrentPrice { get; init; }
    public decimal StrikePrice { get; init; }
    public DateTime Expiry { get; init; }
    public int DaysToExpiry { get; init; }
    public decimal Premium { get; init; }
    public decimal Breakeven { get; init; }
    public decimal Confidence { get; init; }
    public decimal ExpectedGrowthPercent { get; init; }
    public string StrategyName { get; init; } = string.Empty;
    public DateTime ScannedAt { get; init; }
    public decimal? PiotroskiFScore { get; init; }
    public decimal? AltmanZScore { get; init; }

    // Calculated properties for frontend
    public decimal PotentialReturn => Premium / StrikePrice * 100;
    public decimal OTMPercent => (CurrentPrice - StrikePrice) / CurrentPrice * 100;
}
