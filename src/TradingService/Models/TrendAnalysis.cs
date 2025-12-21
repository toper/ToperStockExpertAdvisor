namespace TradingService.Models;

public record TrendAnalysis
{
    public string Symbol { get; init; } = string.Empty;
    public decimal ExpectedGrowthPercent { get; init; }
    public decimal TrendStrength { get; init; }
    public TrendDirection Direction { get; init; }
    public decimal Confidence { get; init; }
    public int AnalysisPeriodDays { get; init; }
}

public enum TrendDirection { Up, Down, Sideways }
