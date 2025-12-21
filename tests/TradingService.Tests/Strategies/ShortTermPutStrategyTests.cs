using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Models;
using TradingService.Services.Strategies;

namespace TradingService.Tests.Strategies;

public class ShortTermPutStrategyTests
{
    private readonly ShortTermPutStrategy _strategy;
    private readonly Mock<ILogger<ShortTermPutStrategy>> _loggerMock;

    public ShortTermPutStrategyTests()
    {
        _loggerMock = new Mock<ILogger<ShortTermPutStrategy>>();
        var appSettings = Options.Create(new AppSettings
        {
            Strategy = new StrategySettings
            {
                MinExpiryDays = 14,
                MaxExpiryDays = 21,
                MinConfidence = 0.6m
            }
        });

        _strategy = new ShortTermPutStrategy(_loggerMock.Object, appSettings);
    }

    [Fact]
    public void Name_ShouldReturnShortTermPut()
    {
        _strategy.Name.Should().Be("ShortTermPut");
    }

    [Fact]
    public void TargetExpiryDays_ShouldMatchSettings()
    {
        _strategy.TargetExpiryMinDays.Should().Be(14);
        _strategy.TargetExpiryMaxDays.Should().Be(21);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoMarketData_ReturnsEmpty()
    {
        // Arrange
        var data = new AggregatedMarketData
        {
            MarketData = null,
            TrendAnalysis = null,
            ShortTermPutOptions = []
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoOptions_ReturnsEmpty()
    {
        // Arrange
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("SPY", 450m),
            TrendAnalysis = CreateBullishTrend("SPY"),
            ShortTermPutOptions = []
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithDowntrend_ReturnsEmpty()
    {
        // Arrange
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("SPY", 450m),
            TrendAnalysis = new TrendAnalysis
            {
                Symbol = "SPY",
                Direction = TrendDirection.Down,
                Confidence = 0.8m,
                TrendStrength = 0.7m,
                ExpectedGrowthPercent = -3m
            },
            ShortTermPutOptions = CreateTestOptions("SPY", 450m)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithStrongUptrend_ReturnsRecommendations()
    {
        // Arrange
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("SPY", 450m),
            TrendAnalysis = CreateBullishTrend("SPY"),
            ShortTermPutOptions = CreateTestOptions("SPY", 450m)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(r =>
        {
            r.Symbol.Should().Be("SPY");
            r.StrategyName.Should().Be("ShortTermPut");
            r.StrikePrice.Should().BeLessThan(450m);
            r.DaysToExpiry.Should().BeInRange(14, 21);
        });
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsMaxThreeRecommendations()
    {
        // Arrange
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("SPY", 450m),
            TrendAnalysis = CreateBullishTrend("SPY"),
            ShortTermPutOptions = CreateTestOptions("SPY", 450m, count: 10)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Count().Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task AnalyzeAsync_Recommendations_SortedByConfidenceDescending()
    {
        // Arrange
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("SPY", 450m),
            TrendAnalysis = CreateBullishTrend("SPY"),
            ShortTermPutOptions = CreateTestOptions("SPY", 450m, count: 5)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().BeInDescendingOrder(r => r.Confidence);
    }

    private static MarketData CreateTestMarketData(string symbol, decimal price)
    {
        return new MarketData
        {
            Symbol = symbol,
            CurrentPrice = price,
            Open = price * 0.99m,
            High = price * 1.01m,
            Low = price * 0.98m,
            Close = price,
            Volume = 10000000,
            AverageVolume = 8000000,
            High52Week = price * 1.15m,
            Low52Week = price * 0.85m,
            MovingAverage20 = price * 0.98m,
            MovingAverage50 = price * 0.95m,
            MovingAverage200 = price * 0.90m,
            RSI = 55m,
            MACD = 2.5m,
            MACDSignal = 1.8m,
            Timestamp = DateTime.UtcNow
        };
    }

    private static TrendAnalysis CreateBullishTrend(string symbol)
    {
        return new TrendAnalysis
        {
            Symbol = symbol,
            Direction = TrendDirection.Up,
            Confidence = 0.75m,
            TrendStrength = 0.7m,
            ExpectedGrowthPercent = 4.5m,
            AnalysisPeriodDays = 21
        };
    }

    private static List<OptionContract> CreateTestOptions(string symbol, decimal price, int count = 5)
    {
        var options = new List<OptionContract>();
        var baseExpiry = DateTime.Today.AddDays(17); // Middle of 14-21 day range

        for (int i = 0; i < count; i++)
        {
            var strikeOffset = 0.05m + (i * 0.02m); // 5% to 13% OTM
            var strike = Math.Round(price * (1 - strikeOffset), 0);

            options.Add(new OptionContract
            {
                Symbol = $"{symbol}{baseExpiry:yyMMdd}P{strike:00000000}",
                Strike = strike,
                Expiry = baseExpiry,
                Bid = 1.50m + (i * 0.3m),
                Ask = 1.70m + (i * 0.3m),
                ImpliedVolatility = 0.22m + (i * 0.02m),
                OpenInterest = 5000 - (i * 500),
                Delta = -0.25m - (i * 0.03m),
                Theta = -0.03m - (i * 0.005m)
            });
        }

        return options;
    }
}
