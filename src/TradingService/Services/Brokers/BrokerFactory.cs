using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Brokers;

/// <summary>
/// Factory for creating broker instances based on configuration.
/// Supports multiple broker implementations through a simple string-based lookup.
/// </summary>
public class BrokerFactory : IBrokerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly BrokerSettings _brokerSettings;
    private readonly ILogger<BrokerFactory> _logger;

    public BrokerFactory(
        IServiceProvider serviceProvider,
        IOptions<AppSettings> appSettings,
        ILogger<BrokerFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _brokerSettings = appSettings.Value.Broker;
        _logger = logger;
    }

    public IBroker CreateBroker(string brokerName)
    {
        _logger.LogDebug("Creating broker instance for: {BrokerName}", brokerName);

        var broker = brokerName.ToLowerInvariant() switch
        {
            "exante" => CreateExanteBroker(),
            _ => throw new ArgumentException($"Unknown broker: {brokerName}. Supported brokers: Exante")
        };

        _logger.LogInformation("Created broker instance: {BrokerName}", broker.Name);
        return broker;
    }

    private IBroker CreateExanteBroker()
    {
        var exanteSettings = _brokerSettings.Exante;

        if (string.IsNullOrEmpty(exanteSettings.ApiKey))
        {
            _logger.LogWarning("Exante API key not configured - broker will operate in simulation mode");
        }

        return new ExanteBroker(
            exanteSettings,
            _serviceProvider.GetRequiredService<ILogger<ExanteBroker>>());
    }
}
