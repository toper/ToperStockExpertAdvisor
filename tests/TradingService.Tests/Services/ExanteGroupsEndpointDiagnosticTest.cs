using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TradingService.Configuration;
using TradingService.Services.Integrations;

namespace TradingService.Tests.Services;

/// <summary>
/// Diagnostic test to analyze /md/3.0/groups endpoint response
/// </summary>
[Trait("Category", "Manual")]
public class ExanteGroupsEndpointDiagnosticTest
{
    [Fact]
    public async Task AnalyzeGroupsEndpoint_ShowDetails()
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
            Timeout = TimeSpan.FromMinutes(2)
        };

        // Generate JWT token using ExanteAuthService
        var authService = new ExanteAuthService(
            appSettings.Broker.Exante,
            NullLogger<ExanteAuthService>.Instance,
            new MockHttpClientFactory());

        await authService.ConfigureClientAuthenticationAsync(httpClient);

        Console.WriteLine("=== ANALYZING /md/3.0/groups ENDPOINT ===");
        Console.WriteLine($"Base URL: {httpClient.BaseAddress}");
        Console.WriteLine();

        // Act
        var response = await httpClient.GetAsync("/md/3.0/groups");

        Console.WriteLine($"Status: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error: {error}");
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response size: {content.Length} bytes");

        var groups = JsonSerializer.Deserialize<List<GroupResponse>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Console.WriteLine($"Total groups: {groups?.Count ?? 0}");
        Console.WriteLine();

        if (groups == null || !groups.Any())
        {
            Console.WriteLine("ERROR: No groups returned!");
            return;
        }

        // Show first 10 groups
        Console.WriteLine("First 10 groups:");
        foreach (var group in groups.Take(10))
        {
            Console.WriteLine($"  Exchange: {group.Exchange}, Group: {group.Group}, " +
                            $"Name: {group.Name}, Types: [{string.Join(", ", group.Types ?? Array.Empty<string>())}]");
        }
        Console.WriteLine();

        // Count by exchange
        var byExchange = groups
            .GroupBy(g => g.Exchange ?? "UNKNOWN")
            .OrderByDescending(g => g.Count())
            .Select(g => new { Exchange = g.Key, Count = g.Count() })
            .ToList();

        Console.WriteLine("Groups by exchange:");
        foreach (var exch in byExchange.Take(20))
        {
            Console.WriteLine($"  {exch.Exchange}: {exch.Count} groups");
        }
        Console.WriteLine();

        // Filter for stock exchanges
        var stockExchanges = new[] { "NASDAQ", "NYSE", "AMEX", "ARCA" };
        var stockGroups = groups
            .Where(g => g.Exchange != null && stockExchanges.Contains(g.Exchange))
            .Where(g => g.Types != null && g.Types.Contains("OPTION"))
            .ToList();

        Console.WriteLine($"Stock option groups (NASDAQ/NYSE/AMEX/ARCA): {stockGroups.Count}");
        Console.WriteLine();

        if (stockGroups.Any())
        {
            Console.WriteLine("First 20 stock option groups:");
            foreach (var group in stockGroups.Take(20))
            {
                Console.WriteLine($"  {group.Group} ({group.Exchange}) - {group.Name}");
            }
            Console.WriteLine();

            // Extract tickers
            var tickers = stockGroups
                .Select(g => g.Group?.Split('.').FirstOrDefault())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            Console.WriteLine($"Unique stock tickers: {tickers.Count}");
            Console.WriteLine($"Sample tickers: {string.Join(", ", tickers.Take(50))}");
            Console.WriteLine();

            // Check for common stocks
            var commonStocks = new[] { "AAPL", "MSFT", "GOOGL", "SPY", "QQQ", "TSLA" };
            var foundCommon = tickers.Intersect(commonStocks).ToList();
            Console.WriteLine($"Common stocks found ({foundCommon.Count}/{commonStocks.Length}):");
            foreach (var stock in foundCommon)
            {
                Console.WriteLine($"  ✓ {stock}");
            }
        }
        else
        {
            Console.WriteLine("WARNING: No stock option groups found!");
            Console.WriteLine("Checking all exchanges...");

            var optionGroups = groups
                .Where(g => g.Types != null && g.Types.Contains("OPTION"))
                .ToList();

            Console.WriteLine($"Total option groups (all exchanges): {optionGroups.Count}");

            if (optionGroups.Any())
            {
                var optionExchanges = optionGroups
                    .GroupBy(g => g.Exchange ?? "UNKNOWN")
                    .OrderByDescending(g => g.Count())
                    .Select(g => new { Exchange = g.Key, Count = g.Count() })
                    .ToList();

                Console.WriteLine("Option groups by exchange:");
                foreach (var exch in optionExchanges.Take(20))
                {
                    Console.WriteLine($"  {exch.Exchange}: {exch.Count} option groups");
                }
            }
        }
    }

    private class GroupResponse
    {
        public string? Exchange { get; set; }
        public string? Group { get; set; }
        public string? Name { get; set; }
        public string[]? Types { get; set; }
    }

    // Helper class for creating HttpClient in tests
    private class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
