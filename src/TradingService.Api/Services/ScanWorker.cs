using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Services.Interfaces;

namespace TradingService.Api.Services;

/// <summary>
/// Background worker that executes daily market scans at scheduled time with SignalR notifications
/// </summary>
public class ScanWorker : BackgroundService
{
    private readonly ILogger<ScanWorker> _logger;
    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly AppSettings _settings;
    private readonly ScanStateTracker _stateTracker;
    private DateTime? _lastScanTime = null;

    public ScanWorker(
        ILogger<ScanWorker> logger,
        IServiceProvider services,
        IHostEnvironment environment,
        IOptions<AppSettings> settings,
        ScanStateTracker stateTracker)
    {
        _logger = logger;
        _services = services;
        _environment = environment;
        _settings = settings.Value;
        _stateTracker = stateTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScanWorker started at: {Time}", DateTimeOffset.Now);
        _logger.LogInformation("Configured scan time: {ScanTime}", _settings.ScanTime);
        _logger.LogInformation("Watchlist: {Watchlist}", string.Join(", ", _settings.Watchlist));

        // Execute first scan immediately on startup (only in development)
        if (_environment.IsDevelopment() && _lastScanTime == null)
        {
            _logger.LogInformation("Executing IMMEDIATE first scan on startup (Development mode)...");
            await ExecuteDailyScanAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var scheduledTime = GetNextScanTime(now);
                var delay = scheduledTime - now;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation(
                        "Next scan scheduled at {ScheduledTime} (in {Delay:hh\\:mm\\:ss})",
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
                _logger.LogInformation("ScanWorker cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scan execution");
                // Wait 5 minutes before retry
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("ScanWorker stopped");
    }

    private async Task ExecuteDailyScanAsync(CancellationToken cancellationToken)
    {
        // CRITICAL: Check if a scan is already in progress
        if (_stateTracker.IsScanInProgress)
        {
            var scanDuration = _stateTracker.ScanStartedAt.HasValue
                ? DateTime.UtcNow - _stateTracker.ScanStartedAt.Value
                : TimeSpan.Zero;

            _logger.LogWarning(
                "Skipping scheduled scan - another scan is already in progress " +
                "(started {Duration:hh\\:mm\\:ss} ago, {ScannedCount}/{TotalSymbols} symbols scanned)",
                scanDuration,
                _stateTracker.ScannedCount,
                _stateTracker.TotalSymbols);
            return;
        }

        _logger.LogInformation("Starting daily scan at {Time}", DateTime.Now);

        using var scope = _services.CreateScope();

        try
        {
            var scanService = scope.ServiceProvider.GetService<IDailyScanService>();

            if (scanService != null)
            {
                await scanService.ExecuteScanAsync(cancellationToken);
                _lastScanTime = DateTime.Now;
                _logger.LogInformation("Daily scan completed successfully at {Time}", _lastScanTime);
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

    private DateTime GetNextScanTime(DateTime now)
    {
        if (!TimeSpan.TryParse(_settings.ScanTime, out var scanTime))
        {
            // Default to 04:00
            scanTime = TimeSpan.FromHours(4);
        }

        var today = now.Date;
        var todayScheduledTime = today.Add(scanTime);

        // If scan time today has passed OR we just executed a scan, schedule for tomorrow
        if (now >= todayScheduledTime || (_lastScanTime.HasValue && _lastScanTime.Value.Date == today))
        {
            return today.AddDays(1).Add(scanTime);
        }

        return todayScheduledTime;
    }
}
