using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Models;
using TradingService.Services.Integrations;

namespace TradingService.Tests.Services;

/// <summary>
/// Unit tests for ExanteOptionsDiscoveryService
/// Tests symbol parsing, filtering logic, and simulation mode
/// </summary>
public class ExanteOptionsDiscoveryServiceTests
{
    private readonly Mock<ILogger<ExanteOptionsDiscoveryService>> _mockLogger;
    private readonly AppSettings _appSettings;

    public ExanteOptionsDiscoveryServiceTests()
    {
        _mockLogger = new Mock<ILogger<ExanteOptionsDiscoveryService>>();
        _appSettings = new AppSettings
        {
            Broker = new BrokerSettings
            {
                Exante = new ExanteBrokerSettings
                {
                    ApiKey = string.Empty, // Simulation mode
                    ApiSecret = string.Empty,
                    BaseUrl = "https://api-demo.exante.eu"
                }
            },
            OptionsDiscovery = new OptionsDiscoverySettings
            {
                Enabled = true,
                MinOpenInterest = 100,
                MinVolume = 50,
                SampleOptionsPerUnderlying = 3,
                FallbackToWatchlist = true,
                IncludeCallOptions = true,
                MaxExpiryDays = 90
            }
        };
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_SimulationMode_ReturnsSyntheticSymbols()
    {
        // Arrange
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        // Act
        var result = await service.DiscoverUnderlyingSymbolsAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("AAPL");
        result.Should().Contain("MSFT");
        result.Should().Contain("SPY");
        result.Count().Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task DiscoverUnderlyingSymbolsAsync_SimulationMode_LogsWarning()
    {
        // Arrange
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        // Act
        await service.DiscoverUnderlyingSymbolsAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Simulation mode")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("AAPL.NASDAQ_250117_P_180", "AAPL", "NASDAQ", OptionType.Put, 180)]
    [InlineData("MSFT.NYSE_250221_C_400", "MSFT", "NYSE", OptionType.Call, 400)]
    [InlineData("SPY.CBOE_250314_P_500", "SPY", "CBOE", OptionType.Put, 500)]
    [InlineData("TSLA.NASDAQ_260115_C_250", "TSLA", "NASDAQ", OptionType.Call, 250)]
    public void ParseExanteSymbol_ValidFormats_ParsesCorrectly(
        string symbolId,
        string expectedUnderlying,
        string expectedExchange,
        OptionType expectedType,
        decimal expectedStrike)
    {
        // This test uses reflection to access the private ParseExanteSymbol method
        // In production, you might want to make this method internal and use InternalsVisibleTo

        // Arrange
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        var method = typeof(ExanteOptionsDiscoveryService).GetMethod(
            "ParseExanteSymbol",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = method?.Invoke(service, new object[] { symbolId }) as ExanteOptionInfo;

        // Assert
        result.Should().NotBeNull();
        result!.UnderlyingSymbol.Should().Be(expectedUnderlying);
        result.Exchange.Should().Be(expectedExchange);
        result.OptionType.Should().Be(expectedType);
        result.Strike.Should().Be(expectedStrike);
        result.SymbolId.Should().Be(symbolId);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("AAPL")]
    [InlineData("AAPL.NASDAQ")]
    [InlineData("AAPL_250117_P_180")] // Missing exchange separator
    [InlineData("AAPL.NASDAQ_999999_P_180")] // Invalid date
    [InlineData("AAPL.NASDAQ_250117_X_180")] // Invalid option type
    public void ParseExanteSymbol_InvalidFormats_ReturnsNull(string symbolId)
    {
        // Arrange
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        var method = typeof(ExanteOptionsDiscoveryService).GetMethod(
            "ParseExanteSymbol",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = method?.Invoke(service, new object[] { symbolId }) as ExanteOptionInfo;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FilterByOptionType_IncludeCallOptions_ReturnsBothPutAndCall()
    {
        // Arrange
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        var allOptions = new List<ExanteOptionInfo>
        {
            new() { SymbolId = "AAPL.NASDAQ_250117_P_180", UnderlyingSymbol = "AAPL", OptionType = OptionType.Put },
            new() { SymbolId = "AAPL.NASDAQ_250117_C_200", UnderlyingSymbol = "AAPL", OptionType = OptionType.Call },
            new() { SymbolId = "MSFT.NYSE_250221_P_400", UnderlyingSymbol = "MSFT", OptionType = OptionType.Put }
        };

        var method = typeof(ExanteOptionsDiscoveryService).GetMethod(
            "FilterByOptionType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = method?.Invoke(service, new object[] { allOptions }) as List<ExanteOptionInfo>;

        // Assert
        result.Should().HaveCount(3); // All options included
        result.Should().Contain(o => o.OptionType == OptionType.Put);
        result.Should().Contain(o => o.OptionType == OptionType.Call);
    }

    [Fact]
    public void FilterByOptionType_ExcludeCallOptions_ReturnsOnlyPut()
    {
        // Arrange
        _appSettings.OptionsDiscovery.IncludeCallOptions = false;
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        var allOptions = new List<ExanteOptionInfo>
        {
            new() { SymbolId = "AAPL.NASDAQ_250117_P_180", UnderlyingSymbol = "AAPL", OptionType = OptionType.Put },
            new() { SymbolId = "AAPL.NASDAQ_250117_C_200", UnderlyingSymbol = "AAPL", OptionType = OptionType.Call },
            new() { SymbolId = "MSFT.NYSE_250221_P_400", UnderlyingSymbol = "MSFT", OptionType = OptionType.Put }
        };

        var method = typeof(ExanteOptionsDiscoveryService).GetMethod(
            "FilterByOptionType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = method?.Invoke(service, new object[] { allOptions }) as List<ExanteOptionInfo>;

        // Assert
        result.Should().HaveCount(2); // Only PUT options
        result.Should().AllSatisfy(o => o.OptionType.Should().Be(OptionType.Put));
    }

    [Fact]
    public void FilterByExpiry_WithinMaxExpiryDays_ReturnsFiltered()
    {
        // Arrange
        _appSettings.OptionsDiscovery.MaxExpiryDays = 30;
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        var today = DateTime.Today;
        var allOptions = new List<ExanteOptionInfo>
        {
            new() { SymbolId = "AAPL.NASDAQ_P_180", Expiry = today.AddDays(15), UnderlyingSymbol = "AAPL" }, // Within
            new() { SymbolId = "MSFT.NYSE_P_400", Expiry = today.AddDays(45), UnderlyingSymbol = "MSFT" }, // Outside
            new() { SymbolId = "TSLA.NASDAQ_P_250", Expiry = today.AddDays(29), UnderlyingSymbol = "TSLA" }, // Within
            new() { SymbolId = "SPY.CBOE_P_500", Expiry = today.AddDays(-5), UnderlyingSymbol = "SPY" } // Expired
        };

        var method = typeof(ExanteOptionsDiscoveryService).GetMethod(
            "FilterByExpiry",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = method?.Invoke(service, new object[] { allOptions }) as List<ExanteOptionInfo>;

        // Assert
        result.Should().HaveCount(2); // Only within 30 days and not expired
        result.Should().Contain(o => o.UnderlyingSymbol == "AAPL");
        result.Should().Contain(o => o.UnderlyingSymbol == "TSLA");
        result.Should().NotContain(o => o.UnderlyingSymbol == "MSFT");
        result.Should().NotContain(o => o.UnderlyingSymbol == "SPY");
    }

    [Fact]
    public void SelectRepresentativeOptions_TakesNearestExpiry()
    {
        // Arrange
        _appSettings.OptionsDiscovery.SampleOptionsPerUnderlying = 3;
        var options = Options.Create(_appSettings);
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);

        var today = DateTime.Today;
        var allOptions = new List<ExanteOptionInfo>
        {
            new() { SymbolId = "AAPL_60", Expiry = today.AddDays(60), Strike = 180 },
            new() { SymbolId = "AAPL_7", Expiry = today.AddDays(7), Strike = 170 },
            new() { SymbolId = "AAPL_30", Expiry = today.AddDays(30), Strike = 175 },
            new() { SymbolId = "AAPL_14", Expiry = today.AddDays(14), Strike = 185 },
            new() { SymbolId = "AAPL_90", Expiry = today.AddDays(90), Strike = 190 }
        };

        var method = typeof(ExanteOptionsDiscoveryService).GetMethod(
            "SelectRepresentativeOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = method?.Invoke(service, new object[] { allOptions }) as List<ExanteOptionInfo>;

        // Assert
        result.Should().HaveCount(3);
        result![0].SymbolId.Should().Be("AAPL_7"); // Nearest
        result[1].SymbolId.Should().Be("AAPL_14"); // Second nearest
        result[2].SymbolId.Should().Be("AAPL_30"); // Third nearest
    }

    [Fact]
    public void Constructor_EmptyApiKey_EnablesSimulationMode()
    {
        // Arrange
        _appSettings.Broker.Exante.ApiKey = string.Empty;
        var options = Options.Create(_appSettings);

        // Act
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);
        var field = typeof(ExanteOptionsDiscoveryService).GetField(
            "_isSimulationMode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var isSimulationMode = (bool)field!.GetValue(service)!;

        // Assert
        isSimulationMode.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithApiKey_DisablesSimulationMode()
    {
        // Arrange
        _appSettings.Broker.Exante.ApiKey = "test-api-key";
        _appSettings.Broker.Exante.ApiSecret = "test-api-secret";
        var options = Options.Create(_appSettings);

        // Act
        var service = new ExanteOptionsDiscoveryService(options, _mockLogger.Object);
        var field = typeof(ExanteOptionsDiscoveryService).GetField(
            "_isSimulationMode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var isSimulationMode = (bool)field!.GetValue(service)!;

        // Assert
        isSimulationMode.Should().BeFalse();
    }
}
