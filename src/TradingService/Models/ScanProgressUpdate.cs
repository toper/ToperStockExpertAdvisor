namespace TradingService.Models;

/// <summary>
/// DTO for real-time scan progress updates sent via SignalR
/// </summary>
public record ScanProgressUpdate
{
    /// <summary>
    /// Current symbol being scanned
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Current index in the scan (0-based)
    /// </summary>
    public int CurrentIndex { get; init; }

    /// <summary>
    /// Total number of symbols to scan
    /// </summary>
    public int TotalSymbols { get; init; }

    /// <summary>
    /// Status of the current operation: Scanning, Completed, Error
    /// </summary>
    public string Status { get; init; } = "Scanning";

    /// <summary>
    /// Optional financial health metrics for display
    /// </summary>
    public FinancialHealthMetrics? Metrics { get; init; }

    /// <summary>
    /// Timestamp of the update
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional error message if Status is Error
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of recommendations generated for this symbol
    /// </summary>
    public int RecommendationsCount { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public decimal ProgressPercent => TotalSymbols > 0
        ? Math.Round((decimal)CurrentIndex / TotalSymbols * 100, 1)
        : 0;
}
