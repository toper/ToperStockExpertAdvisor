using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data.Entities;
using TradingService.Services.Integrations;
using TradingService.Services.Interfaces;

namespace TradingService.Services;

/// <summary>
/// Service for processing bulk financial data from SimFin
/// Downloads CSV data, calculates F-Score/Z-Score for all companies, and stores in database
/// </summary>
public class BulkFinancialDataProcessor : IBulkFinancialDataProcessor
{
    private readonly ISimFinDataProvider _simFinProvider;
    private readonly IFinancialHealthService _financialHealthService;
    private readonly ICompanyFinancialRepository _repository;
    private readonly ILogger<BulkFinancialDataProcessor> _logger;
    private readonly AppSettings _appSettings;
    private readonly string _simFinCacheDir;

    public BulkFinancialDataProcessor(
        ISimFinDataProvider simFinProvider,
        IFinancialHealthService financialHealthService,
        ICompanyFinancialRepository repository,
        ILogger<BulkFinancialDataProcessor> logger,
        IOptions<AppSettings> appSettings)
    {
        _simFinProvider = simFinProvider;
        _financialHealthService = financialHealthService;
        _repository = repository;
        _logger = logger;
        _appSettings = appSettings.Value;
        _simFinCacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "simfin");
    }

    public async Task<BulkProcessingResult> ProcessAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var healthyCount = 0;
        var unhealthyCount = 0;
        var failedCount = 0;

        try
        {
            _logger.LogInformation("Starting bulk financial data processing...");

            // Step 1: Trigger bulk CSV download by calling SimFin for a dummy symbol
            // This ensures the CSV files are downloaded and cached
            _logger.LogInformation("Ensuring SimFin bulk CSV data is downloaded...");
            await _simFinProvider.GetCompanyDataAsync("AAPL", cancellationToken); // Trigger download
            _logger.LogInformation("SimFin bulk CSV data is available");

            // Step 2: Get all unique symbols from the income statement CSV
            var allSymbols = await GetAllSymbolsFromCsvAsync(cancellationToken);
            _logger.LogInformation("Found {Count} unique symbols in SimFin bulk data", allSymbols.Count);

            if (!allSymbols.Any())
            {
                _logger.LogWarning("No symbols found in SimFin CSV data");
                return new BulkProcessingResult
                {
                    TotalSymbolsProcessed = 0,
                    HealthySymbols = 0,
                    UnhealthySymbols = 0,
                    FailedSymbols = 0,
                    ProcessingTime = stopwatch.Elapsed
                };
            }

            // Step 3: Process symbols in batches
            var batchSize = _appSettings.FinancialHealth.BulkProcessingBatchSize;
            var batches = allSymbols.Chunk(batchSize).ToList();

            _logger.LogInformation("Processing {TotalSymbols} symbols in {BatchCount} batches of {BatchSize}",
                allSymbols.Count, batches.Count, batchSize);

            for (var i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                var batchNumber = i + 1;

                _logger.LogInformation("Processing batch {BatchNumber}/{TotalBatches} ({Count} symbols)...",
                    batchNumber, batches.Count, batch.Length);

                try
                {
                    // Calculate health metrics for this batch
                    var metricsMap = await _financialHealthService.CalculateMetricsBatchAsync(
                        batch,
                        cancellationToken);

                    // Map to CompanyFinancial entities
                    var financials = new List<CompanyFinancial>();
                    var fetchedAt = DateTime.UtcNow;

                    foreach (var (symbol, metrics) in metricsMap)
                    {
                        try
                        {
                            // Get the report date from the CSV (current period)
                            var reportDate = await GetMostRecentReportDateAsync(symbol, cancellationToken);

                            var financial = new CompanyFinancial
                            {
                                Symbol = symbol,
                                ReportDate = reportDate ?? DateTime.UtcNow.AddMonths(-3), // Default to 3 months ago if not found
                                FetchedAt = fetchedAt,
                                PiotroskiFScore = metrics.PiotroskiFScore,
                                AltmanZScore = metrics.AltmanZScore,
                                ROA = metrics.ROA,
                                DebtToEquity = metrics.DebtToEquity,
                                CurrentRatio = metrics.CurrentRatio,
                                MarketCapBillions = metrics.MarketCapBillions,
                                // Add fundamental data fields if available
                                // (These would need to be added to FinancialHealthMetrics or fetched separately)
                            };

                            financials.Add(financial);

                            // Count healthy vs unhealthy
                            if (metrics.PiotroskiFScore > _appSettings.FinancialHealth.MinPiotroskiFScore)
                            {
                                healthyCount++;
                            }
                            else
                            {
                                unhealthyCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to process metrics for symbol {Symbol}", symbol);
                            failedCount++;
                        }
                    }

                    // Bulk save to database
                    if (financials.Any())
                    {
                        await _repository.BulkInsertOrUpdateAsync(financials, cancellationToken);
                        _logger.LogInformation(
                            "Saved batch {BatchNumber}/{TotalBatches}: {Saved} records (Total healthy: {Healthy}, unhealthy: {Unhealthy}, failed: {Failed})",
                            batchNumber, batches.Count, financials.Count, healthyCount, unhealthyCount, failedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch {BatchNumber}/{TotalBatches}",
                        batchNumber, batches.Count);
                    failedCount += batch.Length;
                }

                // Small delay between batches to avoid overwhelming APIs
                if (batchNumber < batches.Count)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }

            stopwatch.Stop();

            var result = new BulkProcessingResult
            {
                TotalSymbolsProcessed = allSymbols.Count - failedCount,
                HealthySymbols = healthyCount,
                UnhealthySymbols = unhealthyCount,
                FailedSymbols = failedCount,
                ProcessingTime = stopwatch.Elapsed
            };

            _logger.LogInformation(
                "Bulk processing completed: {Total} symbols processed, {Healthy} healthy (F-Score > {MinFScore}), {Unhealthy} unhealthy, {Failed} failed, Duration: {Duration:F1}s",
                result.TotalSymbolsProcessed,
                result.HealthySymbols,
                _appSettings.FinancialHealth.MinPiotroskiFScore,
                result.UnhealthySymbols,
                result.FailedSymbols,
                result.ProcessingTime.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fatal error during bulk processing");
            throw;
        }
    }

    /// <summary>
    /// Get all unique symbols from SimFin CSV files
    /// Reads the income statement CSV as it's the most comprehensive
    /// </summary>
    private async Task<List<string>> GetAllSymbolsFromCsvAsync(CancellationToken cancellationToken)
    {
        var csvPath = Path.Combine(_simFinCacheDir, "us-income-quarterly.csv");
        if (!File.Exists(csvPath))
        {
            _logger.LogWarning("SimFin income CSV not found at {Path}", csvPath);
            return new List<string>();
        }

        try
        {
            var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            });

            // Read all records and collect unique tickers
            await foreach (var record in csv.GetRecordsAsync<IncomeStatementCsvRecord>(cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(record.Ticker))
                {
                    symbols.Add(record.Ticker);
                }
            }

            _logger.LogInformation("Extracted {Count} unique symbols from SimFin CSV", symbols.Count);
            return symbols.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading symbols from SimFin CSV");
            return new List<string>();
        }
    }

    /// <summary>
    /// Get the most recent report date for a symbol from CSV
    /// </summary>
    private async Task<DateTime?> GetMostRecentReportDateAsync(string symbol, CancellationToken cancellationToken)
    {
        var csvPath = Path.Combine(_simFinCacheDir, "us-income-quarterly.csv");
        if (!File.Exists(csvPath))
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            });

            var dates = new List<DateTime>();
            await foreach (var record in csv.GetRecordsAsync<IncomeStatementCsvRecord>(cancellationToken))
            {
                if (string.Equals(record.Ticker, symbol, StringComparison.OrdinalIgnoreCase))
                {
                    dates.Add(record.ReportDate);
                }
            }

            return dates.OrderByDescending(d => d).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting report date for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// Minimal CSV record class for extracting symbol and report date
    /// </summary>
    private class IncomeStatementCsvRecord
    {
        [CsvHelper.Configuration.Attributes.Name("Ticker")]
        public string Ticker { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Report Date")]
        public DateTime ReportDate { get; set; }
    }
}
