using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Models;
using TradingService.Services.Strategies;

namespace TradingService.Tests.Strategies;

public class VolatilityCrushStrategyTests
{
    private readonly VolatilityCrushStrategy _strategy;
    private readonly Mock<ILogger<VolatilityCrushStrategy>> _loggerMock;

    public VolatilityCrushStrategyTests()
    {
        _loggerMock = new Mock<ILogger<VolatilityCrushStrategy>>();
        var appSettings = Options.Create(new AppSettings
        {
            Strategy = new StrategySettings
            {
                MinExpiryDays = 14,
                MaxExpiryDays = 21,
                MinConfidence = 0.6m
            }
        });

        _strategy = new VolatilityCrushStrategy(_loggerMock.Object, appSettings);
    }

    [Fact]
    public void Name_ShouldReturnVolatilityCrush()
    {
        _strategy.Name.Should().Be("VolatilityCrush");
    }

    [Fact]
    public void Description_ShouldMentionVolatility()
    {
        _strategy.Description.Should().Contain("volatility");
    }

    [Fact]
    public async Task AnalyzeAsync_WithLowIV_ReturnsEmpty()
    {
        // Arrange - IV below 25% threshold
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("AAPL", 180m),
            TrendAnalysis = CreateStableTrend("AAPL"),
            ShortTermPutOptions = CreateLowIVOptions("AAPL", 180m)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithElevatedIV_ReturnsRecommendations()
    {
        // Arrange - IV in 30-50% range (optimal for vol crush)
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("AAPL", 180m),
            TrendAnalysis = CreateStableTrend("AAPL"),
            ShortTermPutOptions = CreateElevatedIVOptions("AAPL", 180m)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(r =>
        {
            r.StrategyName.Should().Be("VolatilityCrush");
            r.StrikePrice.Should().BeLessThan(180m);
        });
    }

    [Fact]
    public async Task AnalyzeAsync_WithExtremelyHighIV_ReturnsEmpty()
    {
        // Arrange - IV above 60% (too risky)
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("AAPL", 180m),
            TrendAnalysis = CreateStableTrend("AAPL"),
            ShortTermPutOptions = CreateExtremeIVOptions("AAPL", 180m)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithStrongDowntrend_ReturnsEmpty()
    {
        // Arrange
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("AAPL", 180m),
            TrendAnalysis = new TrendAnalysis
            {
                Symbol = "AAPL",
                Direction = TrendDirection.Down,
                TrendStrength = 0.8m, // Strong downtrend
                Confidence = 0.7m,
                ExpectedGrowthPercent = -5m
            },
            ShortTermPutOptions = CreateElevatedIVOptions("AAPL", 180m)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_RecommendationsHaveHigherAnnualizedReturn()
    {
        // Vol crush strategy requires higher returns due to elevated IV
        var data = new AggregatedMarketData
        {
            MarketData = CreateTestMarketData("AAPL", 180m),
            TrendAnalysis = CreateStableTrend("AAPL"),
            ShortTermPutOptions = CreateElevatedIVOptions("AAPL", 180m)
        };

        // Act
        var result = await _strategy.AnalyzeAsync(data);

        // Assert - Verify premiums are substantial (high IV = high premium)
        result.Should().AllSatisfy(r =>
        {
            r.Premium.Should().BeGreaterThan(1.0m);
        });
    }

    private static MarketData CreateTestMarketData(string symbol, decimal price)
    {
        return new MarketData
        {
            Symbol = symbol,
            CurrentPrice = price,
            Open = price * 0.99m,
            High = price * 1.02m,
            Low = price * 0.98m,
            Close = price,
            Volume = 15000000,
            AverageVolume = 12000000,
            High52Week = price * 1.20m,
            Low52Week = price * 0.80m,
            MovingAverage20 = price * 0.99m,
            MovingAverage50 = price * 0.97m,
            MovingAverage200 = price * 0.92m,
            RSI = 52m,
            MACD = 1.2m,
            MACDSignal = 0.9m,
            Timestamp = DateTime.UtcNow
        };
    }

    private static TrendAnalysis CreateStableTrend(string symbol)
    {
        return new TrendAnalysis
        {
            Symbol = symbol,
            Direction = TrendDirection.Sideways,
            Confidence = 0.65m,
            TrendStrength = 0.4m,
            ExpectedGrowthPercent = 1.5m,
            AnalysisPeriodDays = 21
        };
    }

    private static List<OptionContract> CreateLowIVOptions(string symbol, decimal price)
    {
        return CreateOptionsWithIV(symbol, price, 0.15m); // 15% IV
    }

    private static List<OptionContract> CreateElevatedIVOptions(string symbol, decimal price)
    {
        return CreateOptionsWithIV(symbol, price, 0.35m); // 35% IV
    }

    private static List<OptionContract> CreateExtremeIVOptions(string symbol, decimal price)
    {
        return CreateOptionsWithIV(symbol, price, 0.70m); // 70% IV
    }

    private static List<OptionContract> CreateOptionsWithIV(string symbol, decimal price, decimal iv)
    {
        var options = new List<OptionContract>();
        var baseExpiry = DateTime.Today.AddDays(17);

        for (int i = 0; i < 5; i++)
        {
            var strikeOffset = 0.05m + (i * 0.025m);
            var strike = Math.Round(price * (1 - strikeOffset), 0);

            // Higher IV = higher premium
            var basePremium = iv * 5m;

            options.Add(new OptionContract
            {
                Symbol = $"{symbol}{baseExpiry:yyMMdd}P{strike:00000000}",
                Strike = strike,
                Expiry = baseExpiry,
                Bid = basePremium + (i * 0.2m),
                Ask = basePremium + 0.20m + (i * 0.2m),
                ImpliedVolatility = iv + (i * 0.02m),
                OpenInterest = 3000,
                Delta = -0.28m - (i * 0.03m),
                Theta = -0.04m - (i * 0.01m)
            });
        }

        return options;
    }
}
