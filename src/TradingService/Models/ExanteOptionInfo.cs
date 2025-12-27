namespace TradingService.Models;

/// <summary>
/// Represents option metadata from Exante API
/// </summary>
public record ExanteOptionInfo
{
    /// <summary>
    /// Full option symbol from Exante (e.g., "AAPL.NASDAQ_250117_P_180")
    /// </summary>
    public string SymbolId { get; init; } = string.Empty;

    /// <summary>
    /// Underlying stock symbol (e.g., "AAPL")
    /// </summary>
    public string UnderlyingSymbol { get; init; } = string.Empty;

    /// <summary>
    /// Exchange where the option is traded (e.g., "NASDAQ")
    /// </summary>
    public string Exchange { get; init; } = string.Empty;

    /// <summary>
    /// Type of option (Put or Call)
    /// </summary>
    public OptionType OptionType { get; init; }

    /// <summary>
    /// Strike price of the option
    /// </summary>
    public decimal Strike { get; init; }

    /// <summary>
    /// Expiration date of the option
    /// </summary>
    public DateTime Expiry { get; init; }

    /// <summary>
    /// Daily trading volume (optional, may not be available from all endpoints)
    /// </summary>
    public int? Volume { get; init; }

    /// <summary>
    /// Open interest (optional, may not be available from all endpoints)
    /// </summary>
    public int? OpenInterest { get; init; }

    /// <summary>
    /// Days until expiration
    /// </summary>
    public int DaysToExpiry => (int)(Expiry - DateTime.Today).TotalDays;
}

/// <summary>
/// Type of option contract
/// </summary>
public enum OptionType
{
    Unknown = 0,
    Put = 1,
    Call = 2
}
