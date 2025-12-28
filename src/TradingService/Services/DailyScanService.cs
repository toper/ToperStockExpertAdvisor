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
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IDbContextFactory _dbContextFactory;
    private readonly ILogger<DailyScanService> _logger;
    private readonly AppSettings _appSettings;
    private readonly IOptionsDiscoveryService? _optionsDiscoveryService;
    private readonly IFinancialHealthService _financialHealthService;

    public DailyScanService(
        IMarketDataAggregator marketDataAggregator,
        IStrategyLoader strategyLoader,
        IRecommendationRepository recommendationRepository,
        IDbContextFactory dbContextFactory,
        ILogger<DailyScanService> logger,
        IOptions<AppSettings> appSettings,
        IFinancialHealthService financialHealthService,
        IOptionsDiscoveryService? optionsDiscoveryService = null)
    {
        _marketDataAggregator = marketDataAggregator;
        _strategyLoader = strategyLoader;
        _recommendationRepository = recommendationRepository;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _appSettings = appSettings.Value;
        _financialHealthService = financialHealthService;
        _optionsDiscoveryService = optionsDiscoveryService;
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

            // Load all active strategies
            var strategies = _strategyLoader.LoadAllStrategies().ToList();
            if (!strategies.Any())
            {
                throw new InvalidOperationException("No strategies available for scanning");
            }

            _logger.LogInformation("Loaded {Count} strategies for scanning", strategies.Count);

            // Phase 1: Get symbols to scan (now uses fast /groups endpoint)
            var allSymbols = await GetSymbolsToScanAsync(cancellationToken);
            if (!allSymbols.Any())
            {
                _logger.LogWarning("No symbols in watchlist. Using default symbols from configuration.");
                allSymbols = _appSettings.Watchlist;
            }

            _logger.LogInformation("Discovered {Count} symbols for scanning", allSymbols.Count);

            // Phase 2: Pre-filter by financial health (NEW - BATCH)
            var symbols = allSymbols;
            if (_appSettings.FinancialHealth.EnablePreFiltering)
            {
                _logger.LogInformation("Pre-filtering symbols by financial health...");

                var healthMetrics = await _financialHealthService.CalculateMetricsBatchAsync(
                    allSymbols, cancellationToken);

                var healthySymbols = healthMetrics
                    .Where(kvp => _financialHealthService.MeetsHealthRequirements(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToList();

                _logger.LogInformation(
                    "Pre-filtered to {Healthy}/{Total} healthy symbols (F-Score ≥ {MinF}, Z-Score ≥ {MinZ})",
                    healthySymbols.Count, allSymbols.Count,
                    _appSettings.FinancialHealth.MinPiotroskiFScore,
                    _appSettings.FinancialHealth.MinAltmanZScore);

                symbols = healthySymbols;

                if (!symbols.Any())
                {
                    _logger.LogWarning("No symbols met financial health requirements. Scan aborted.");
                    scanLog.CompletedAt = DateTime.UtcNow;
                    scanLog.Status = "CompletedNoHealthySymbols";
                    using (var db = _dbContextFactory.Create())
                    {
                        await db.ScanLogs
                            .Where(s => s.Id == scanLog.Id)
                            .Set(s => s.CompletedAt, scanLog.CompletedAt)
                            .Set(s => s.Status, scanLog.Status)
                            .UpdateAsync();
                    }
                    return;
                }
            }
            else
            {
                _logger.LogInformation("Financial health pre-filtering is disabled");
            }

            _logger.LogInformation("Scanning {Count} symbols: {Symbols}",
                symbols.Count, string.Join(", ", symbols.Take(20)));

            // Deactivate old recommendations before starting new scan
            await _recommendationRepository.DeactivateOldRecommendationsAsync(DateTime.UtcNow.AddDays(-1));

            // Collect all recommendations
            var allRecommendations = new List<PutRecommendation>();
            var symbolsProcessed = 0;
            var errors = new List<string>();

            // Process each symbol
            foreach (var symbol in symbols)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Scan cancelled by user");
                    scanLog.Status = "Cancelled";
                    break;
                }

                try
                {
                    _logger.LogInformation("Processing symbol: {Symbol}", symbol);

                    // Get aggregated market data
                    var marketData = await _marketDataAggregator.GetFullMarketDataAsync(symbol);

                    if (marketData.MarketData == null)
                    {
                        _logger.LogWarning("No market data available for {Symbol}, skipping", symbol);
                        errors.Add($"{symbol}: No market data");
                        continue;
                    }

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
                                allRecommendations.AddRange(recommendations);

                                _logger.LogInformation(
                                    "{Strategy} generated {Count} recommendations for {Symbol}",
                                    strategy.Name, recommendations.Count(), symbol);
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

                    symbolsProcessed++;

                    // Add a small delay to avoid overwhelming APIs
                    await Task.Delay(500, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing symbol {Symbol}", symbol);
                    errors.Add($"{symbol}: {ex.Message}");
                }
            }

            // Save all recommendations
            var savedCount = 0;
            if (allRecommendations.Any())
            {
                try
                {
                    // Filter for high-confidence recommendations only
                    var highConfidenceRecommendations = allRecommendations
                        .Where(r => r.Confidence >= _appSettings.Strategy.MinConfidence)
                        .OrderByDescending(r => r.Confidence)
                        .ThenBy(r => r.Symbol)
                        .ThenBy(r => r.DaysToExpiry)
                        .ToList();

                    if (highConfidenceRecommendations.Any())
                    {
                        savedCount = await _recommendationRepository.AddRangeAsync(highConfidenceRecommendations);

                        _logger.LogInformation(
                            "Saved {SavedCount} high-confidence recommendations out of {TotalCount} generated",
                            savedCount, allRecommendations.Count);

                        // Log top recommendations
                        var topRecommendations = highConfidenceRecommendations
                            .Take(10)
                            .Select(r => $"{r.Symbol} PUT ${r.StrikePrice} {r.Expiry:MM/dd} (Conf: {r.Confidence:P})");

                        _logger.LogInformation("Top recommendations: {Recommendations}",
                            string.Join(", ", topRecommendations));
                    }
                    else
                    {
                        _logger.LogWarning(
                            "All {Count} generated recommendations had confidence below threshold ({Threshold:P})",
                            allRecommendations.Count, _appSettings.Strategy.MinConfidence);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving recommendations to database");
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
                return watchlistSymbols.Distinct().ToList();
            }

            // If watchlist is empty, use configuration
            return _appSettings.Watchlist.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading watchlist from database, using configuration");
            return _appSettings.Watchlist.ToList();
        }
    }
}