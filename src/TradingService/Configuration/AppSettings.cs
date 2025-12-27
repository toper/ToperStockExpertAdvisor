using System.ComponentModel.DataAnnotations;

namespace TradingService.Configuration;

public class AppSettings
{
    [Required]
    public string ScanTime { get; set; } = "04:00";

    [Required]
    public List<string> Watchlist { get; set; } = ["SPY", "QQQ", "AAPL", "MSFT", "GOOGL", "NVDA"];

    [Required]
    public StrategySettings Strategy { get; set; } = new();

    [Required]
    public ConsulSettings Consul { get; set; } = new();

    [Required]
    public DatabaseSettings Database { get; set; } = new();

    [Required]
    public BrokerSettings Broker { get; set; } = new();

    [Required]
    public OptionsDiscoverySettings OptionsDiscovery { get; set; } = new();
}

public class StrategySettings
{
    [Range(1, 365)]
    public int MinExpiryDays { get; set; } = 14;

    [Range(1, 365)]
    public int MaxExpiryDays { get; set; } = 21;

    [Range(0, 1)]
    public decimal MinConfidence { get; set; } = 0.6m;
}

public class ConsulSettings
{
    [Required]
    [Url]
    public string Host { get; set; } = "http://localhost:8500";

    [Required]
    public string ServiceName { get; set; } = "TradingService";

    [Range(1, 65535)]
    public int ServicePort { get; set; } = 5001;
}

public class DatabaseSettings
{
    [Required]
    public string ConnectionString { get; set; } = "Data Source=trading.db";
}

public class BrokerSettings
{
    [Required]
    public string DefaultBroker { get; set; } = "Exante";

    [Required]
    public ExanteBrokerSettings Exante { get; set; } = new();
}

public class ExanteBrokerSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;

    [Required]
    public string Environment { get; set; } = "Demo"; // Demo or Live

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api-demo.exante.eu";

    /// <summary>
    /// JWT token for Bearer authentication (required for market data endpoints)
    /// </summary>
    public string JwtToken { get; set; } = string.Empty;
}

public class OptionsDiscoverySettings
{
    /// <summary>
    /// Enable dynamic options discovery from Exante (replaces static watchlist)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Minimum open interest to consider an option liquid
    /// </summary>
    public int MinOpenInterest { get; set; } = 100;

    /// <summary>
    /// Minimum daily volume to consider an option liquid
    /// </summary>
    public int MinVolume { get; set; } = 50;

    /// <summary>
    /// Number of representative options to check per underlying symbol
    /// </summary>
    public int SampleOptionsPerUnderlying { get; set; } = 3;

    /// <summary>
    /// Fallback to static watchlist if discovery fails
    /// </summary>
    public bool FallbackToWatchlist { get; set; } = true;

    /// <summary>
    /// Fetch both PUTs and CALLs (for future use)
    /// </summary>
    public bool IncludeCallOptions { get; set; } = true;

    /// <summary>
    /// Only discover options expiring within this range
    /// </summary>
    public int MaxExpiryDays { get; set; } = 90;
}
