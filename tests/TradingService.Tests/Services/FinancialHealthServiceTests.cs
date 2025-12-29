using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Models;
using TradingService.Services;
using TradingService.Services.Integrations;
using TradingService.Services.Interfaces;
using Xunit;

namespace TradingService.Tests.Services;

/// <summary>
/// Unit tests for FinancialHealthService, focusing on Piotroski F-Score calculation
/// </summary>
public class FinancialHealthServiceTests
{
    private readonly Mock<ILogger<FinancialHealthService>> _loggerMock;
    private readonly Mock<ISimFinDataProvider> _simFinProviderMock;
    private readonly Mock<IMarketDataProvider> _marketDataProviderMock;
    private readonly FinancialHealthService _service;

    public FinancialHealthServiceTests()
    {
        _loggerMock = new Mock<ILogger<FinancialHealthService>>();
        _simFinProviderMock = new Mock<ISimFinDataProvider>();
        _marketDataProviderMock = new Mock<IMarketDataProvider>();

        _service = new FinancialHealthService(
            _loggerMock.Object,
            _simFinProviderMock.Object,
            _marketDataProviderMock.Object);
    }

    [Fact]
    public async Task CalculateMetricsAsync_WithPerfectFScore_Returns9()
    {
        // Arrange - Company with all 9 Piotroski criteria met
        var symbol = "AAPL";
        var simFinData = CreatePerfectFScoreCompany();
        var marketData = new MarketData { CurrentPrice = 150m };

        _simFinProviderMock
            .Setup(x => x.GetCompanyDataAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(simFinData);

        _marketDataProviderMock
            .Setup(x => x.GetMarketDataAsync(symbol))
            .ReturnsAsync(marketData);

        // Act
        var result = await _service.CalculateMetricsAsync(symbol);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(9m, result.PiotroskiFScore);
    }

    [Fact]
    public async Task CalculateMetricsAsync_WithPoorFScore_ReturnsLowScore()
    {
        // Arrange - Company with poor fundamentals
        var symbol = "POOR";
        var simFinData = CreatePoorFScoreCompany();
        var marketData = new MarketData { CurrentPrice = 10m };

        _simFinProviderMock
            .Setup(x => x.GetCompanyDataAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(simFinData);

        _marketDataProviderMock
            .Setup(x => x.GetMarketDataAsync(symbol))
            .ReturnsAsync(marketData);

        // Act
        var result = await _service.CalculateMetricsAsync(symbol);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.PiotroskiFScore);
        Assert.True(result.PiotroskiFScore < 4m, "Poor company should have F-Score < 4");
    }

    [Fact]
    public async Task CalculateMetricsAsync_WithNoHistoricalData_ReturnsPartialScore()
    {
        // Arrange - Company with no previous period data
        var symbol = "NEW";
        var simFinData = CreateCompanyWithoutHistoricalData();
        var marketData = new MarketData { CurrentPrice = 50m };

        _simFinProviderMock
            .Setup(x => x.GetCompanyDataAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(simFinData);

        _marketDataProviderMock
            .Setup(x => x.GetMarketDataAsync(symbol))
            .ReturnsAsync(marketData);

        // Act
        var result = await _service.CalculateMetricsAsync(symbol);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.PiotroskiFScore);
        // Without historical data, max score is 4 (only current period criteria)
        Assert.True(result.PiotroskiFScore <= 4m, "Without historical data, max F-Score is 4");
    }

    [Fact]
    public void MeetsHealthRequirements_WithHighScores_ReturnsTrue()
    {
        // Arrange
        var metrics = new FinancialHealthMetrics
        {
            PiotroskiFScore = 8m,
            AltmanZScore = 3.5m
        };

        // Act
        var result = _service.MeetsHealthRequirements(metrics);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void MeetsHealthRequirements_WithLowFScore_ReturnsFalse()
    {
        // Arrange
        var metrics = new FinancialHealthMetrics
        {
            PiotroskiFScore = 5m,
            AltmanZScore = 3.5m
        };

        // Act
        var result = _service.MeetsHealthRequirements(metrics);

        // Assert
        Assert.False(result, "F-Score below 7 should not meet requirements");
    }

    [Fact]
    public void MeetsHealthRequirements_WithLowZScore_ReturnsFalse()
    {
        // Arrange
        var metrics = new FinancialHealthMetrics
        {
            PiotroskiFScore = 8m,
            AltmanZScore = 1.5m
        };

        // Act
        var result = _service.MeetsHealthRequirements(metrics);

        // Assert
        Assert.False(result, "Z-Score below 1.81 should not meet requirements");
    }

    #region Test Data Helpers

    private SimFinCompanyData CreatePerfectFScoreCompany()
    {
        // Company that meets all 9 Piotroski criteria
        return new SimFinCompanyData
        {
            CompanyInfo = new SimFinCompanyResponse { Ticker = "PERFECT" },

            // Current period - Strong fundamentals
            BalanceSheet = new SimFinBalanceSheet
            {
                TotalAssets = 1000000m,
                TotalCash = 200000m,
                TotalDebt = 100000m,
                LongTermDebt = 80000m, // Decreased from previous (important for F5)
                CurrentAssets = 500000m,
                CurrentLiabilities = 200000m, // Better ratio than previous
                TotalEquity = 800000m,
                RetainedEarnings = 400000m,
                TotalLiabilities = 200000m
            },
            IncomeStatement = new SimFinIncomeStatement
            {
                Revenue = 800000m, // Increased from previous
                OperatingIncome = 160000m, // Better margin than previous
                NetIncome = 120000m, // Positive and improved
                SharesOutstanding = 10000000m // Same or less than previous
            },
            CashFlow = new SimFinCashFlow
            {
                OperatingCashFlow = 150000m // Greater than Net Income (good accruals)
            },

            // Previous period - Good but not as strong
            PreviousBalanceSheet = new SimFinBalanceSheet
            {
                TotalAssets = 950000m,
                TotalDebt = 120000m,
                LongTermDebt = 100000m, // Higher long-term debt ratio than current (for F5)
                CurrentAssets = 450000m,
                CurrentLiabilities = 220000m, // Worse ratio
                TotalEquity = 750000m
            },
            PreviousIncomeStatement = new SimFinIncomeStatement
            {
                Revenue = 700000m, // Lower revenue
                OperatingIncome = 126000m, // Lower margin (126k/700k vs 160k/800k)
                NetIncome = 90000m, // Lower ROA
                SharesOutstanding = 10000000m // No dilution
            }
        };
    }

    private SimFinCompanyData CreatePoorFScoreCompany()
    {
        // Company that meets few Piotroski criteria
        return new SimFinCompanyData
        {
            CompanyInfo = new SimFinCompanyResponse { Ticker = "POOR" },

            // Current period - Poor fundamentals
            BalanceSheet = new SimFinBalanceSheet
            {
                TotalAssets = 1000000m,
                TotalCash = 50000m,
                TotalDebt = 400000m,
                LongTermDebt = 350000m, // Increased from previous (bad for F5)
                CurrentAssets = 300000m,
                CurrentLiabilities = 250000m, // Worse ratio than previous (bad)
                TotalEquity = 600000m,
                RetainedEarnings = 100000m,
                TotalLiabilities = 400000m
            },
            IncomeStatement = new SimFinIncomeStatement
            {
                Revenue = 500000m, // Decreased from previous (bad)
                OperatingIncome = 50000m, // Worse margin (bad)
                NetIncome = -20000m, // Negative (bad)
                SharesOutstanding = 12000000m // Increased - dilution (bad)
            },
            CashFlow = new SimFinCashFlow
            {
                OperatingCashFlow = -30000m // Negative (bad)
            },

            // Previous period - Was better
            PreviousBalanceSheet = new SimFinBalanceSheet
            {
                TotalAssets = 950000m,
                TotalDebt = 300000m,
                LongTermDebt = 250000m, // Lower long-term debt (current increased - bad for F5)
                CurrentAssets = 400000m,
                CurrentLiabilities = 200000m, // Better ratio
                TotalEquity = 650000m
            },
            PreviousIncomeStatement = new SimFinIncomeStatement
            {
                Revenue = 600000m, // Higher revenue
                OperatingIncome = 72000m, // Better margin
                NetIncome = 40000m, // Positive ROA
                SharesOutstanding = 10000000m // Less shares
            }
        };
    }

    private SimFinCompanyData CreateCompanyWithoutHistoricalData()
    {
        // Company with good current data but no historical comparison
        return new SimFinCompanyData
        {
            CompanyInfo = new SimFinCompanyResponse { Ticker = "NEW" },

            // Current period - Good fundamentals
            BalanceSheet = new SimFinBalanceSheet
            {
                TotalAssets = 1000000m,
                TotalCash = 150000m,
                TotalDebt = 200000m,
                LongTermDebt = 150000m,
                CurrentAssets = 400000m,
                CurrentLiabilities = 200000m,
                TotalEquity = 800000m,
                RetainedEarnings = 300000m,
                TotalLiabilities = 200000m
            },
            IncomeStatement = new SimFinIncomeStatement
            {
                Revenue = 600000m,
                OperatingIncome = 120000m,
                NetIncome = 80000m, // Positive ROA
                SharesOutstanding = 10000000m
            },
            CashFlow = new SimFinCashFlow
            {
                OperatingCashFlow = 100000m // Greater than Net Income
            },

            // No previous period data
            PreviousBalanceSheet = null,
            PreviousIncomeStatement = null,
            PreviousCashFlow = null
        };
    }

    #endregion
}
