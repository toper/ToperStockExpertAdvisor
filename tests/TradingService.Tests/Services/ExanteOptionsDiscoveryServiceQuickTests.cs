using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Services.Integrations;

namespace TradingService.Tests.Services;

/// <summary>
/// Quick integration tests for ExanteOptionsDiscoveryService
/// These tests use real API but skip expensive liquidity checks
/// </summary>
[Trait("Category", "QuickIntegration")]
public class ExanteOptionsDiscoveryServiceQuickTests
{
    [Fact]
    public async Task RealAPI_FetchAndParseOptions_ReturnsUnderlyingSymbols()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .Build();

        var appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(appSettings);

        // DISABLE liquidity filtering for faster test
        appSettings.OptionsDiscovery.MinOpenInterest = 0;
        appSettings.OptionsDiscovery.MinVolume = 0;

        var mockLogger = new Mock<ILogger<ExanteOptionsDiscoveryService>>();
        var options = Options.Create(appSettings);
        var service = new ExanteOptionsDiscoveryService(options, mockLogger.Object);

        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        // Act
        var result = await service.DiscoverUnderlyingSymbolsAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty("Exante should have available options");

        var symbolsList = result.ToList();
        Console.WriteLine($"\n=== EXANTE OPTIONS DISCOVERY RESULTS ===");
        Console.WriteLine($"Total underlying symbols discovered: {symbolsList.Count}");
        Console.WriteLine($"\nFirst 50 symbols:");
        foreach (var symbol in symbolsList.Take(50))
        {
            Console.WriteLine($"  - {symbol}");
        }

        // Validation
        symbolsList.Should().AllSatisfy(s =>
        {
            s.Should().NotBeNullOrWhiteSpace();
            // Allow alphanumeric and forward slash (for tickers like BRK/B, PBR/A)
            s.Should().MatchRegex("^[A-Z0-9/]+$", "symbols should be alphanumeric uppercase with optional /");
        });

        // Check for common stocks
        var hasCommonStocks = symbolsList.Any(s =>
            s == "AAPL" || s == "MSFT" || s == "GOOGL" ||
            s == "SPY" || s == "QQQ" || s == "TSLA");

        Console.WriteLine($"\nContains common stocks (AAPL, MSFT, etc): {hasCommonStocks}");

        // Group by first character to see distribution
        var distribution = symbolsList
            .GroupBy(s => s[0])
            .OrderBy(g => g.Key)
            .Select(g => new { Letter = g.Key, Count = g.Count() })
            .ToList();

        Console.WriteLine($"\nSymbol distribution by first character:");
        foreach (var dist in distribution)
        {
            Console.WriteLine($"  {dist.Letter}: {dist.Count} symbols");
        }
    }
}
