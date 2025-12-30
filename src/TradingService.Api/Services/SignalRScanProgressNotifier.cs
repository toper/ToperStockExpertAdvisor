using Microsoft.AspNetCore.SignalR;
using TradingService.Api.Hubs;
using TradingService.Data.Entities;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Api.Services;

/// <summary>
/// Implementation of IScanProgressNotifier using SignalR
/// </summary>
public class SignalRScanProgressNotifier : IScanProgressNotifier
{
    private readonly IHubContext<ScanProgressHub> _hubContext;
    private readonly ILogger<SignalRScanProgressNotifier> _logger;
    private readonly ScanStateTracker _stateTracker;

    public SignalRScanProgressNotifier(
        IHubContext<ScanProgressHub> hubContext,
        ILogger<SignalRScanProgressNotifier> logger,
        ScanStateTracker stateTracker)
    {
        _hubContext = hubContext;
        _logger = logger;
        _stateTracker = stateTracker;
    }

    public async Task NotifyScanStartedAsync(int scanLogId, int totalSymbols)
    {
        try
        {
            // Update state tracker
            _stateTracker.StartScan(scanLogId, totalSymbols);

            await _hubContext.Clients.All.SendAsync("ScanStarted", new
            {
                ScanLogId = scanLogId,
                TotalSymbols = totalSymbols,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Notified clients: Scan started (ID: {ScanLogId}, Symbols: {TotalSymbols})",
                scanLogId, totalSymbols);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send ScanStarted notification");
            // Don't throw - notifications are fire-and-forget
        }
    }

    public async Task NotifySymbolScanningAsync(ScanProgressUpdate update)
    {
        try
        {
            // Update state tracker
            _stateTracker.UpdateProgress(update.Symbol, update.CurrentIndex);

            await _hubContext.Clients.All.SendAsync("SymbolScanning", update);

            _logger.LogDebug("Notified clients: Scanning {Symbol} ({CurrentIndex}/{TotalSymbols})",
                update.Symbol, update.CurrentIndex + 1, update.TotalSymbols);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SymbolScanning notification for {Symbol}", update.Symbol);
        }
    }

    public async Task NotifySymbolCompletedAsync(ScanProgressUpdate update)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("SymbolCompleted", update);

            _logger.LogDebug("Notified clients: Completed {Symbol} ({RecommendationsCount} recommendations)",
                update.Symbol, update.RecommendationsCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SymbolCompleted notification for {Symbol}", update.Symbol);
        }
    }

    public async Task NotifySymbolErrorAsync(ScanProgressUpdate update)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("SymbolError", update);

            _logger.LogDebug("Notified clients: Error scanning {Symbol}: {ErrorMessage}",
                update.Symbol, update.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SymbolError notification for {Symbol}", update.Symbol);
        }
    }

    public async Task NotifyScanCompletedAsync(ScanLog scanLog)
    {
        try
        {
            // Update state tracker
            _stateTracker.CompleteScan();

            await _hubContext.Clients.All.SendAsync("ScanCompleted", new
            {
                scanLog.Id,
                scanLog.StartedAt,
                scanLog.CompletedAt,
                scanLog.SymbolsScanned,
                scanLog.RecommendationsGenerated,
                scanLog.Status,
                scanLog.ErrorMessage,
                Duration = scanLog.CompletedAt.HasValue
                    ? (scanLog.CompletedAt.Value - scanLog.StartedAt).TotalSeconds
                    : 0
            });

            _logger.LogInformation("Notified clients: Scan completed (ID: {ScanLogId}, Status: {Status})",
                scanLog.Id, scanLog.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send ScanCompleted notification");
        }
    }
}
