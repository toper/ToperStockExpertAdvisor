namespace TradingService.Models;

public record HistoricalQuote
{
    public DateTime Date { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal AdjustedClose { get; init; }
    public long Volume { get; init; }
}
