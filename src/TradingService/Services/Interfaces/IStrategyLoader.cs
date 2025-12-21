namespace TradingService.Services.Interfaces;

public interface IStrategyLoader
{
    IEnumerable<IStrategy> LoadAllStrategies();
}
