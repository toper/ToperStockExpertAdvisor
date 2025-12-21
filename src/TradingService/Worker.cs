using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Services.Interfaces;

namespace TradingService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _services;
    private readonly AppSettings _settings;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider services,
        IOptions<AppSettings> settings)
    {
        _logger = logger;
        _services = services;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TradingService Worker started at: {Time}", DateTimeOffset.Now);
        _logger.LogInformation("Configured scan time: {ScanTime}", _settings.ScanTime);
        _logger.LogInformation("Watchlist: {Watchlist}", string.Join(", ", _settings.Watchlist));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var scheduledTime = GetNextScanTime();
                var delay = scheduledTime - now;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation(
                        "Next scan scheduled at {ScheduledTime} (in {Delay})",
                        scheduledTime,
                        delay);

                    await Task.Delay(delay, stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await ExecuteDailyScanAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scan execution");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("TradingService Worker stopped");
    }

    private async Task ExecuteDailyScanAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting daily scan at {Time}", DateTime.Now);

        using var scope = _services.CreateScope();

        try
        {
            var scanService = scope.ServiceProvider.GetService<IDailyScanService>();

            if (scanService != null)
            {
                await scanService.ExecuteScanAsync(cancellationToken);
                _logger.LogInformation("Daily scan completed successfully");
            }
            else
            {
                _logger.LogWarning("IDailyScanService not registered - scan skipped");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily scan failed");
        }
    }

    private DateTime GetNextScanTime()
    {
        var today = DateTime.Today;

        if (TimeSpan.TryParse(_settings.ScanTime, out var scanTime))
        {
            var scheduledTime = today.Add(scanTime);

            if (DateTime.Now > scheduledTime)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            return scheduledTime;
        }

        // Default to 04:00
        var defaultTime = today.AddHours(4);
        if (DateTime.Now > defaultTime)
        {
            defaultTime = defaultTime.AddDays(1);
        }

        return defaultTime;
    }
}
