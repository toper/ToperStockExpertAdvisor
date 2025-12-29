namespace TradingService.Models;

public record OptionsChain
{
    public string UnderlyingSymbol { get; init; } = string.Empty;
    public IReadOnlyList<OptionContract> PutOptions { get; init; } = [];
    public IReadOnlyList<OptionContract> CallOptions { get; init; } = [];
}

public record OptionContract
{
    public string Symbol { get; init; } = string.Empty;
    public string? ExanteSymbol { get; init; }
    public decimal Strike { get; init; }
    public DateTime Expiry { get; init; }
    public int DaysToExpiry => (int)(Expiry - DateTime.Today).TotalDays;
    public decimal Bid { get; init; }
    public decimal Ask { get; init; }
    public decimal Mid => (Bid + Ask) / 2;
    public decimal ImpliedVolatility { get; init; }
    public int? Volume { get; init; }
    public int? OpenInterest { get; init; }
    public decimal Delta { get; init; }
    public decimal Theta { get; init; }
}
