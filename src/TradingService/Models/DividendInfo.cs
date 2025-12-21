namespace TradingService.Models;

public record DividendInfo
{
    public string Symbol { get; init; } = string.Empty;
    public decimal DividendYield { get; init; }
    public decimal AnnualDividend { get; init; }
    public DateTime? ExDividendDate { get; init; }
    public DateTime? PaymentDate { get; init; }
}
