using TradingService.Models;

namespace TradingService.Services.Interfaces;

public interface IBroker
{
    string Name { get; }
    Task<bool> IsConnectedAsync();
    Task<AccountInfo> GetAccountInfoAsync();
    Task<OrderResult> PlacePutSellOrderAsync(PutSellOrder order);
    Task<IEnumerable<Position>> GetPositionsAsync();
    Task<OrderResult> ClosePositionAsync(string positionId);
}

public interface IBrokerFactory
{
    IBroker CreateBroker(string brokerName);
}
