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

    [Required]
    public FinancialHealthSettings FinancialHealth { get; set; } = new();

    [Required]
    public RateLimitingSettings RateLimiting { get; set; } = new();

    [Required]
    public SimFinSettings SimFin { get; set; } = new();
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
    /// <summary>
    /// Client ID for JWT authentication (obtain from Exante Client Area)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;

    [Required]
    public string Environment { get; set; } = "Demo"; // Demo or Live

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api-demo.exante.eu";
}

public class OptionsDiscoverySettings
{
    /// <summary>
    /// Enable dynamic options discovery from Exante (replaces static watchlist)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Use fast /md/3.0/groups endpoint instead of parsing 1.6M options
    /// </summary>
    public bool UseGroupsEndpoint { get; set; } = true;

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

public class FinancialHealthSettings
{
    /// <summary>
    /// Enable pre-filtering symbols by financial health before fetching expensive market data
    /// </summary>
    public bool EnablePreFiltering { get; set; } = true;

    /// <summary>
    /// Minimum Piotroski F-Score (0-9 scale, higher is better)
    /// </summary>
    [Range(0, 9)]
    public decimal MinPiotroskiFScore { get; set; } = 7m;

    /// <summary>
    /// Minimum Altman Z-Score (>2.99 safe, 1.81-2.99 grey, <1.81 distress)
    /// </summary>
    public decimal MinAltmanZScore { get; set; } = 1.81m;

    /// <summary>
    /// Maximum concurrent health calculations
    /// </summary>
    [Range(1, 20)]
    public int BatchConcurrency { get; set; } = 5;
}

public class RateLimitingSettings
{
    /// <summary>
    /// Enable automatic retry on HTTP 429 (Too Many Requests)
    /// </summary>
    public bool EnableRetryOn429 { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in seconds
    /// </summary>
    [Range(1, 300)]
    public int InitialRetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Use exponential backoff (60s, 120s, 240s) instead of fixed delay
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Request timeout per attempt in seconds (for Polly resilience handler)
    /// </summary>
    [Range(10, 300)]
    public int AttemptTimeoutSeconds { get; set; } = 60;
}

public class SimFinSettings
{
    /// <summary>
    /// SimFin API base URL
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://backend.simfin.com/api/v3";

    /// <summary>
    /// SimFin API key (get from https://simfin.com/)
    /// </summary>
    [Required]
    [MinLength(1)]
    public string ApiKey { get; set; } = null!;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(5, 60)]
    public int TimeoutSeconds { get; set; } = 30;
}
