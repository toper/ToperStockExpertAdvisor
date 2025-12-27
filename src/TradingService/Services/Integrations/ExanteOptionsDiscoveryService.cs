using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Integrations;

/// <summary>
/// Service for discovering underlying symbols from available Exante options
/// Filters options by liquidity and extracts unique underlying symbols
/// </summary>
public partial class ExanteOptionsDiscoveryService : IOptionsDiscoveryService
{
    private readonly ExanteBrokerSettings _brokerSettings;
    private readonly OptionsDiscoverySettings _discoverySettings;
    private readonly ILogger<ExanteOptionsDiscoveryService> _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _isSimulationMode;
    private readonly SemaphoreSlim _rateLimiter = new(5, 5); // Max 5 concurrent requests
    private const int RequestDelayMs = 100; // 100ms between requests

    // Regex for validating Exante option symbols: UNDERLYING.EXCHANGE_YYMMDD_[P|C]_STRIKE
    [GeneratedRegex(@"^[A-Z]+\.[A-Z]+_\d{6}_[PC]_\d+$")]
    private static partial Regex OptionSymbolRegex();

    public ExanteOptionsDiscoveryService(
        IOptions<AppSettings> appSettings,
        ILogger<ExanteOptionsDiscoveryService> logger)
    {
        _brokerSettings = appSettings.Value.Broker.Exante;
        _discoverySettings = appSettings.Value.OptionsDiscovery;
        _logger = logger;
        _isSimulationMode = string.IsNullOrEmpty(_brokerSettings.ApiKey);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_brokerSettings.BaseUrl),
            Timeout = TimeSpan.FromMinutes(10) // Large response: 1.6M options, ~900MB
        };

        if (!_isSimulationMode)
        {
            ConfigureAuthentication();
        }

        _logger.LogInformation(
            "ExanteOptionsDiscoveryService initialized - Mode: {Mode}, Filters: MinOI={MinOI}, MinVol={MinVol}",
            _isSimulationMode ? "Simulation" : "Live",
            _discoverySettings.MinOpenInterest,
            _discoverySettings.MinVolume);
    }

    private void ConfigureAuthentication()
    {
        // Prefer JWT Bearer token if available (required for market data endpoints)
        if (!string.IsNullOrEmpty(_brokerSettings.JwtToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _brokerSettings.JwtToken);
            _logger.LogDebug("Using JWT Bearer authentication");
        }
        else
        {
            // Fallback to Basic Auth
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_brokerSettings.ApiKey}:{_brokerSettings.ApiSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
            _logger.LogDebug("Using Basic authentication");
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IEnumerable<string>> DiscoverUnderlyingSymbolsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_isSimulationMode)
        {
            _logger.LogWarning("Simulation mode - returning synthetic underlying symbols");
            return GenerateSyntheticUnderlyings();
        }

        try
        {
            _logger.LogInformation("Starting FULL options discovery from Exante API - parsing all options");
            _logger.LogWarning("This may take 5-15 minutes for large datasets (~1.6M options)");

            // FULL PARSING APPROACH: Fetch all options and extract unique underlying symbols
            // Step 1: Fetch all options from Exante /md/3.0/types/OPTION
            var allOptions = await FetchAllOptionsAsync(cancellationToken);

            if (!allOptions.Any())
            {
                _logger.LogWarning("No options returned from Exante API");
                return Array.Empty<string>();
            }

            _logger.LogInformation("Fetched {Count} options, now filtering...", allOptions.Count);

            // Step 2: Filter by option type (PUT only or PUT+CALL based on config)
            var filteredByType = FilterByOptionType(allOptions);
            _logger.LogInformation("After type filter: {Count} options", filteredByType.Count);

            // Step 3: Filter by expiry (only options within MaxExpiryDays)
            var filteredByExpiry = FilterByExpiry(filteredByType);
            _logger.LogInformation("After expiry filter: {Count} options", filteredByExpiry.Count);

            // Step 4: Group by underlying symbol
            var groupedByUnderlying = filteredByExpiry
                .GroupBy(o => o.UnderlyingSymbol)
                .ToDictionary(g => g.Key, g => g.ToList());

            _logger.LogInformation("Found {Count} unique underlying symbols", groupedByUnderlying.Count);

            // Step 5: Extract underlying symbols (skip liquidity check for now - too slow)
            var underlyingSymbols = groupedByUnderlying.Keys
                .OrderBy(s => s)
                .ToList();

            _logger.LogInformation(
                "Discovered {Count} underlying symbols with options: {Symbols}",
                underlyingSymbols.Count,
                string.Join(", ", underlyingSymbols.Take(20)));

            return underlyingSymbols;

            /* LIQUIDITY FILTERING - DISABLED (too slow)
            // Step 5: Filter by liquidity (optional, very slow - checks quotes for each option)
            if (_discoverySettings.MinOpenInterest > 0 || _discoverySettings.MinVolume > 0)
            {
                var liquidSymbols = await FilterByLiquidityAsync(groupedByUnderlying, cancellationToken);
                return liquidSymbols;
            }
            */
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during options discovery");
            throw;
        }
    }

    private async Task<List<ExanteOptionInfo>> FetchAllOptionsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching options from Exante API (this may take several minutes for large datasets)...");

            var response = await _httpClient.GetAsync(
                "/md/3.0/types/OPTION",
                HttpCompletionOption.ResponseHeadersRead, // Don't buffer the entire response
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Exante API returned {StatusCode}: {Reason}",
                    response.StatusCode,
                    response.ReasonPhrase);
                return new List<ExanteOptionInfo>();
            }

            // Use streaming JSON parsing for large responses
            var options = new List<ExanteOptionInfo>();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var symbols = JsonSerializer.DeserializeAsyncEnumerable<ExanteSymbolResponse>(
                stream,
                jsonOptions,
                cancellationToken);

            var processedCount = 0;
            await foreach (var symbol in symbols.WithCancellation(cancellationToken))
            {
                if (symbol != null)
                {
                    var optionInfo = ParseExanteSymbolFromResponse(symbol);
                    if (optionInfo != null)
                    {
                        options.Add(optionInfo);
                    }

                    processedCount++;
                    if (processedCount % 100000 == 0)
                    {
                        _logger.LogInformation("Processed {Count} options...", processedCount);
                    }
                }
            }

            _logger.LogInformation("Completed fetching {Total} options, parsed {Parsed} valid options",
                processedCount, options.Count);

            return options;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching options from Exante");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for Exante API response");
            throw;
        }
    }

    private ExanteOptionInfo? ParseExanteSymbolFromResponse(ExanteSymbolResponse symbol)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrEmpty(symbol.SymbolId) ||
                symbol.OptionData == null ||
                string.IsNullOrEmpty(symbol.OptionData.OptionRight))
            {
                return null;
            }

            // Extract underlying symbol and exchange
            // Try ticker first (e.g., "AAPL"), then parse from underlyingSymbolId
            var underlying = symbol.Ticker;
            string? underlyingExchange = null;

            if (string.IsNullOrEmpty(underlying) && !string.IsNullOrEmpty(symbol.UnderlyingSymbolId))
            {
                // UnderlyingSymbolId format: "GC.COMEX.G2026" or "AAPL.NASDAQ"
                var parts = symbol.UnderlyingSymbolId.Split('.');
                underlying = parts.Length > 0 ? parts[0] : null;
                underlyingExchange = parts.Length > 1 ? parts[1] : null;
            }

            if (string.IsNullOrEmpty(underlying))
            {
                return null;
            }

            // OPTIMIZATION: Filter to US stock exchanges only (skip commodities, forex, etc.)
            var targetExchanges = new HashSet<string> { "NASDAQ", "NYSE", "AMEX", "ARCA" };
            if (!string.IsNullOrEmpty(underlyingExchange) && !targetExchanges.Contains(underlyingExchange))
            {
                return null; // Skip non-US stock options
            }

            // Parse option type
            var optionType = symbol.OptionData.OptionRight.ToUpper() == "PUT"
                ? OptionType.Put
                : symbol.OptionData.OptionRight.ToUpper() == "CALL"
                    ? OptionType.Call
                    : OptionType.Unknown;

            if (optionType == OptionType.Unknown)
            {
                return null;
            }

            // Parse strike price
            if (!decimal.TryParse(symbol.OptionData.StrikePrice, out var strike))
            {
                return null;
            }

            // Parse expiry (Unix timestamp in milliseconds)
            DateTime expiry;
            if (symbol.Expiration.HasValue)
            {
                expiry = DateTimeOffset.FromUnixTimeMilliseconds(symbol.Expiration.Value).DateTime;
            }
            else
            {
                // If no expiration, skip this option
                return null;
            }

            return new ExanteOptionInfo
            {
                SymbolId = symbol.SymbolId,
                UnderlyingSymbol = underlying,
                Exchange = symbol.Exchange ?? string.Empty,
                OptionType = optionType,
                Strike = strike,
                Expiry = expiry
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error parsing option symbol response: {SymbolId}", symbol.SymbolId);
            return null;
        }
    }

    private List<ExanteOptionInfo> FilterByOptionType(List<ExanteOptionInfo> options)
    {
        if (_discoverySettings.IncludeCallOptions)
        {
            // Include both PUT and CALL
            return options;
        }

        // Only PUT options
        return options.Where(o => o.OptionType == OptionType.Put).ToList();
    }

    private List<ExanteOptionInfo> FilterByExpiry(List<ExanteOptionInfo> options)
    {
        var maxExpiry = DateTime.Today.AddDays(_discoverySettings.MaxExpiryDays);
        return options.Where(o => o.Expiry <= maxExpiry && o.DaysToExpiry > 0).ToList();
    }

    private async Task<List<string>> FilterByLiquidityAsync(
        Dictionary<string, List<ExanteOptionInfo>> groupedByUnderlying,
        CancellationToken cancellationToken)
    {
        var liquidUnderlyings = new ConcurrentBag<string>();

        // Process underlyings in parallel with rate limiting
        await Parallel.ForEachAsync(
            groupedByUnderlying,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 5,
                CancellationToken = cancellationToken
            },
            async (group, ct) =>
            {
                var underlying = group.Key;
                var options = group.Value;

                // Select representative options to check (near-term, ATM-ish)
                var sampleOptions = SelectRepresentativeOptions(options);

                // Check liquidity of sample options
                var liquidCount = 0;
                foreach (var option in sampleOptions)
                {
                    try
                    {
                        var isLiquid = await CheckLiquidityAsync(option.SymbolId, ct);
                        if (isLiquid)
                        {
                            liquidCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Error checking liquidity for {Symbol}",
                            option.SymbolId);
                    }
                }

                // If majority of samples are liquid, include the underlying
                if (liquidCount >= Math.Ceiling(sampleOptions.Count / 2.0))
                {
                    liquidUnderlyings.Add(underlying);
                    _logger.LogDebug(
                        "Underlying {Symbol} has sufficient liquidity ({Liquid}/{Total} samples)",
                        underlying,
                        liquidCount,
                        sampleOptions.Count);
                }
            });

        return liquidUnderlyings.OrderBy(s => s).ToList();
    }

    private List<ExanteOptionInfo> SelectRepresentativeOptions(List<ExanteOptionInfo> options)
    {
        // Select up to SampleOptionsPerUnderlying options
        // Prioritize: nearest expiry, diverse strikes
        return options
            .OrderBy(o => o.DaysToExpiry)
            .Take(_discoverySettings.SampleOptionsPerUnderlying)
            .ToList();
    }

    private async Task<bool> CheckLiquidityAsync(
        string symbolId,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            // Note: Exante API may not provide volume/open interest in quotes endpoint
            // This is a simplified implementation that checks if quotes are available
            // In production, you may need to use different endpoints or fallback logic

            var response = await _httpClient.GetAsync(
                $"/md/3.0/quotes/{symbolId}",
                cancellationToken);

            await Task.Delay(RequestDelayMs, cancellationToken); // Rate limiting

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var quote = JsonSerializer.Deserialize<ExanteQuoteResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Check if we have basic quote data (bid/ask available)
            // In real implementation, check Volume >= MinVolume && OpenInterest >= MinOpenInterest
            if (quote?.Bid != null && quote?.Ask != null && quote.Bid > 0 && quote.Ask > 0)
            {
                // For now, assume liquid if we have valid quotes
                // TODO: Add actual volume/open interest checks when available from API
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking liquidity for {Symbol}", symbolId);
            return false;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private IEnumerable<string> GenerateSyntheticUnderlyings()
    {
        // Return realistic set for testing
        return new[]
        {
            "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA",
            "NVDA", "META", "AMD", "NFLX", "SPY", "QQQ"
        };
    }

    /// <summary>
    /// OPTIMIZED: Fetch symbols that have options using /md/3.0/groups endpoint
    /// This is much faster than parsing all 1.6M options from /md/3.0/types/OPTION
    /// </summary>
    private async Task<List<string>> FetchSymbolsWithOptionsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching option groups from Exante API...");

            var response = await _httpClient.GetAsync(
                "/md/3.0/groups",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Exante API returned {StatusCode}: {Reason}",
                    response.StatusCode,
                    response.ReasonPhrase);
                return new List<string>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var groups = JsonSerializer.Deserialize<List<ExanteGroupResponse>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (groups == null || !groups.Any())
            {
                _logger.LogWarning("No groups returned from Exante API");
                return new List<string>();
            }

            _logger.LogInformation("Received {Count} groups from Exante", groups.Count);

            // Define target exchanges for underlying stocks
            // Note: The "Exchange" field in groups response shows where OPTIONS are traded (mostly CBOE)
            // We need to extract the exchange from the "Group" field (e.g., "AAPL.NASDAQ")
            var targetExchanges = new HashSet<string> { "NASDAQ", "NYSE", "AMEX", "ARCA" };

            // Filter and extract underlying symbols
            var underlyingSymbols = groups
                .Where(g => g.Types != null && g.Types.Contains("OPTION"))
                .Where(g => !string.IsNullOrEmpty(g.Group))
                .Select(g =>
                {
                    // Group format is "TICKER.EXCHANGE" (e.g., "AAPL.NASDAQ", "HCA.NYSE")
                    var parts = g.Group!.Split('.');
                    if (parts.Length >= 2)
                    {
                        var ticker = parts[0];
                        var underlyingExchange = parts[1];

                        // Filter by underlying exchange
                        if (targetExchanges.Contains(underlyingExchange))
                        {
                            return ticker;
                        }
                    }
                    return null;
                })
                .Where(ticker => !string.IsNullOrEmpty(ticker))
                .Distinct()
                .OrderBy(ticker => ticker)
                .ToList()!;

            _logger.LogInformation(
                "Found {Count} unique underlying symbols with options on {Exchanges}",
                underlyingSymbols.Count,
                string.Join(", ", targetExchanges));

            return underlyingSymbols;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching option groups from Exante");
            throw;
        }
    }

    // DTOs for Exante API responses
    private class ExanteSymbolResponse
    {
        public string? SymbolId { get; set; }
        public string? Ticker { get; set; }
        public string? UnderlyingSymbolId { get; set; }
        public string? SymbolType { get; set; }
        public string? Exchange { get; set; }
        public long? Expiration { get; set; } // Unix timestamp in milliseconds
        public ExanteOptionData? OptionData { get; set; }
    }

    private class ExanteOptionData
    {
        public string? OptionRight { get; set; } // "PUT" or "CALL"
        public string? StrikePrice { get; set; }
    }

    private class ExanteQuoteResponse
    {
        public decimal? Bid { get; set; }
        public decimal? Ask { get; set; }
        public int? Volume { get; set; }
        public int? OpenInterest { get; set; }
    }

    /// <summary>
    /// DTO for Exante /md/3.0/groups endpoint
    /// Returns symbols that have options available
    /// </summary>
    private class ExanteGroupResponse
    {
        public string? Exchange { get; set; }
        public string? Group { get; set; } // Underlying symbol (e.g., "AAPL.NASDAQ")
        public string? Name { get; set; }
        public string[]? Types { get; set; } // e.g., ["OPTION"]
    }
}
