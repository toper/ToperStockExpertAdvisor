using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TradingService.Configuration;

namespace TradingService.Tests.Services;

/// <summary>
/// Direct API tests to diagnose what's actually happening
/// </summary>
[Trait("Category", "RealAPI")]
public class ExanteApiRealTests
{
    [Fact]
    public async Task FetchFirst100Options_ShowStructure()
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
            Timeout = TimeSpan.FromMinutes(10)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", appSettings.Broker.Exante.JwtToken);
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        // Act
        Console.WriteLine("=== FETCHING OPTIONS FROM EXANTE ===");
        Console.WriteLine($"URL: {httpClient.BaseAddress}/md/3.0/types/OPTION");
        Console.WriteLine();

        var response = await httpClient.GetAsync(
            "/md/3.0/types/OPTION",
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var options = JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
            stream,
            jsonOptions);

        var count = 0;
        var appleOptions = 0;
        var microsoftOptions = 0;
        var spyOptions = 0;

        Console.WriteLine("First 10 options:");
        Console.WriteLine();

        await foreach (var option in options)
        {
            count++;

            if (count <= 10)
            {
                Console.WriteLine($"Option #{count}:");
                Console.WriteLine(option.GetRawText());
                Console.WriteLine();
            }

            // Count specific tickers
            if (option.TryGetProperty("ticker", out var ticker))
            {
                var tickerStr = ticker.GetString();
                if (tickerStr == "AAPL") appleOptions++;
                if (tickerStr == "MSFT") microsoftOptions++;
                if (tickerStr == "SPY") spyOptions++;
            }

            // Stop after 1000 for quick test
            if (count >= 1000)
            {
                Console.WriteLine($"... stopping at {count} options for quick test");
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"=== RESULTS (first 1000 options) ===");
        Console.WriteLine($"Total processed: {count}");
        Console.WriteLine($"AAPL options: {appleOptions}");
        Console.WriteLine($"MSFT options: {microsoftOptions}");
        Console.WriteLine($"SPY options: {spyOptions}");
        Console.WriteLine();

        // Assert
        count.Should().BeGreaterThan(0, "should fetch at least some options");
    }

    [Fact]
    public async Task CountAllOptions_ShowStats()
    {
        // This test counts ALL options and shows statistics
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

        Console.WriteLine("=== COUNTING ALL OPTIONS (this will take ~5 minutes) ===");

        var response = await httpClient.GetAsync(
            "/md/3.0/types/OPTION",
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var options = JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
            stream,
            jsonOptions);

        var total = 0;
        var withTicker = 0;
        var withUnderlyingSymbolId = 0;
        var withOptionData = 0;
        var putOptions = 0;
        var callOptions = 0;
        var tickers = new HashSet<string>();
        var underlyingSymbols = new HashSet<string>();

        await foreach (var option in options)
        {
            total++;

            if (option.TryGetProperty("ticker", out var ticker) && !ticker.ValueKind.Equals(JsonValueKind.Null))
            {
                withTicker++;
                tickers.Add(ticker.GetString() ?? "");
            }

            if (option.TryGetProperty("underlyingSymbolId", out var underlying) && !underlying.ValueKind.Equals(JsonValueKind.Null))
            {
                withUnderlyingSymbolId++;
                var underlyingStr = underlying.GetString() ?? "";
                var parts = underlyingStr.Split('.');
                if (parts.Length > 0)
                {
                    underlyingSymbols.Add(parts[0]);
                }
            }

            if (option.TryGetProperty("optionData", out var optionData))
            {
                withOptionData++;
                if (optionData.TryGetProperty("optionRight", out var right))
                {
                    var rightStr = right.GetString();
                    if (rightStr == "PUT") putOptions++;
                    if (rightStr == "CALL") callOptions++;
                }
            }

            if (total % 100000 == 0)
            {
                Console.WriteLine($"Processed {total} options...");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"=== STATISTICS ===");
        Console.WriteLine($"Total options: {total}");
        Console.WriteLine($"With ticker field: {withTicker}");
        Console.WriteLine($"With underlyingSymbolId field: {withUnderlyingSymbolId}");
        Console.WriteLine($"With optionData field: {withOptionData}");
        Console.WriteLine($"PUT options: {putOptions}");
        Console.WriteLine($"CALL options: {callOptions}");
        Console.WriteLine($"Unique tickers: {tickers.Count}");
        Console.WriteLine($"Unique underlying symbols: {underlyingSymbols.Count}");
        Console.WriteLine();
        Console.WriteLine($"Sample tickers (first 50): {string.Join(", ", tickers.Take(50))}");
        Console.WriteLine();
        Console.WriteLine($"Sample underlyings (first 50): {string.Join(", ", underlyingSymbols.Take(50))}");
    }
}
