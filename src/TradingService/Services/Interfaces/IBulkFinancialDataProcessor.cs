namespace TradingService.Services.Interfaces;

/// <summary>
/// Service for processing bulk financial data from SimFin
/// Downloads CSV data, calculates F-Score/Z-Score for all companies, and stores in database
/// </summary>
public interface IBulkFinancialDataProcessor
{
    /// <summary>
    /// Process all companies from SimFin bulk CSV data
    /// This is the main entry point for bulk processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result with statistics</returns>
    Task<BulkProcessingResult> ProcessAllCompaniesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of bulk financial data processing
/// </summary>
public record BulkProcessingResult
{
    /// <summary>
    /// Total number of symbols processed
    /// </summary>
    public int TotalSymbolsProcessed { get; init; }

    /// <summary>
    /// Number of symbols with F-Score > 7 (healthy companies)
    /// </summary>
    public int HealthySymbols { get; init; }

    /// <summary>
    /// Number of symbols with F-Score <= 7 (unhealthy companies)
    /// </summary>
    public int UnhealthySymbols { get; init; }

    /// <summary>
    /// Number of symbols that failed to process
    /// </summary>
    public int FailedSymbols { get; init; }

    /// <summary>
    /// Total processing time
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }
}
