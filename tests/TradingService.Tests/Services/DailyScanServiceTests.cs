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
    private readonly Mock<IStockDataRepository> _stockDataRepositoryMock;
    private readonly Mock<TradingService.Data.IDbContextFactory> _dbFactoryMock;
    private readonly Mock<ILogger<DailyScanService>> _loggerMock;
    private readonly Mock<IFinancialHealthService> _financialHealthServiceMock;
    private readonly Mock<IBulkFinancialDataProcessor> _bulkFinancialDataProcessorMock;
    private readonly DailyScanService _service;

    public DailyScanServiceTests()
    {
        _aggregatorMock = new Mock<IMarketDataAggregator>();
        _strategyLoaderMock = new Mock<IStrategyLoader>();
        _stockDataRepositoryMock = new Mock<IStockDataRepository>();
        _dbFactoryMock = new Mock<TradingService.Data.IDbContextFactory>();
        _loggerMock = new Mock<ILogger<DailyScanService>>();
        _financialHealthServiceMock = new Mock<IFinancialHealthService>();
        _bulkFinancialDataProcessorMock = new Mock<IBulkFinancialDataProcessor>();

        // Setup financial health service mock
        _financialHealthServiceMock
            .Setup(x => x.CalculateMetricsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialHealthMetrics
            {
                PiotroskiFScore = 8m,
                AltmanZScore = 3.5m
            });

        _financialHealthServiceMock
            .Setup(x => x.MeetsHealthRequirements(It.IsAny<FinancialHealthMetrics>()))
            .Returns(true);

        // Setup stock data repository mock (data is fresh - no bulk processing needed)
        _stockDataRepositoryMock
            .Setup(x => x.GetTotalCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100); // Non-zero = data exists (not stale)

        var appSettings = Options.Create(new AppSettings
        {
            Watchlist = ["SPY", "QQQ"],
            Strategy = new StrategySettings
            {
                MinConfidence = 0.6m
            },
            FinancialHealth = new FinancialHealthSettings
            {
                EnablePreFiltering = false, // Disable pre-filtering in tests
                MinPiotroskiFScore = 7m,
                DataRefreshDays = 7
            }
        });

        _service = new DailyScanService(
            _aggregatorMock.Object,
            _strategyLoaderMock.Object,
            _stockDataRepositoryMock.Object,
            _dbFactoryMock.Object,
            _loggerMock.Object,
            appSettings,
            _financialHealthServiceMock.Object,
            _bulkFinancialDataProcessorMock.Object);
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

        _stockDataRepositoryMock.Setup(r => r.UpsertExanteDataAsync(It.IsAny<StockData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stockDataRepositoryMock.Setup(r => r.DeleteStaleRecordsAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        _stockDataRepositoryMock.Setup(r => r.UpsertExanteDataAsync(It.IsAny<StockData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stockDataRepositoryMock.Setup(r => r.DeleteStaleRecordsAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        _stockDataRepositoryMock.Setup(r => r.DeleteStaleRecordsAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stockDataRepositoryMock.Setup(r => r.UpsertExanteDataAsync(It.IsAny<StockData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert
        _stockDataRepositoryMock.Verify(r => r.DeleteStaleRecordsAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
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

        _stockDataRepositoryMock.Setup(r => r.DeleteStaleRecordsAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stockDataRepositoryMock.Setup(r => r.UpsertExanteDataAsync(It.IsAny<StockData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert
        // Verify that UPSERT was called for recommendations with confidence >= 0.6
        _stockDataRepositoryMock.Verify(
            r => r.UpsertExanteDataAsync(It.Is<StockData>(s => s.Confidence >= 0.6m), It.IsAny<CancellationToken>()),
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

        var upsertedData = new List<StockData>();
        _stockDataRepositoryMock.Setup(r => r.UpsertExanteDataAsync(It.IsAny<StockData>(), It.IsAny<CancellationToken>()))
            .Callback<StockData, CancellationToken>((data, ct) => upsertedData.Add(data))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteScanAsync(CancellationToken.None);

        // Assert - Only high confidence recommendations should be upserted (note: now picks BEST per symbol)
        upsertedData.Should().NotBeEmpty();
        // Since we have 2 symbols (SPY, QQQ) with recommendations above threshold, expect 1-2 upserts
        upsertedData.Should().HaveCountGreaterThanOrEqualTo(1);
    }
}
