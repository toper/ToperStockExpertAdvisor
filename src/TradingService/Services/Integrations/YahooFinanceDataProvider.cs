using Microsoft.Extensions.Logging;
using NodaTime;
using TradingService.Models;
using TradingService.Services.Interfaces;
using YahooQuotesApi;

namespace TradingService.Services.Integrations;

public class YahooFinanceDataProvider : IMarketDataProvider
{
    private readonly ILogger<YahooFinanceDataProvider> _logger;

    public YahooFinanceDataProvider(ILogger<YahooFinanceDataProvider> logger)
    {
        _logger = logger;
    }

    public async Task<MarketData?> GetMarketDataAsync(string symbol)
    {
        try
        {
            _logger.LogInformation("Fetching market data for {Symbol}", symbol);

            // Build YahooQuotes with history for calculating technical indicators
            var yahooQuotes = new YahooQuotesBuilder()
                .WithHistoryStartDate(Instant.FromUtc(DateTime.UtcNow.Year, 1, 1, 0, 0))
                .Build();

            // Get snapshot for current data
            var snapshot = await yahooQuotes.GetSnapshotAsync(symbol);

            if (snapshot == null)
            {
                _logger.LogWarning("No data found for symbol {Symbol}", symbol);
                return null;
            }

            // Get historical data for technical indicators
            var historyResult = await yahooQuotes.GetHistoryAsync(symbol);
            var prices = new List<HistoricalQuote>();

            if (historyResult.HasValue)
            {
                var history = historyResult.Value;
                prices = history.Ticks
                    .Select(t => new HistoricalQuote
                    {
                        Date = t.Date.ToDateTimeUtc(),
                        Open = (decimal)t.Open,
                        High = (decimal)t.High,
                        Low = (decimal)t.Low,
                        Close = (decimal)t.Close,
                        AdjustedClose = (decimal)t.AdjustedClose,
                        Volume = t.Volume
                    })
                    .ToList();
            }

            // Calculate technical indicators
            var ma20 = CalculateMovingAverage(prices, 20);
            var ma50 = CalculateMovingAverage(prices, 50);
            var ma200 = CalculateMovingAverage(prices, 200);
            var rsi = CalculateRSI(prices, 14);
            var (macd, macdSignal) = CalculateMACD(prices);

            // Get 52-week high/low from historical data
            var yearPrices = prices.Where(p => p.Date >= DateTime.UtcNow.AddDays(-365)).ToList();
            var high52Week = yearPrices.Any() ? yearPrices.Max(p => p.High) : (decimal)snapshot.RegularMarketPrice;
            var low52Week = yearPrices.Any() ? yearPrices.Min(p => p.Low) : (decimal)snapshot.RegularMarketPrice;

            return new MarketData
            {
                Symbol = symbol,
                CurrentPrice = (decimal)snapshot.RegularMarketPrice,
                Open = (decimal)snapshot.RegularMarketOpen,
                High = (decimal)snapshot.RegularMarketDayHigh,
                Low = (decimal)snapshot.RegularMarketDayLow,
                Close = (decimal)snapshot.RegularMarketPreviousClose,
                Volume = snapshot.RegularMarketVolume,
                AverageVolume = (decimal)snapshot.AverageDailyVolume3Month,
                High52Week = high52Week,
                Low52Week = low52Week,
                MovingAverage50 = ma50,
                MovingAverage200 = ma200,
                MovingAverage20 = ma20,
                RSI = rsi,
                MACD = macd,
                MACDSignal = macdSignal,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market data for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IEnumerable<HistoricalQuote>> GetHistoricalDataAsync(string symbol, int days)
    {
        try
        {
            _logger.LogInformation("Fetching {Days} days of historical data for {Symbol}", days, symbol);

            var startDate = DateTime.UtcNow.AddDays(-days - 10); // Add buffer for weekends/holidays
            var yahooQuotes = new YahooQuotesBuilder()
                .WithHistoryStartDate(Instant.FromUtc(startDate.Year, startDate.Month, startDate.Day, 0, 0))
                .Build();

            var historyResult = await yahooQuotes.GetHistoryAsync(symbol);

            if (!historyResult.HasValue)
            {
                _logger.LogWarning("No historical data found for symbol {Symbol}", symbol);
                return Enumerable.Empty<HistoricalQuote>();
            }

            var history = historyResult.Value;
            var endDate = DateTime.UtcNow;
            var cutoffDate = endDate.AddDays(-days);

            return history.Ticks
                .Select(t => new HistoricalQuote
                {
                    Date = t.Date.ToDateTimeUtc(),
                    Open = (decimal)t.Open,
                    High = (decimal)t.High,
                    Low = (decimal)t.Low,
                    Close = (decimal)t.Close,
                    AdjustedClose = (decimal)t.AdjustedClose,
                    Volume = t.Volume
                })
                .Where(h => h.Date >= cutoffDate && h.Date <= endDate)
                .OrderBy(h => h.Date)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical data for {Symbol}", symbol);
            return Enumerable.Empty<HistoricalQuote>();
        }
    }

    public async Task<DividendInfo?> GetDividendInfoAsync(string symbol)
    {
        try
        {
            _logger.LogInformation("Fetching dividend info for {Symbol}", symbol);

            var yahooQuotes = new YahooQuotesBuilder().Build();
            var snapshot = await yahooQuotes.GetSnapshotAsync(symbol);

            if (snapshot == null)
            {
                _logger.LogWarning("No dividend data found for symbol {Symbol}", symbol);
                return null;
            }

            var dividendYield = snapshot.TrailingAnnualDividendYield;
            var dividendRate = snapshot.TrailingAnnualDividendRate;

            return new DividendInfo
            {
                Symbol = symbol,
                DividendYield = (decimal)(dividendYield * 100), // Convert to percentage
                AnnualDividend = (decimal)dividendRate,
                ExDividendDate = snapshot.DividendDate != default ? snapshot.DividendDate.ToDateTimeUtc() : null,
                PaymentDate = null // Not available in snapshot
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dividend info for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<TrendAnalysis> AnalyzeTrendAsync(string symbol, int days = 21)
    {
        try
        {
            _logger.LogInformation("Analyzing trend for {Symbol} over {Days} days", symbol, days);

            var historicalData = await GetHistoricalDataAsync(symbol, days + 50);
            var quotes = historicalData.ToList();

            if (!quotes.Any())
            {
                _logger.LogWarning("No historical data available for trend analysis of {Symbol}", symbol);
                return CreateEmptyTrendAnalysis(symbol, days);
            }

            var recentQuotes = quotes.TakeLast(days).ToList();
            if (recentQuotes.Count < days / 2)
            {
                return CreateEmptyTrendAnalysis(symbol, days);
            }

            // Calculate trend using linear regression
            var (slope, intercept, r2) = CalculateLinearRegression(recentQuotes);

            // Calculate expected growth percentage
            var lastPrice = recentQuotes.Last().Close;
            var projectedPrice = (decimal)(slope * (days + 5) + intercept);
            var expectedGrowthPercent = lastPrice != 0 ? ((projectedPrice - lastPrice) / lastPrice) * 100 : 0;

            // Determine trend direction
            var percentageChange = ((recentQuotes.Last().Close - recentQuotes.First().Close) / recentQuotes.First().Close) * 100;
            var direction = percentageChange > 1 ? TrendDirection.Up :
                           percentageChange < -1 ? TrendDirection.Down :
                           TrendDirection.Sideways;

            // Calculate trend strength (0 to 1)
            var trendStrength = Math.Min(1, Math.Abs(percentageChange) / 10);

            // Calculate confidence based on R² and data consistency
            var confidence = CalculateConfidence(r2, recentQuotes);

            return new TrendAnalysis
            {
                Symbol = symbol,
                ExpectedGrowthPercent = Math.Round(expectedGrowthPercent, 2),
                TrendStrength = Math.Round((decimal)trendStrength, 3),
                Direction = direction,
                Confidence = Math.Round(confidence, 3),
                AnalysisPeriodDays = days
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing trend for {Symbol}", symbol);
            return CreateEmptyTrendAnalysis(symbol, days);
        }
    }

    private TrendAnalysis CreateEmptyTrendAnalysis(string symbol, int days) => new TrendAnalysis
    {
        Symbol = symbol,
        ExpectedGrowthPercent = 0,
        TrendStrength = 0,
        Direction = TrendDirection.Sideways,
        Confidence = 0,
        AnalysisPeriodDays = days
    };

    #region Technical Indicator Calculations

    private decimal CalculateMovingAverage(List<HistoricalQuote> prices, int period)
    {
        if (prices.Count < period)
            return 0;

        var recentPrices = prices
            .OrderByDescending(p => p.Date)
            .Take(period)
            .Select(p => p.Close)
            .ToList();

        return recentPrices.Any() ? Math.Round(recentPrices.Average(), 2) : 0;
    }

    private decimal CalculateRSI(List<HistoricalQuote> prices, int period = 14)
    {
        if (prices.Count < period + 1)
            return 50;

        var orderedPrices = prices.OrderBy(p => p.Date).ToList();
        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < orderedPrices.Count; i++)
        {
            var change = orderedPrices[i].Close - orderedPrices[i - 1].Close;
            if (change > 0)
            {
                gains.Add(change);
                losses.Add(0);
            }
            else
            {
                gains.Add(0);
                losses.Add(Math.Abs(change));
            }
        }

        if (gains.Count < period)
            return 50;

        var avgGain = gains.TakeLast(period).Average();
        var avgLoss = losses.TakeLast(period).Average();

        if (avgLoss == 0)
            return 100;

        var rs = avgGain / avgLoss;
        var rsi = 100 - (100 / (1 + rs));

        return Math.Round(rsi, 2);
    }

    private (decimal macd, decimal signal) CalculateMACD(List<HistoricalQuote> prices)
    {
        if (prices.Count < 26)
            return (0, 0);

        var closePrices = prices
            .OrderBy(p => p.Date)
            .Select(p => p.Close)
            .ToList();

        var ema12 = CalculateEMA(closePrices, 12);
        var ema26 = CalculateEMA(closePrices, 26);
        var macdLine = ema12 - ema26;

        var macdValues = new List<decimal>();
        for (int i = 25; i < closePrices.Count; i++)
        {
            var shortEma = CalculateEMA(closePrices.Take(i + 1).ToList(), 12);
            var longEma = CalculateEMA(closePrices.Take(i + 1).ToList(), 26);
            macdValues.Add(shortEma - longEma);
        }

        var signalLine = macdValues.Count >= 9 ? CalculateEMA(macdValues, 9) : 0;

        return (Math.Round(macdLine, 4), Math.Round(signalLine, 4));
    }

    private decimal CalculateEMA(List<decimal> prices, int period)
    {
        if (prices.Count < period)
            return prices.LastOrDefault();

        var multiplier = 2.0m / (period + 1);
        var ema = prices.Take(period).Average();

        foreach (var price in prices.Skip(period))
        {
            ema = (price * multiplier) + (ema * (1 - multiplier));
        }

        return ema;
    }

    private (double slope, double intercept, double r2) CalculateLinearRegression(List<HistoricalQuote> quotes)
    {
        var n = quotes.Count;
        if (n < 2)
            return (0, 0, 0);

        var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
        var y = quotes.Select(q => (double)q.Close).ToArray();

        var xMean = x.Average();
        var yMean = y.Average();

        var xVariance = x.Select(xi => Math.Pow(xi - xMean, 2)).Sum();
        var covariance = x.Select((xi, i) => (xi - xMean) * (y[i] - yMean)).Sum();

        if (xVariance == 0)
            return (0, yMean, 0);

        var slope = covariance / xVariance;
        var intercept = yMean - slope * xMean;

        var yPredicted = x.Select(xi => slope * xi + intercept).ToArray();
        var ssRes = y.Select((yi, i) => Math.Pow(yi - yPredicted[i], 2)).Sum();
        var ssTot = y.Select(yi => Math.Pow(yi - yMean, 2)).Sum();
        var r2 = ssTot != 0 ? 1 - (ssRes / ssTot) : 0;

        return (slope, intercept, r2);
    }

    private decimal CalculateConfidence(double r2, List<HistoricalQuote> quotes)
    {
        var r2Confidence = (decimal)r2;

        var consistencyScore = 0m;
        for (int i = 1; i < quotes.Count; i++)
        {
            if ((quotes[i].Close >= quotes[i - 1].Close && quotes.Last().Close >= quotes.First().Close) ||
                (quotes[i].Close <= quotes[i - 1].Close && quotes.Last().Close <= quotes.First().Close))
            {
                consistencyScore += 1m / (quotes.Count - 1);
            }
        }

        var confidence = (r2Confidence * 0.6m) + (consistencyScore * 0.4m);
        return Math.Min(1, Math.Max(0, confidence));
    }

    #endregion
}