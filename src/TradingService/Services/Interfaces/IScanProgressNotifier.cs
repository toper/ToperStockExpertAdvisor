using TradingService.Data.Entities;
using TradingService.Models;

namespace TradingService.Services.Interfaces;

/// <summary>
/// Interface for notifying clients about scan progress (used to avoid circular dependency with SignalR Hub)
/// </summary>
public interface IScanProgressNotifier
{
    /// <summary>
    /// Notify that a scan has started
    /// </summary>
    Task NotifyScanStartedAsync(int scanLogId, int totalSymbols);

    /// <summary>
    /// Notify that a symbol is being scanned
    /// </summary>
    Task NotifySymbolScanningAsync(ScanProgressUpdate update);

    /// <summary>
    /// Notify that a symbol scan has completed
    /// </summary>
    Task NotifySymbolCompletedAsync(ScanProgressUpdate update);

    /// <summary>
    /// Notify that a symbol scan encountered an error
    /// </summary>
    Task NotifySymbolErrorAsync(ScanProgressUpdate update);

    /// <summary>
    /// Notify that the entire scan has completed
    /// </summary>
    Task NotifyScanCompletedAsync(ScanLog scanLog);
}
