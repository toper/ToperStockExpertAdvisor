namespace TradingService.Models;

public record AggregatedMarketData
{
    public MarketData? MarketData { get; init; }
    public TrendAnalysis? TrendAnalysis { get; init; }
    public IReadOnlyList<OptionContract> ShortTermPutOptions { get; init; } = [];
    public DividendInfo? DividendInfo { get; init; }
    public FinancialHealthMetrics? FinancialHealthMetrics { get; init; }
}
