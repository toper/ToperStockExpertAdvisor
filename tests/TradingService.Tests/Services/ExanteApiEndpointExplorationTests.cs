using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TradingService.Configuration;

namespace TradingService.Tests.Services;

/// <summary>
/// Explore different Exante API endpoints to find stock options
/// Based on user guidance: use get instruments by exchange, avoid CBOE
/// </summary>
[Trait("Category", "Manual")]
public class ExanteApiEndpointExplorationTests
{
    [Fact]
    public async Task TryDifferentEndpoints_FindStockOptions()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .Build();

        var appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(appSettings);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(appSettings.Broker.Exante.BaseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", appSettings.Broker.Exante.JwtToken);
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        Console.WriteLine("=== EXPLORING EXANTE API ENDPOINTS ===");
        Console.WriteLine($"Base URL: {httpClient.BaseAddress}");
        Console.WriteLine();

        // Test 1: Try to get exchanges list
        await TryEndpoint(httpClient, "/md/3.0/exchanges", "Get available exchanges");

        // Test 2: Try to get symbols by exchange (NASDAQ)
        await TryEndpoint(httpClient, "/md/3.0/exchanges/NASDAQ", "Get NASDAQ instruments");

        // Test 3: Try to get symbols by exchange (NYSE)
        await TryEndpoint(httpClient, "/md/3.0/exchanges/NYSE", "Get NYSE instruments");

        // Test 4: Try to get types
        await TryEndpoint(httpClient, "/md/3.0/types", "Get instrument types");

        // Test 5: Try NASDAQ options specifically
        await TryEndpoint(httpClient, "/md/3.0/exchanges/NASDAQ/types/OPTION", "Get NASDAQ options");

        // Test 6: Try NYSE options specifically
        await TryEndpoint(httpClient, "/md/3.0/exchanges/NYSE/types/OPTION", "Get NYSE options");

        // Test 7: Try to get specific symbol info (AAPL)
        await TryEndpoint(httpClient, "/md/3.0/symbols/AAPL.NASDAQ", "Get AAPL symbol info");

        // Test 8: Try symbol search
        await TryEndpoint(httpClient, "/md/3.0/symbols?query=AAPL", "Search for AAPL");

        // Test 9: Try groups
        await TryEndpoint(httpClient, "/md/3.0/groups", "Get instrument groups");

        // Test 10: Try different version (2.0)
        await TryEndpoint(httpClient, "/md/2.0/types/OPTION", "Get options via v2.0 API");
    }

    private async Task TryEndpoint(HttpClient client, string endpoint, string description)
    {
        Console.WriteLine($"--- {description} ---");
        Console.WriteLine($"Endpoint: {endpoint}");

        try
        {
            var response = await client.GetAsync(endpoint);
            Console.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                // Try to parse as JSON array
                if (content.StartsWith("["))
                {
                    using var doc = JsonDocument.Parse(content);
                    var count = doc.RootElement.GetArrayLength();
                    Console.WriteLine($"✓ SUCCESS - Returned {count} items");

                    if (count > 0)
                    {
                        Console.WriteLine("First 3 items:");
                        var items = doc.RootElement.EnumerateArray().Take(3);
                        foreach (var item in items)
                        {
                            Console.WriteLine($"  {item.GetRawText()}");
                        }
                    }
                }
                // Try to parse as JSON object
                else if (content.StartsWith("{"))
                {
                    Console.WriteLine($"✓ SUCCESS - Returned object:");
                    using var doc = JsonDocument.Parse(content);
                    Console.WriteLine($"  {doc.RootElement.GetRawText()}");
                }
                else
                {
                    Console.WriteLine($"✓ SUCCESS - Content length: {content.Length} bytes");
                    Console.WriteLine($"  First 200 chars: {content.Substring(0, Math.Min(200, content.Length))}");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✗ FAILED - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ EXCEPTION - {ex.Message}");
        }

        Console.WriteLine();
    }

    [Fact]
    public async Task AnalyzeWorkingEndpoint_CheckForStockOptions()
    {
        // This test uses the working /md/3.0/types/OPTION endpoint
        // but analyzes more symbols to find where stock options appear

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .Build();

        var appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(appSettings);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(appSettings.Broker.Exante.BaseUrl),
            Timeout = TimeSpan.FromMinutes(10)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", appSettings.Broker.Exante.JwtToken);

        Console.WriteLine("=== ANALYZING /md/3.0/types/OPTION FOR STOCK OPTIONS ===");

        var response = await httpClient.GetAsync(
            "/md/3.0/types/OPTION",
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var symbols = JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
            stream,
            jsonOptions);

        var count = 0;
        var stockOptions = new List<string>();
        var futuresOptions = new List<string>();
        var exchangeDistribution = new Dictionary<string, int>();
        var tickerDistribution = new Dictionary<string, int>();

        await foreach (var symbol in symbols)
        {
            count++;

            // Get exchange
            if (symbol.TryGetProperty("exchange", out var exchange))
            {
                var exchangeStr = exchange.GetString() ?? "UNKNOWN";
                exchangeDistribution[exchangeStr] = exchangeDistribution.GetValueOrDefault(exchangeStr) + 1;
            }

            // Get ticker
            if (symbol.TryGetProperty("ticker", out var ticker))
            {
                var tickerStr = ticker.GetString() ?? "";

                // Track ticker frequency
                tickerDistribution[tickerStr] = tickerDistribution.GetValueOrDefault(tickerStr) + 1;

                // Look for stock options (NASDAQ/NYSE exchanges)
                if (symbol.TryGetProperty("exchange", out var exch))
                {
                    var exchStr = exch.GetString() ?? "";
                    if (exchStr == "NASDAQ" || exchStr == "NYSE" || exchStr == "AMEX")
                    {
                        if (!stockOptions.Contains(tickerStr))
                            stockOptions.Add(tickerStr);
                    }
                    else
                    {
                        if (!futuresOptions.Contains(tickerStr))
                            futuresOptions.Add(tickerStr);
                    }
                }
            }

            // Progress logging
            if (count % 100000 == 0)
            {
                Console.WriteLine($"Processed {count} options...");
                Console.WriteLine($"  Stock options found so far: {stockOptions.Count}");
                Console.WriteLine($"  Futures/commodities found: {futuresOptions.Count}");
            }

            // Sample first occurrence of common stocks
            if (symbol.TryGetProperty("ticker", out var t))
            {
                var tStr = t.GetString();
                if (tStr == "AAPL" || tStr == "MSFT" || tStr == "SPY" || tStr == "QQQ" || tStr == "TSLA")
                {
                    Console.WriteLine($"\n✓ FOUND {tStr} OPTION AT POSITION {count}:");
                    Console.WriteLine(symbol.GetRawText());
                    Console.WriteLine();
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== ANALYSIS RESULTS ===");
        Console.WriteLine($"Total options processed: {count}");
        Console.WriteLine($"Stock options (NASDAQ/NYSE/AMEX): {stockOptions.Count}");
        Console.WriteLine($"Futures/commodities: {futuresOptions.Count}");
        Console.WriteLine();

        Console.WriteLine("Top 20 exchanges:");
        foreach (var kvp in exchangeDistribution.OrderByDescending(x => x.Value).Take(20))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value} options");
        }
        Console.WriteLine();

        Console.WriteLine("Top 50 tickers:");
        foreach (var kvp in tickerDistribution.OrderByDescending(x => x.Value).Take(50))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value} options");
        }
        Console.WriteLine();

        if (stockOptions.Any())
        {
            Console.WriteLine($"Stock option tickers (first 100): {string.Join(", ", stockOptions.Take(100))}");
        }
    }
}
