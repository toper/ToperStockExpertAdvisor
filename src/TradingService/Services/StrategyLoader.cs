using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingService.Services.Interfaces;

namespace TradingService.Services;

/// <summary>
/// Loads and manages trading strategies dynamically
/// </summary>
public class StrategyLoader : IStrategyLoader
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StrategyLoader> _logger;

    public StrategyLoader(
        IServiceProvider serviceProvider,
        ILogger<StrategyLoader> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IEnumerable<IStrategy> LoadAllStrategies()
    {
        var strategies = new List<IStrategy>();

        try
        {
            _logger.LogInformation("Loading all registered trading strategies");

            // Get all registered strategies from DI container
            var registeredStrategies = _serviceProvider.GetServices<IStrategy>();

            foreach (var strategy in registeredStrategies)
            {
                strategies.Add(strategy);
                _logger.LogInformation(
                    "Loaded strategy: {Name} - {Description} (Expiry: {MinDays}-{MaxDays} days)",
                    strategy.Name,
                    strategy.Description,
                    strategy.TargetExpiryMinDays,
                    strategy.TargetExpiryMaxDays);
            }

            if (!strategies.Any())
            {
                _logger.LogWarning("No strategies were loaded. Check DI registration.");
            }
            else
            {
                _logger.LogInformation("Successfully loaded {Count} strategies", strategies.Count);
            }

            return strategies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading strategies");
            return strategies;
        }
    }
}