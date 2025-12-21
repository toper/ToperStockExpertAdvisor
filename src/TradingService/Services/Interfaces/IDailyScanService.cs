namespace TradingService.Services.Interfaces;

public interface IDailyScanService
{
    Task ExecuteScanAsync(CancellationToken cancellationToken);
}
