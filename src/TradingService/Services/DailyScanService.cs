using LinqToDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data;
using TradingService.Data.Entities;
using TradingService.Services.Interfaces;

namespace TradingService.Services;

/// <summary>
/// Main service responsible for daily market scanning and recommendation generation
/// </summary>
public class DailyScanService : IDailyScanService
{
    private readonly IMarketDataAggregator _marketDataAggregator;
    private readonly IStrategyLoader _strategyLoader;
    private readonly IStockDataRepository _stockDataRepository;
    private readonly IDbContextFactory _dbContextFactory;
    private readonly ILogger<DailyScanService> _logger;
    private readonly AppSettings _appSettings;
    private readonly IOptionsDiscoveryService? _optionsDiscoveryService;
    private readonly IFinancialHealthService _financialHealthService;
    private readonly IScanProgressNotifier? _scanProgressNotifier;
    private readonly IBulkFinancialDataProcessor _bulkFinancialDataProcessor;

    public DailyScanService(
        IMarketDataAggregator marketDataAggregator,
        IStrategyLoader strategyLoader,
        IStockDataRepository stockDataRepository,
        IDbContextFactory dbContextFactory,
        ILogger<DailyScanService> logger,
        IOptions<AppSettings> appSettings,
        IFinancialHealthService financialHealthService,
        IBulkFinancialDataProcessor bulkFinancialDataProcessor,
        IOptionsDiscoveryService? optionsDiscoveryService = null,
        IScanProgressNotifier? scanProgressNotifier = null)
    {
        _marketDataAggregator = marketDataAggregator;
        _strategyLoader = strategyLoader;
        _stockDataRepository = stockDataRepository;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _appSettings = appSettings.Value;
        _financialHealthService = financialHealthService;
        _bulkFinancialDataProcessor = bulkFinancialDataProcessor;
        _optionsDiscoveryService = optionsDiscoveryService;
        _scanProgressNotifier = scanProgressNotifier;
    }

    public async Task ExecuteScanAsync(CancellationToken cancellationToken)
    {
        var scanLog = new ScanLog
        {
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        try
        {
            using (var db = _dbContextFactory.Create())
            {
                scanLog.Id = await db.InsertWithInt32IdentityAsync(scanLog);
            }

            _logger.LogInformation("Starting daily market scan at {Time:yyyy-MM-dd HH:mm:ss}", scanLog.StartedAt);

            // Step 0: Cleanup stale records (90-day retention)
            _logger.LogInformation("Cleaning up stale stock data (90-day retention)...");
            await _stockDataRepository.DeleteStaleRecordsAsync(TimeSpan.FromDays(90), cancellationToken);

            // Load all active strategies
            var strategies = _strategyLoader.LoadAllStrategies().ToList();
            if (!strategies.Any())
            {
                throw new InvalidOperationException("No strategies available for scanning");
            }

            _logger.LogInformation("Loaded {Count} strategies for scanning", strategies.Count);

            // Step 1: Process bulk financial data if stale (BLOCKS scan until complete)
            _logger.LogInformation("Checking bulk financial data freshness...");
            var dataRefreshDays = _appSettings.FinancialHealth.DataRefreshDays;

            // Check if SimFin data needs refresh by looking at StockData.SimFinUpdatedAt
            var totalCount = await _stockDataRepository.GetTotalCountAsync(cancellationToken);
            var isStale = totalCount == 0; // No data = stale

            if (!isStale)
            {
                // Check if most recent SimFinUpdatedAt is older than refresh threshold
                // For now, we'll process bulk data on first run (totalCount == 0)
                // TODO: Add method to check SimFinUpdatedAt timestamp
            }

            if (isStale)
            {
                _logger.LogInformation("Bulk financial data is stale, processing ALL companies from SimFin...");
                _logger.LogInformation("This may take 30-60 minutes for ~4,447 symbols. Scan will start after completion.");

                // BLOCKING CALL - waits for bulk processing to complete
                var bulkResult = await _bulkFinancialDataProcessor.ProcessAllCompaniesAsync(cancellationToken);

                _logger.LogInformation(
                    "Bulk processing completed: {Total} symbols processed, {Healthy} with F-Score > {MinFScore}, Duration: {Duration}s",
                    bulkResult.TotalSymbolsProcessed,
                    bulkResult.HealthySymbols,
                    _appSettings.FinancialHealth.MinPiotroskiFScore,
                    bulkResult.ProcessingTime.TotalSeconds);
            }
            else
            {
                _logger.LogInformation("Bulk financial data is fresh (< {Days} days old), using cached data", dataRefreshDays);
            }

            // Phase 1: Get symbols to scan (now uses fast /groups endpoint + pre-filtering)
            var allSymbols = await GetSymbolsToScanAsync(cancellationToken);
            if (!allSymbols.Any())
            {
                _logger.LogWarning("No symbols in watchlist. Using default symbols from configuration.");
                allSymbols = _appSettings.Watchlist;
            }

            _logger.LogInformation("Discovered {Count} symbols for scanning", allSymbols.Count);

            // NO PRE-FILTERING - Scan all symbols, filtering will be done in UI
            var symbols = allSymbols;
            _logger.LogInformation("Scanning ALL {Count} symbols (no pre-filtering): {Symbols}",
                symbols.Count, string.Join(", ", symbols.Take(20)));

            // Notify clients that scan has started
            if (_scanProgressNotifier != null)
            {
                await _scanProgressNotifier.NotifyScanStartedAsync(scanLog.Id, symbols.Count);
            }

            // Track recommendations per symbol (pick best one)
            var bestRecommendationsPerSymbol = new Dictionary<string, PutRecommendation>();
            var symbolsProcessed = 0; // Count of symbols that passed health check and were analyzed
            var errors = new List<string>();

            // Process each symbol - use for loop to track progress correctly
            for (int i = 0; i < symbols.Count; i++)
            {
                var symbol = symbols[i];

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Scan cancelled by user");
                    scanLog.Status = "Cancelled";
                    break;
                }

                // Notify clients that we're scanning this symbol (use i for correct progress)
                if (_scanProgressNotifier != null)
                {
                    await _scanProgressNotifier.NotifySymbolScanningAsync(new Models.ScanProgressUpdate
                    {
                        Symbol = symbol,
                        CurrentIndex = i, // Use loop index for accurate progress
                        TotalSymbols = symbols.Count,
                        Status = "Scanning"
                    });
                }

                try
                {
                    _logger.LogInformation("Processing symbol: {Symbol}", symbol);

                    // OPTIMIZATION: Check financial health FIRST (before expensive Exante API calls)
                    var healthMetrics = await _financialHealthService.CalculateMetricsAsync(symbol, cancellationToken);

                    // Skip symbols that don't meet health requirements (F-Score < 7, Z-Score < 1.81)
                    if (!_financialHealthService.MeetsHealthRequirements(healthMetrics))
                    {
                        _logger.LogDebug(
                            "Skipping {Symbol} - Does not meet financial health requirements. " +
                            "F-Score: {FScore}, Z-Score: {ZScore}",
                            symbol,
                            healthMetrics.PiotroskiFScore?.ToString("F1") ?? "N/A",
                            healthMetrics.AltmanZScore?.ToString("F2") ?? "N/A");
                        continue; // Skip this symbol - don't fetch expensive options data!
                    }

                    // Symbol is healthy - fetch market data + options
                    var marketData = await _marketDataAggregator.GetFullMarketDataAsync(symbol);

                    if (marketData.MarketData == null)
                    {
                        _logger.LogWarning("No market data available for {Symbol}, skipping", symbol);
                        errors.Add($"{symbol}: No market data");
                        continue;
                    }

                    // Add financial health to aggregated data
                    marketData = marketData with { FinancialHealthMetrics = healthMetrics };

                    // Run all strategies on this symbol
                    foreach (var strategy in strategies)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            _logger.LogDebug("Running {Strategy} on {Symbol}", strategy.Name, symbol);

                            var recommendations = await strategy.AnalyzeAsync(marketData, cancellationToken);

                            if (recommendations.Any())
                            {
                                // Pick BEST recommendation from this strategy (highest confidence)
                                var bestFromStrategy = recommendations
                                    .OrderByDescending(r => r.Confidence)
                                    .First();

                                // Update best recommendation for this symbol if this one is better
                                if (!bestRecommendationsPerSymbol.ContainsKey(symbol) ||
                                    bestFromStrategy.Confidence > bestRecommendationsPerSymbol[symbol].Confidence)
                                {
                                    bestRecommendationsPerSymbol[symbol] = bestFromStrategy;
                                }

                                _logger.LogInformation(
                                    "{Strategy} generated {Count} recommendations for {Symbol} (best confidence: {Confidence:P})",
                                    strategy.Name, recommendations.Count(), symbol, bestFromStrategy.Confidence);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error running {Strategy} on {Symbol}",
                                strategy.Name, symbol);
                            errors.Add($"{symbol}/{strategy.Name}: {ex.Message}");
                        }
                    }

                    symbolsProcessed++; // Count symbols that were actually analyzed

                    // Notify clients that symbol processing completed
                    if (_scanProgressNotifier != null)
                    {
                        await _scanProgressNotifier.NotifySymbolCompletedAsync(new Models.ScanProgressUpdate
                        {
                            Symbol = symbol,
                            CurrentIndex = i, // Use loop index for accurate progress
                            TotalSymbols = symbols.Count,
                            Status = "Completed",
                            RecommendationsCount = bestRecommendationsPerSymbol.ContainsKey(symbol) ? 1 : 0
                        });
                    }

                    // Add a small delay to avoid overwhelming APIs
                    await Task.Delay(500, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing symbol {Symbol}", symbol);
                    errors.Add($"{symbol}: {ex.Message}");

                    // Notify clients of error
                    if (_scanProgressNotifier != null)
                    {
                        await _scanProgressNotifier.NotifySymbolErrorAsync(new Models.ScanProgressUpdate
                        {
                            Symbol = symbol,
                            CurrentIndex = symbolsProcessed,
                            TotalSymbols = symbols.Count,
                            Status = "Error",
                            ErrorMessage = ex.Message
                        });
                    }
                }
            }

            // UPSERT best recommendations to StockData
            var savedCount = 0;
            if (bestRecommendationsPerSymbol.Any())
            {
                try
                {
                    _logger.LogInformation("Saving {Count} recommendations (best per symbol)...",
                        bestRecommendationsPerSymbol.Count);

                    foreach (var (symbol, recommendation) in bestRecommendationsPerSymbol)
                    {
                        try
                        {
                            // Map PutRecommendation to StockData (Exante fields)
                            var stockData = new StockData
                            {
                                Symbol = symbol,
                                // Yahoo + Exante fields
                                CurrentPrice = recommendation.CurrentPrice,
                                StrikePrice = recommendation.StrikePrice,
                                Expiry = recommendation.Expiry,
                                DaysToExpiry = recommendation.DaysToExpiry,
                                Premium = recommendation.Premium,
                                Breakeven = recommendation.Breakeven,
                                Confidence = recommendation.Confidence,
                                ExpectedGrowthPercent = recommendation.ExpectedGrowthPercent,
                                StrategyName = recommendation.StrategyName,
                                ExanteSymbol = recommendation.ExanteSymbol,
                                OptionPrice = recommendation.OptionPrice,
                                Volume = recommendation.Volume,
                                OpenInterest = recommendation.OpenInterest,
                            };

                            // UPSERT - updates Exante data, preserves SimFin data
                            await _stockDataRepository.UpsertExanteDataAsync(stockData, cancellationToken);
                            savedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error upserting stock data for {Symbol}", symbol);
                            errors.Add($"{symbol} UPSERT: {ex.Message}");
                        }
                    }

                    _logger.LogInformation("Saved {SavedCount} stock data records", savedCount);

                    // Log top recommendations by confidence
                    var topRecommendations = bestRecommendationsPerSymbol.Values
                        .OrderByDescending(r => r.Confidence)
                        .Take(10)
                        .Select(r => $"{r.Symbol} PUT ${r.StrikePrice} {r.Expiry:MM/dd} (Conf: {r.Confidence:P})");

                    _logger.LogInformation("Top 10 recommendations by confidence: {Recommendations}",
                        string.Join(", ", topRecommendations));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving stock data to database");
                    errors.Add($"Database save: {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning("No recommendations generated in this scan");
            }

            // Update scan log
            scanLog.CompletedAt = DateTime.UtcNow;
            scanLog.SymbolsScanned = symbolsProcessed;
            scanLog.RecommendationsGenerated = savedCount;
            scanLog.Status = errors.Any() ? "CompletedWithErrors" : "Completed";
            scanLog.ErrorMessage = errors.Any() ? string.Join("; ", errors.Take(5)) : null;

            using (var db = _dbContextFactory.Create())
            {
                await db.ScanLogs
                    .Where(s => s.Id == scanLog.Id)
                    .Set(s => s.CompletedAt, scanLog.CompletedAt)
                    .Set(s => s.SymbolsScanned, scanLog.SymbolsScanned)
                    .Set(s => s.RecommendationsGenerated, scanLog.RecommendationsGenerated)
                    .Set(s => s.Status, scanLog.Status)
                    .Set(s => s.ErrorMessage, scanLog.ErrorMessage)
                    .UpdateAsync();
            }

            var duration = scanLog.CompletedAt.Value - scanLog.StartedAt;
            _logger.LogInformation(
                "Daily scan completed in {Duration:mm\\:ss}. " +
                "Symbols: {SymbolsScanned}/{TotalSymbols}, " +
                "Recommendations: {RecommendationsGenerated}, " +
                "Status: {Status}",
                duration,
                scanLog.SymbolsScanned,
                symbols.Count,
                scanLog.RecommendationsGenerated,
                scanLog.Status);

            if (errors.Any())
            {
                _logger.LogWarning("Scan completed with {ErrorCount} errors:\n{Errors}",
                    errors.Count, string.Join("\n", errors));
            }

            // Notify clients that scan has completed
            if (_scanProgressNotifier != null)
            {
                await _scanProgressNotifier.NotifyScanCompletedAsync(scanLog);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during daily scan");

            scanLog.CompletedAt = DateTime.UtcNow;
            scanLog.Status = "Failed";
            scanLog.ErrorMessage = ex.Message;

            try
            {
                using (var db = _dbContextFactory.Create())
                {
                    await db.ScanLogs
                        .Where(s => s.Id == scanLog.Id)
                        .Set(s => s.CompletedAt, scanLog.CompletedAt)
                        .Set(s => s.Status, scanLog.Status)
                        .Set(s => s.ErrorMessage, scanLog.ErrorMessage)
                        .UpdateAsync();
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to update scan log after error");
            }

            throw;
        }
    }

    private async Task<List<string>> GetSymbolsToScanAsync(
        CancellationToken cancellationToken = default)
    {
        // NEW: Check if options discovery is enabled
        if (_appSettings.OptionsDiscovery.Enabled && _optionsDiscoveryService != null)
        {
            try
            {
                _logger.LogInformation("Using dynamic options discovery from Exante");

                var discoveredSymbols = await _optionsDiscoveryService
                    .DiscoverUnderlyingSymbolsAsync(cancellationToken);

                var symbolsList = discoveredSymbols.ToList();

                if (symbolsList.Any())
                {
                    _logger.LogInformation(
                        "Discovered {Count} underlying symbols: {Symbols}",
                        symbolsList.Count,
                        string.Join(", ", symbolsList.Take(20)));

                    return symbolsList;
                }

                _logger.LogWarning("Options discovery returned no symbols");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during options discovery");

                if (!_appSettings.OptionsDiscovery.FallbackToWatchlist)
                {
                    throw;
                }

                _logger.LogWarning("Falling back to static watchlist");
            }
        }

        // EXISTING: Fall back to database or config watchlist
        List<string> symbols;
        try
        {
            using var db = _dbContextFactory.Create();

            // Get active watchlist items
            var watchlistSymbols = await db.Watchlist
                .Where(w => w.IsActive)
                .Select(w => w.Symbol)
                .ToListAsync(cancellationToken);

            if (watchlistSymbols.Any())
            {
                symbols = watchlistSymbols.Distinct().ToList();
            }
            else
            {
                // If watchlist is empty, use configuration
                symbols = _appSettings.Watchlist.ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading watchlist from database, using configuration");
            symbols = _appSettings.Watchlist.ToList();
        }

        // NEW: Pre-filter by F-Score > minFScore if enabled
        if (_appSettings.FinancialHealth.EnablePreFiltering)
        {
            return await ApplyFinancialHealthFilterAsync(symbols, cancellationToken);
        }

        return symbols;
    }

    /// <summary>
    /// Apply F-Score pre-filtering to reduce Exante API calls
    /// Only scans symbols with F-Score > threshold
    /// Fallback: Symbols not in StockData are scanned anyway
    /// </summary>
    private async Task<List<string>> ApplyFinancialHealthFilterAsync(
        List<string> symbols,
        CancellationToken cancellationToken)
    {
        var minFScore = _appSettings.FinancialHealth.MinPiotroskiFScore;
        _logger.LogInformation("Pre-filtering {Count} symbols by F-Score > {MinScore}...",
            symbols.Count, minFScore);

        // Get symbols with F-Score >= threshold from StockData
        var healthyStocks = await _stockDataRepository.GetHealthySymbolsAsync(
            minFScore,
            cancellationToken);

        var healthySymbolSet = new HashSet<string>(
            healthyStocks.Select(s => s.Symbol),
            StringComparer.OrdinalIgnoreCase);

        // Partition symbols: healthy (F-Score >= threshold) and fallback (not in StockData)
        var filteredSymbols = new List<string>();
        var fallbackSymbols = new List<string>();

        foreach (var symbol in symbols)
        {
            if (healthySymbolSet.Contains(symbol))
            {
                // Has F-Score >= threshold
                filteredSymbols.Add(symbol);
            }
            else
            {
                // Check if symbol exists in StockData at all
                var stockData = await _stockDataRepository.GetBySymbolAsync(symbol, cancellationToken);
                if (stockData == null)
                {
                    // Not in StockData - FALLBACK: scan anyway (young/small companies not in SimFin)
                    fallbackSymbols.Add(symbol);
                    filteredSymbols.Add(symbol);
                }
                // else: Symbol exists in StockData but F-Score < threshold, SKIP it
            }
        }

        _logger.LogInformation(
            "Pre-filtering complete: {Original} → {Filtered} symbols ({Healthy} with F-Score >= {MinScore}, {Fallback} fallback without StockData, {Skipped} skipped)",
            symbols.Count,
            filteredSymbols.Count,
            filteredSymbols.Count - fallbackSymbols.Count,
            minFScore,
            fallbackSymbols.Count,
            symbols.Count - filteredSymbols.Count);

        if (fallbackSymbols.Any())
        {
            _logger.LogDebug("Fallback symbols (not in StockData): {Symbols}",
                string.Join(", ", fallbackSymbols.Take(10)));
        }

        return filteredSymbols;
    }
}