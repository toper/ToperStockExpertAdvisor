namespace TradingService.Models;

public record AccountInfo
{
    public string AccountId { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal AvailableMargin { get; init; }
    public decimal UsedMargin { get; init; }
    public string Currency { get; init; } = "USD";
}

public record OrderResult
{
    public bool Success { get; init; }
    public string? OrderId { get; init; }
    public string? Message { get; init; }
    public decimal? FilledPrice { get; init; }
    public int? FilledQuantity { get; init; }
}

public record PutSellOrder
{
    public string OptionSymbol { get; init; } = string.Empty;
    public string UnderlyingSymbol { get; init; } = string.Empty;
    public decimal Strike { get; init; }
    public DateTime Expiry { get; init; }
    public int Quantity { get; init; }
    public decimal LimitPrice { get; init; }
}

public record Position
{
    public string PositionId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal CurrentPrice { get; init; }
    public decimal UnrealizedPnL { get; init; }
}
