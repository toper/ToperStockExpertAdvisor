using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Services.Integrations;

namespace TradingService.Tests.Services;

/// <summary>
/// Tests for the OPTIMIZED ExanteOptionsDiscoveryService using /md/3.0/groups endpoint
/// This should be much faster than the old approach (seconds vs minutes)
/// </summary>
[Trait("Category", "Integration")]
public class ExanteOptionsDiscoveryServiceOptimizedTests
{
    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_UsingGroupsEndpoint_ReturnsFast()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .Build();

        var appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(appSettings);

        var mockLogger = new Mock<ILogger<ExanteOptionsDiscoveryService>>();
        var options = Options.Create(appSettings);
        var service = new ExanteOptionsDiscoveryService(options, mockLogger.Object);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Should complete in <30 seconds

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.DiscoverUnderlyingSymbolsAsync(cts.Token);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty("Exante should have stock options available");

        var symbolsList = result.ToList();
        Console.WriteLine($"\n=== OPTIMIZED DISCOVERY RESULTS ===");
        Console.WriteLine($"Execution time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Total underlying symbols discovered: {symbolsList.Count}");
        Console.WriteLine();

        // Validate we have common stocks
        var commonStocks = new[] { "AAPL", "MSFT", "GOOGL", "SPY", "QQQ", "TSLA", "AMZN", "NVDA", "META" };
        var foundCommonStocks = symbolsList.Intersect(commonStocks).ToList();

        Console.WriteLine($"Common stocks found ({foundCommonStocks.Count}/{commonStocks.Length}):");
        foreach (var stock in foundCommonStocks)
        {
            Console.WriteLine($"  ✓ {stock}");
        }
        Console.WriteLine();

        // Show first 50 symbols
        Console.WriteLine("First 50 symbols:");
        foreach (var symbol in symbolsList.Take(50))
        {
            Console.WriteLine($"  - {symbol}");
        }
        Console.WriteLine();

        // Group by first character
        var distribution = symbolsList
            .GroupBy(s => s[0])
            .OrderBy(g => g.Key)
            .Select(g => new { Letter = g.Key, Count = g.Count() })
            .ToList();

        Console.WriteLine("Distribution by first letter:");
        foreach (var dist in distribution)
        {
            Console.WriteLine($"  {dist.Letter}: {dist.Count} symbols");
        }

        // Assertions
        symbolsList.Should().AllSatisfy(s =>
        {
            s.Should().NotBeNullOrWhiteSpace();
            // Allow alphanumeric and forward slash (for tickers like BRK/B, PBR/A)
            s.Should().MatchRegex("^[A-Z0-9/]+$", "symbols should be alphanumeric uppercase with optional /");
        });

        foundCommonStocks.Should().NotBeEmpty("should contain at least some common stocks");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "optimized approach should complete in <30 seconds");
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_SimulationMode_ReturnsQuickly()
    {
        // Arrange
        var appSettings = new AppSettings
        {
            Broker = new BrokerSettings
            {
                Exante = new ExanteBrokerSettings
                {
                    BaseUrl = "https://api-demo.exante.eu",
                    ApiKey = "", // Empty = simulation mode
                    JwtToken = ""
                }
            },
            OptionsDiscovery = new OptionsDiscoverySettings
            {
                Enabled = true
            }
        };

        var mockLogger = new Mock<ILogger<ExanteOptionsDiscoveryService>>();
        var options = Options.Create(appSettings);
        var service = new ExanteOptionsDiscoveryService(options, mockLogger.Object);

        // Act
        var result = await service.DiscoverUnderlyingSymbolsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        var symbolsList = result.ToList();
        Console.WriteLine($"\n=== SIMULATION MODE RESULTS ===");
        Console.WriteLine($"Total synthetic symbols: {symbolsList.Count}");
        Console.WriteLine($"Symbols: {string.Join(", ", symbolsList)}");

        // Should contain common test symbols
        symbolsList.Should().Contain("AAPL");
        symbolsList.Should().Contain("MSFT");
        symbolsList.Should().Contain("SPY");
    }
}
