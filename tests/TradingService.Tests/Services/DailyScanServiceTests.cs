using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data.Entities;
using TradingService.Models;
using TradingService.Services;
using TradingService.Services.Interfaces;

namespace TradingService.Tests.Services;

public class DailyScanServiceTests
{
    private readonly Mock<IMarketDataAggregator> _aggregatorMock;
    private readonly Mock<IStrategyLoader> _strategyLoaderMock;
    private readonly Mock<IRecommendationRepository> _repositoryMock;
    private readonly Mock<TradingService.Data.IDbContextFactory> _dbFactoryMock;
    private readonly Mock<ILogger<DailyScanService>> _loggerMock;
    private readonly DailyScanService _service;

    public DailyScanServiceTests()
    {
        _aggregatorMock = new Mock<IMarketDataAggregator>();
        _strategyLoaderMock = new Mock<IStrategyLoader>();
        _repositoryMock = new Mock<IRecommendationRepository>();
        _dbFactoryMock = new Mock<TradingService.Data.IDbContextFactory>();
        _loggerMock = new Mock<ILogger<DailyScanService>>();

        var appSettings = Options.Create(new AppSettings
        {
            Watchlist = ["SPY", "QQQ"],
            Strategy = new StrategySettings
            {
                MinConfidence = 0.6m
            }
        });

        _service = new DailyScanService(
            _aggregatorMock.Object,
            _strategyLoaderMock.Object,
            _repositoryMock.Object,
            _dbFactoryMock.Object,
            _loggerMock.Object,
            appSettings);
    }

    [Fact]
    public async Task ExecuteScanAsync_LoadsAllStrategies()
    {
        // Arrange
        var mockStrategy = new Mock<IStrategy>();
        mockStrategy.Setup(s => s.Name).Returns("TestStrategy");
        mockStrategy.Setup(s => s.AnalyzeAsync(It.IsAny<AggregatedMarketData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PutRecommendation>());

        _strategyLoaderMock.Setup(l => l.LoadAllStrategies())
            .Returns([mockStrategy.Object]);

        _aggregatorMock.Setup(a => a.GetFullMarketDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new AggregatedMarketData());

        _repositoryMock.Setup(r => r.DeactivateOldRecommendationsAsync(It.IsAny<DateTime>()))
            .Returns(Task.FromResult(0));

        _repositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PutRecommendation>>()))
            .Returns(Task.FromResult(0));

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert
        _strategyLoaderMock.Verify(l => l.LoadAllStrategies(), Times.Once);
    }

    [Fact]
    public async Task ExecuteScanAsync_FetchesDataForEachSymbol()
    {
        // Arrange
        _strategyLoaderMock.Setup(l => l.LoadAllStrategies())
            .Returns(new List<IStrategy>());

        _aggregatorMock.Setup(a => a.GetFullMarketDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new AggregatedMarketData());

        _repositoryMock.Setup(r => r.DeactivateOldRecommendationsAsync(It.IsAny<DateTime>()))
            .Returns(Task.FromResult(0));

        _repositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PutRecommendation>>()))
            .Returns(Task.FromResult(0));

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert - Should fetch data for SPY and QQQ
        _aggregatorMock.Verify(a => a.GetFullMarketDataAsync("SPY"), Times.Once);
        _aggregatorMock.Verify(a => a.GetFullMarketDataAsync("QQQ"), Times.Once);
    }

    [Fact]
    public async Task ExecuteScanAsync_DeactivatesOldRecommendations()
    {
        // Arrange
        _strategyLoaderMock.Setup(l => l.LoadAllStrategies())
            .Returns(new List<IStrategy>());

        _aggregatorMock.Setup(a => a.GetFullMarketDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new AggregatedMarketData());

        _repositoryMock.Setup(r => r.DeactivateOldRecommendationsAsync(It.IsAny<DateTime>()))
            .Returns(Task.FromResult(5));

        _repositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PutRecommendation>>()))
            .Returns(Task.FromResult(0));

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.DeactivateOldRecommendationsAsync(It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteScanAsync_SavesHighConfidenceRecommendations()
    {
        // Arrange
        var recommendations = new List<PutRecommendation>
        {
            new() { Symbol = "SPY", Confidence = 0.75m, IsActive = true },
            new() { Symbol = "QQQ", Confidence = 0.80m, IsActive = true }
        };

        var mockStrategy = new Mock<IStrategy>();
        mockStrategy.Setup(s => s.Name).Returns("TestStrategy");
        mockStrategy.Setup(s => s.AnalyzeAsync(It.IsAny<AggregatedMarketData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recommendations);

        _strategyLoaderMock.Setup(l => l.LoadAllStrategies())
            .Returns([mockStrategy.Object]);

        _aggregatorMock.Setup(a => a.GetFullMarketDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new AggregatedMarketData());

        _repositoryMock.Setup(r => r.DeactivateOldRecommendationsAsync(It.IsAny<DateTime>()))
            .Returns(Task.FromResult(0));

        _repositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PutRecommendation>>()))
            .Returns(Task.FromResult(2));

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            r => r.AddRangeAsync(It.Is<IEnumerable<PutRecommendation>>(
                recs => recs.All(rec => rec.Confidence >= 0.6m))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteScanAsync_RespectsMinConfidenceThreshold()
    {
        // Arrange
        var recommendations = new List<PutRecommendation>
        {
            new() { Symbol = "SPY", Confidence = 0.55m, IsActive = true }, // Below threshold
            new() { Symbol = "QQQ", Confidence = 0.75m, IsActive = true }  // Above threshold
        };

        var mockStrategy = new Mock<IStrategy>();
        mockStrategy.Setup(s => s.Name).Returns("TestStrategy");
        mockStrategy.Setup(s => s.AnalyzeAsync(It.IsAny<AggregatedMarketData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recommendations);

        _strategyLoaderMock.Setup(l => l.LoadAllStrategies())
            .Returns([mockStrategy.Object]);

        _aggregatorMock.Setup(a => a.GetFullMarketDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new AggregatedMarketData());

        _repositoryMock.Setup(r => r.DeactivateOldRecommendationsAsync(It.IsAny<DateTime>()))
            .Returns(Task.FromResult(0));

        IEnumerable<PutRecommendation>? savedRecommendations = null;
        _repositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PutRecommendation>>()))
            .Callback<IEnumerable<PutRecommendation>>(recs => savedRecommendations = recs.ToList())
            .Returns(Task.FromResult(1));

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert - Only high confidence recommendations should be saved
        savedRecommendations.Should().NotBeNull();
        savedRecommendations!.Should().AllSatisfy(r => r.Confidence.Should().BeGreaterThanOrEqualTo(0.6m));
    }
}
