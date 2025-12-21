namespace TradingService.Models;

public record MarketData
{
    public string Symbol { get; init; } = string.Empty;
    public decimal CurrentPrice { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
    public decimal AverageVolume { get; init; }
    public decimal High52Week { get; init; }
    public decimal Low52Week { get; init; }
    public decimal MovingAverage50 { get; init; }
    public decimal MovingAverage200 { get; init; }
    public decimal MovingAverage20 { get; init; }
    public decimal RSI { get; init; }
    public decimal MACD { get; init; }
    public decimal MACDSignal { get; init; }
    public DateTime Timestamp { get; init; }
}
