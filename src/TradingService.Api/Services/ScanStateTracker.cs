namespace TradingService.Api.Services;

/// <summary>
/// Singleton service that tracks the current scan state
/// Used to inform newly connected clients about ongoing scans
/// </summary>
public class ScanStateTracker
{
    private readonly object _lock = new();

    public bool IsScanInProgress { get; private set; }
    public int? CurrentScanLogId { get; private set; }
    public int TotalSymbols { get; private set; }
    public string? CurrentSymbol { get; private set; }
    public int ScannedCount { get; private set; }
    public DateTime? ScanStartedAt { get; private set; }

    public void StartScan(int scanLogId, int totalSymbols)
    {
        lock (_lock)
        {
            IsScanInProgress = true;
            CurrentScanLogId = scanLogId;
            TotalSymbols = totalSymbols;
            CurrentSymbol = null;
            ScannedCount = 0;
            ScanStartedAt = DateTime.UtcNow;
        }
    }

    public void UpdateProgress(string symbol, int currentIndex)
    {
        lock (_lock)
        {
            if (IsScanInProgress)
            {
                CurrentSymbol = symbol;
                ScannedCount = currentIndex + 1;
            }
        }
    }

    public void CompleteScan()
    {
        lock (_lock)
        {
            IsScanInProgress = false;
            CurrentSymbol = null;
        }
    }

    public object? GetCurrentState()
    {
        lock (_lock)
        {
            if (!IsScanInProgress || !CurrentScanLogId.HasValue)
                return null;

            // Return with camelCase property names to match frontend expectations
            return new
            {
                scanLogId = CurrentScanLogId.Value,
                totalSymbols = TotalSymbols,
                currentSymbol = CurrentSymbol,
                scannedCount = ScannedCount,
                timestamp = ScanStartedAt ?? DateTime.UtcNow
            };
        }
    }
}
