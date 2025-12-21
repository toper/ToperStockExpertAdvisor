using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data.Entities;
using TradingService.Models;
using TradingService.Services.Brokers;
using TradingService.Services.Interfaces;

namespace TradingService.Tests.Services;

public class OrderExecutorTests
{
    private readonly Mock<IBrokerFactory> _brokerFactoryMock;
    private readonly Mock<IBroker> _brokerMock;
    private readonly Mock<ILogger<OrderExecutor>> _loggerMock;
    private readonly OrderExecutor _executor;

    public OrderExecutorTests()
    {
        _brokerFactoryMock = new Mock<IBrokerFactory>();
        _brokerMock = new Mock<IBroker>();
        _loggerMock = new Mock<ILogger<OrderExecutor>>();

        var appSettings = Options.Create(new AppSettings
        {
            Broker = new BrokerSettings
            {
                DefaultBroker = "Exante"
            }
        });

        _brokerFactoryMock.Setup(f => f.CreateBroker(It.IsAny<string>()))
            .Returns(_brokerMock.Object);

        _brokerMock.Setup(b => b.IsConnectedAsync())
            .ReturnsAsync(true);

        _executor = new OrderExecutor(
            _brokerFactoryMock.Object,
            appSettings,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_UsesDefaultBroker()
    {
        // Arrange
        var recommendation = CreateValidRecommendation();
        _brokerMock.Setup(b => b.PlacePutSellOrderAsync(It.IsAny<PutSellOrder>()))
            .ReturnsAsync(new OrderResult { Success = true, OrderId = "123" });

        // Act
        await _executor.ExecuteRecommendationAsync(recommendation, 10000m);

        // Assert
        _brokerFactoryMock.Verify(f => f.CreateBroker("Exante"), Times.Once);
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_UsesSpecifiedBroker()
    {
        // Arrange
        var recommendation = CreateValidRecommendation();
        _brokerMock.Setup(b => b.PlacePutSellOrderAsync(It.IsAny<PutSellOrder>()))
            .ReturnsAsync(new OrderResult { Success = true, OrderId = "123" });

        // Act
        await _executor.ExecuteRecommendationAsync(recommendation, 10000m, "OtherBroker");

        // Assert
        _brokerFactoryMock.Verify(f => f.CreateBroker("OtherBroker"), Times.Once);
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_RejectsExpiredRecommendation()
    {
        // Arrange
        var recommendation = new PutRecommendation
        {
            Symbol = "SPY",
            CurrentPrice = 450m,
            StrikePrice = 420m,
            Expiry = DateTime.Today.AddDays(-1), // Expired
            Premium = 2.50m,
            Confidence = 0.75m
        };

        // Act
        var result = await _executor.ExecuteRecommendationAsync(recommendation, 10000m);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("validation");
        _brokerMock.Verify(b => b.PlacePutSellOrderAsync(It.IsAny<PutSellOrder>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_RejectsLowPremium()
    {
        // Arrange
        var recommendation = new PutRecommendation
        {
            Symbol = "SPY",
            CurrentPrice = 450m,
            StrikePrice = 420m,
            Expiry = DateTime.Today.AddDays(17),
            Premium = 0.05m, // Below $0.10 minimum
            Confidence = 0.75m
        };

        // Act
        var result = await _executor.ExecuteRecommendationAsync(recommendation, 10000m);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_RejectsITMOptions()
    {
        // Arrange
        var recommendation = new PutRecommendation
        {
            Symbol = "SPY",
            CurrentPrice = 450m,
            StrikePrice = 460m, // ITM (strike > current price)
            Expiry = DateTime.Today.AddDays(17),
            Premium = 15.00m,
            Confidence = 0.75m
        };

        // Act
        var result = await _executor.ExecuteRecommendationAsync(recommendation, 10000m);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_RejectsInsufficientCapital()
    {
        // Arrange
        var recommendation = CreateValidRecommendation();

        // Act - Only $100 investment (not enough for margin)
        var result = await _executor.ExecuteRecommendationAsync(recommendation, 100m);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Insufficient capital");
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_ChecksBrokerConnection()
    {
        // Arrange
        var recommendation = CreateValidRecommendation();
        _brokerMock.Setup(b => b.IsConnectedAsync())
            .ReturnsAsync(false);

        // Act
        var result = await _executor.ExecuteRecommendationAsync(recommendation, 10000m);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not connected");
    }

    [Fact]
    public async Task ExecuteRecommendationAsync_PlacesOrderWithCorrectDetails()
    {
        // Arrange
        var recommendation = CreateValidRecommendation();
        PutSellOrder? capturedOrder = null;

        _brokerMock.Setup(b => b.PlacePutSellOrderAsync(It.IsAny<PutSellOrder>()))
            .Callback<PutSellOrder>(order => capturedOrder = order)
            .ReturnsAsync(new OrderResult { Success = true, OrderId = "123" });

        // Act
        await _executor.ExecuteRecommendationAsync(recommendation, 10000m);

        // Assert
        capturedOrder.Should().NotBeNull();
        capturedOrder!.UnderlyingSymbol.Should().Be("SPY");
        capturedOrder.Strike.Should().Be(420m);
        capturedOrder.LimitPrice.Should().Be(2.50m);
        capturedOrder.Quantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateContracts_ReturnsCorrectCount()
    {
        // Arrange
        var recommendation = new PutRecommendation
        {
            StrikePrice = 400m,
            Premium = 2.00m
        };

        // Margin per contract = 400 * 100 * 0.20 - 2 * 100 = 8000 - 200 = 7800
        // 10000 / 7800 = 1.28 -> 1 contract

        // Act
        var contracts = _executor.CalculateContracts(recommendation, 10000m);

        // Assert
        contracts.Should().Be(1);
    }

    [Fact]
    public void CalculateContracts_ReturnsMultipleContracts()
    {
        // Arrange
        var recommendation = new PutRecommendation
        {
            StrikePrice = 400m,
            Premium = 2.00m
        };

        // 50000 / 7800 = 6.4 -> 6 contracts

        // Act
        var contracts = _executor.CalculateContracts(recommendation, 50000m);

        // Assert
        contracts.Should().Be(6);
    }

    [Fact]
    public void BuildOptionSymbol_ReturnsCorrectFormat()
    {
        // Arrange
        var recommendation = new PutRecommendation
        {
            Symbol = "AAPL",
            StrikePrice = 180m,
            Expiry = new DateTime(2025, 1, 17)
        };

        // Act
        var symbol = _executor.BuildOptionSymbol(recommendation);

        // Assert - OCC format: AAPL  250117P00180000
        symbol.Should().StartWith("AAPL");
        symbol.Should().Contain("250117");
        symbol.Should().Contain("P");
        symbol.Should().Contain("180000");
    }

    private static PutRecommendation CreateValidRecommendation()
    {
        return new PutRecommendation
        {
            Symbol = "SPY",
            CurrentPrice = 450m,
            StrikePrice = 420m,
            Expiry = DateTime.Today.AddDays(17),
            DaysToExpiry = 17,
            Premium = 2.50m,
            Breakeven = 417.50m,
            Confidence = 0.75m,
            IsActive = true
        };
    }
}
