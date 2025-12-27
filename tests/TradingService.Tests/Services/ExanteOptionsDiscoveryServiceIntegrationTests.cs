using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Services.Integrations;

namespace TradingService.Tests.Services;

/// <summary>
/// Integration tests for ExanteOptionsDiscoveryService using real Exante Demo API
/// These tests require network connectivity and valid Exante credentials
/// </summary>
[Trait("Category", "Integration")]
public class ExanteOptionsDiscoveryServiceIntegrationTests : IAsyncLifetime
{
    private readonly Mock<ILogger<ExanteOptionsDiscoveryService>> _mockLogger;
    private readonly AppSettings _appSettings;
    private ExanteOptionsDiscoveryService? _service;

    public ExanteOptionsDiscoveryServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<ExanteOptionsDiscoveryService>>();

        // Load configuration from appsettings.IntegrationTests.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .Build();

        _appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(_appSettings);
    }

    public Task InitializeAsync()
    {
        // Create service with real Exante credentials
        var options = Options.Create(_appSettings);
        _service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_WithRealAPI_ReturnsSymbols()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        // Act
        var result = await _service!.DiscoverUnderlyingSymbolsAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty("Exante should have available options");

        // Log results for inspection
        var symbolsList = result.ToList();
        Console.WriteLine($"Discovered {symbolsList.Count} underlying symbols:");
        foreach (var symbol in symbolsList.Take(20))
        {
            Console.WriteLine($"  - {symbol}");
        }

        // Basic validation
        symbolsList.Should().AllSatisfy(s =>
        {
            s.Should().NotBeNullOrWhiteSpace();
            // Allow alphanumeric and forward slash (for tickers like BRK/B, ACB1, etc.)
            s.Should().MatchRegex("^[A-Z0-9/]+$", "symbols should be alphanumeric uppercase with optional /");
        });
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_WithRealAPI_FiltersPopularStocks()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        // Act
        var result = await _service!.DiscoverUnderlyingSymbolsAsync(cts.Token);
        var symbolsList = result.ToList();

        // Assert - Should find at least some popular stocks
        symbolsList.Should().Contain(popularStocks =>
            popularStocks.Contains("AAPL") ||
            popularStocks.Contains("MSFT") ||
            popularStocks.Contains("GOOGL") ||
            popularStocks.Contains("SPY") ||
            popularStocks.Contains("QQQ"),
            "should discover at least some popular stocks with liquid options");
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_WithRealAPI_LogsProgress()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        // Act
        await _service!.DiscoverUnderlyingSymbolsAsync(cts.Token);

        // Assert - Should log key steps
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting options discovery")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // UPDATED: The optimized approach uses /md/3.0/groups endpoint
        // and logs "Fetching option groups" instead of "Fetched X options"
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Received") && v.ToString()!.Contains("groups")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_WithCancellation_ThrowsOperationCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _service!.DiscoverUnderlyingSymbolsAsync(cts.Token);
        });
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_MultipleCallsInSequence_ReturnsConsistentResults()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        // Act - Call twice
        var result1 = await _service!.DiscoverUnderlyingSymbolsAsync(cts.Token);
        var result2 = await _service!.DiscoverUnderlyingSymbolsAsync(cts.Token);

        // Assert - Results should be similar (allowing for market changes)
        var list1 = result1.ToList();
        var list2 = result2.ToList();

        list1.Should().NotBeEmpty();
        list2.Should().NotBeEmpty();

        // At least 80% overlap expected (some options might expire/be added between calls)
        var overlap = list1.Intersect(list2).Count();
        var overlapPercentage = (double)overlap / Math.Max(list1.Count, list2.Count);

        overlapPercentage.Should().BeGreaterThan(0.8, "results should be mostly consistent between calls");

        Console.WriteLine($"First call: {list1.Count} symbols");
        Console.WriteLine($"Second call: {list2.Count} symbols");
        Console.WriteLine($"Overlap: {overlap} symbols ({overlapPercentage:P})");
    }
}

/// <summary>
/// Manual/exploratory integration tests for debugging and API exploration
/// These tests are designed to be run manually and inspect actual API responses
/// </summary>
[Trait("Category", "Manual")]
public class ExanteOptionsDiscoveryServiceManualTests
{
    private readonly Mock<ILogger<ExanteOptionsDiscoveryService>> _mockLogger;
    private readonly AppSettings _appSettings;

    public ExanteOptionsDiscoveryServiceManualTests()
    {
        _mockLogger = new Mock<ILogger<ExanteOptionsDiscoveryService>>();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .Build();

        _appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(_appSettings);
    }

    [Fact(Skip = "Manual test - run explicitly to explore API")]
    public async Task ExploreExanteAPI_FetchAndPrintAllOptions()
    {
        // This test fetches ALL options from Exante and prints detailed information
        // Useful for understanding the API response structure

        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var result = await service.DiscoverUnderlyingSymbolsAsync(cts.Token);

        var symbolsList = result.ToList();

        Console.WriteLine("=== EXANTE OPTIONS DISCOVERY RESULTS ===");
        Console.WriteLine($"Total underlying symbols discovered: {symbolsList.Count}");
        Console.WriteLine();

        Console.WriteLine("Top 50 symbols:");
        foreach (var symbol in symbolsList.Take(50))
        {
            Console.WriteLine($"  {symbol}");
        }

        // Group by first letter for distribution analysis
        var distribution = symbolsList.GroupBy(s => s[0])
            .OrderBy(g => g.Key)
            .Select(g => new { Letter = g.Key, Count = g.Count() });

        Console.WriteLine();
        Console.WriteLine("Symbol distribution by first letter:");
        foreach (var dist in distribution)
        {
            Console.WriteLine($"  {dist.Letter}: {dist.Count} symbols");
        }
    }

    [Fact(Skip = "Manual test - run explicitly to test filtering")]
    public async Task TestDifferentLiquidityFilters()
    {
        // Test with different liquidity settings to see the impact

        var testConfigs = new[]
        {
            new { MinOI = 0, MinVol = 0, Desc = "No filtering" },
            new { MinOI = 50, MinVol = 20, Desc = "Relaxed filtering" },
            new { MinOI = 100, MinVol = 50, Desc = "Standard filtering" },
            new { MinOI = 500, MinVol = 200, Desc = "Strict filtering" }
        };

        foreach (var config in testConfigs)
        {
            _appSettings.OptionsDiscovery.MinOpenInterest = config.MinOI;
            _appSettings.OptionsDiscovery.MinVolume = config.MinVol;

            var options = Options.Create(_appSettings);
            var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var result = await service.DiscoverUnderlyingSymbolsAsync(cts.Token);

            Console.WriteLine($"{config.Desc} (MinOI={config.MinOI}, MinVol={config.MinVol}):");
            Console.WriteLine($"  Discovered {result.Count()} symbols");
            Console.WriteLine($"  Top 10: {string.Join(", ", result.Take(10))}");
            Console.WriteLine();
        }
    }
}
