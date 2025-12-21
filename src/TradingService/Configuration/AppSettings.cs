namespace TradingService.Configuration;

public class AppSettings
{
    public string ScanTime { get; set; } = "04:00";
    public List<string> Watchlist { get; set; } = ["SPY", "QQQ", "AAPL", "MSFT", "GOOGL", "NVDA"];
    public StrategySettings Strategy { get; set; } = new();
    public ConsulSettings Consul { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public BrokerSettings Broker { get; set; } = new();
}

public class StrategySettings
{
    public int MinExpiryDays { get; set; } = 14;
    public int MaxExpiryDays { get; set; } = 21;
    public decimal MinConfidence { get; set; } = 0.6m;
}

public class ConsulSettings
{
    public string Host { get; set; } = "http://localhost:8500";
    public string ServiceName { get; set; } = "TradingService";
    public int ServicePort { get; set; } = 5001;
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = "Data Source=trading.db";
}

public class BrokerSettings
{
    public string DefaultBroker { get; set; } = "Exante";
    public ExanteBrokerSettings Exante { get; set; } = new();
}

public class ExanteBrokerSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Environment { get; set; } = "Demo"; // Demo or Live
    public string BaseUrl { get; set; } = "https://api-demo.exante.eu";
}
