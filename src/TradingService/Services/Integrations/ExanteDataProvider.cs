using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Models;
using TradingService.Services.Interfaces;
using YahooQuotesApi;

namespace TradingService.Services.Integrations;

/// <summary>
/// Options data provider using Yahoo Finance API as primary source
/// Can be extended to use Exante API when credentials are configured
/// </summary>
public class ExanteDataProvider : IOptionsDataProvider
{
    private readonly ILogger<ExanteDataProvider> _logger;
    private readonly AppSettings _appSettings;

    public ExanteDataProvider(
        ILogger<ExanteDataProvider> logger,
        IOptions<AppSettings> appSettings)
    {
        _logger = logger;
        _appSettings = appSettings.Value;
    }

    public async Task<OptionsChain?> GetOptionsChainAsync(string symbol)
    {
        try
        {
            _logger.LogInformation("Fetching options chain for {Symbol}", symbol);

            // Get current price from Yahoo Finance
            var yahooQuotes = new YahooQuotesBuilder().Build();
            var snapshot = await yahooQuotes.GetSnapshotAsync(symbol);

            if (snapshot == null)
            {
                _logger.LogWarning("No securities data found for {Symbol}", symbol);
                return null;
            }

            var currentPrice = snapshot.RegularMarketPrice;

            if (currentPrice == 0)
            {
                _logger.LogWarning("Cannot fetch options chain - current price is 0 for {Symbol}", symbol);
                return null;
            }

            // Generate synthetic options data for testing
            // TODO: Replace with actual Exante API when credentials are available
            var putOptions = GenerateSyntheticPutOptions(symbol, (decimal)currentPrice);
            var callOptions = GenerateSyntheticCallOptions(symbol, (decimal)currentPrice);

            return new OptionsChain
            {
                UnderlyingSymbol = symbol,
                PutOptions = putOptions,
                CallOptions = callOptions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching options chain for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IEnumerable<OptionContract>> GetShortTermPutOptionsAsync(
        string symbol,
        int minDays = 14,
        int maxDays = 21)
    {
        try
        {
            _logger.LogInformation(
                "Fetching short-term PUT options for {Symbol} ({MinDays}-{MaxDays} days)",
                symbol, minDays, maxDays);

            var optionsChain = await GetOptionsChainAsync(symbol);

            if (optionsChain == null)
            {
                _logger.LogWarning("No options chain available for {Symbol}", symbol);
                return Enumerable.Empty<OptionContract>();
            }

            // Filter PUT options by expiry date range
            var today = DateTime.Today;
            var minExpiry = today.AddDays(minDays);
            var maxExpiry = today.AddDays(maxDays);

            var filteredOptions = optionsChain.PutOptions
                .Where(opt => opt.Expiry >= minExpiry && opt.Expiry <= maxExpiry)
                .OrderBy(opt => opt.Expiry)
                .ThenBy(opt => opt.Strike)
                .ToList();

            _logger.LogInformation(
                "Found {Count} short-term PUT options for {Symbol} between {MinExpiry:yyyy-MM-dd} and {MaxExpiry:yyyy-MM-dd}",
                filteredOptions.Count, symbol, minExpiry, maxExpiry);

            return filteredOptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error fetching short-term PUT options for {Symbol}", symbol);
            return Enumerable.Empty<OptionContract>();
        }
    }

    #region Synthetic Options Generation (for testing/development)

    /// <summary>
    /// Generates synthetic PUT options for testing purposes
    /// Replace with actual API calls when Exante credentials are available
    /// </summary>
    private List<OptionContract> GenerateSyntheticPutOptions(string symbol, decimal currentPrice)
    {
        var options = new List<OptionContract>();
        var expiryDates = GetExpiryDates();

        foreach (var expiry in expiryDates)
        {
            // Generate strikes around current price (OTM puts)
            var strikes = GenerateStrikes(currentPrice, 0.7m, 0.99m, 0.025m);

            foreach (var strike in strikes)
            {
                var daysToExpiry = (expiry - DateTime.Today).Days;
                var moneyness = strike / currentPrice;

                // Calculate synthetic option values
                var impliedVol = CalculateSyntheticIV(moneyness, daysToExpiry);
                var (bid, ask) = CalculateSyntheticPremium(
                    currentPrice, strike, daysToExpiry, impliedVol, true);

                var delta = CalculateSyntheticDelta(
                    currentPrice, strike, daysToExpiry, impliedVol, true);

                var theta = CalculateSyntheticTheta(
                    currentPrice, strike, daysToExpiry, impliedVol, true);

                options.Add(new OptionContract
                {
                    Symbol = $"{symbol}{expiry:yyMMdd}P{strike:00000}",
                    Strike = strike,
                    Expiry = expiry,
                    Bid = bid,
                    Ask = ask,
                    ImpliedVolatility = impliedVol,
                    OpenInterest = Random.Shared.Next(10, 1000),
                    Delta = delta,
                    Theta = theta
                });
            }
        }

        return options;
    }

    /// <summary>
    /// Generates synthetic CALL options for testing purposes
    /// </summary>
    private List<OptionContract> GenerateSyntheticCallOptions(string symbol, decimal currentPrice)
    {
        var options = new List<OptionContract>();
        var expiryDates = GetExpiryDates();

        foreach (var expiry in expiryDates)
        {
            // Generate strikes around current price (OTM calls)
            var strikes = GenerateStrikes(currentPrice, 1.01m, 1.3m, 0.025m);

            foreach (var strike in strikes)
            {
                var daysToExpiry = (expiry - DateTime.Today).Days;
                var moneyness = strike / currentPrice;

                // Calculate synthetic option values
                var impliedVol = CalculateSyntheticIV(moneyness, daysToExpiry);
                var (bid, ask) = CalculateSyntheticPremium(
                    currentPrice, strike, daysToExpiry, impliedVol, false);

                var delta = CalculateSyntheticDelta(
                    currentPrice, strike, daysToExpiry, impliedVol, false);

                var theta = CalculateSyntheticTheta(
                    currentPrice, strike, daysToExpiry, impliedVol, false);

                options.Add(new OptionContract
                {
                    Symbol = $"{symbol}{expiry:yyMMdd}C{strike:00000}",
                    Strike = strike,
                    Expiry = expiry,
                    Bid = bid,
                    Ask = ask,
                    ImpliedVolatility = impliedVol,
                    OpenInterest = Random.Shared.Next(10, 1000),
                    Delta = delta,
                    Theta = theta
                });
            }
        }

        return options;
    }

    /// <summary>
    /// Get standard monthly expiry dates (3rd Friday of each month)
    /// </summary>
    private List<DateTime> GetExpiryDates()
    {
        var expiryDates = new List<DateTime>();
        var today = DateTime.Today;

        // Generate next 3 months of expiry dates
        for (int month = 0; month < 3; month++)
        {
            var targetMonth = today.AddMonths(month);
            var thirdFriday = GetThirdFriday(targetMonth.Year, targetMonth.Month);

            if (thirdFriday > today)
            {
                expiryDates.Add(thirdFriday);
            }
        }

        // Add weekly expiries for the next 4 weeks
        for (int week = 1; week <= 4; week++)
        {
            var friday = GetNextFriday(today.AddDays(7 * week));
            if (!expiryDates.Contains(friday))
            {
                expiryDates.Add(friday);
            }
        }

        return expiryDates.OrderBy(d => d).ToList();
    }

    private DateTime GetThirdFriday(int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)firstDay.DayOfWeek + 7) % 7;
        var firstFriday = firstDay.AddDays(daysUntilFriday);
        return firstFriday.AddDays(14); // Third Friday
    }

    private DateTime GetNextFriday(DateTime from)
    {
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)from.DayOfWeek + 7) % 7;
        if (daysUntilFriday == 0) daysUntilFriday = 7; // If today is Friday, get next Friday
        return from.AddDays(daysUntilFriday);
    }

    private List<decimal> GenerateStrikes(decimal currentPrice, decimal minRatio, decimal maxRatio, decimal step)
    {
        var strikes = new List<decimal>();
        var minStrike = Math.Round(currentPrice * minRatio / 5) * 5; // Round to nearest $5
        var maxStrike = Math.Round(currentPrice * maxRatio / 5) * 5;

        for (var strike = minStrike; strike <= maxStrike; strike += currentPrice * step)
        {
            strikes.Add(Math.Round(strike, 2));
        }

        return strikes;
    }

    private decimal CalculateSyntheticIV(decimal moneyness, int daysToExpiry)
    {
        // Simple IV smile model
        var baseVol = 0.20m; // 20% base volatility
        var smile = Math.Abs(1 - moneyness) * 0.5m; // Add smile effect
        var termStructure = Math.Max(0, (30 - daysToExpiry) / 100m); // Short-term premium

        return Math.Min(0.8m, baseVol + smile + termStructure);
    }

    private (decimal bid, decimal ask) CalculateSyntheticPremium(
        decimal spot, decimal strike, int daysToExpiry, decimal iv, bool isPut)
    {
        // Simplified Black-Scholes approximation for testing
        var timeToExpiry = daysToExpiry / 365m;
        var riskFreeRate = 0.05m; // 5% risk-free rate

        var intrinsicValue = isPut
            ? Math.Max(0, strike - spot)
            : Math.Max(0, spot - strike);

        // Time value decreases as we approach expiry
        var timeValue = spot * iv * (decimal)Math.Sqrt((double)timeToExpiry) * 0.4m;

        // Adjust for moneyness
        var moneyness = strike / spot;
        var moneynessAdjustment = isPut
            ? Math.Max(0, 1 - moneyness)
            : Math.Max(0, moneyness - 1);

        timeValue *= (1 - moneynessAdjustment);

        var midPrice = Math.Max(0.01m, intrinsicValue + timeValue);

        // Create bid-ask spread (wider for less liquid options)
        var spreadPercent = 0.02m + (0.03m * Math.Abs(1 - moneyness));
        var halfSpread = midPrice * spreadPercent;

        return (Math.Round(midPrice - halfSpread, 2), Math.Round(midPrice + halfSpread, 2));
    }

    private decimal CalculateSyntheticDelta(
        decimal spot, decimal strike, int daysToExpiry, decimal iv, bool isPut)
    {
        // Simplified delta calculation
        var moneyness = spot / strike;
        var timeAdjustment = Math.Min(1, daysToExpiry / 30m);

        decimal delta;
        if (isPut)
        {
            if (moneyness > 1.1m) // Deep OTM
                delta = -0.1m * timeAdjustment;
            else if (moneyness > 1.02m) // OTM
                delta = -0.3m * timeAdjustment;
            else if (moneyness > 0.98m) // ATM
                delta = -0.5m * timeAdjustment;
            else if (moneyness > 0.9m) // ITM
                delta = -0.7m * timeAdjustment;
            else // Deep ITM
                delta = -0.9m * timeAdjustment;
        }
        else
        {
            if (moneyness < 0.9m) // Deep OTM
                delta = 0.1m * timeAdjustment;
            else if (moneyness < 0.98m) // OTM
                delta = 0.3m * timeAdjustment;
            else if (moneyness < 1.02m) // ATM
                delta = 0.5m * timeAdjustment;
            else if (moneyness < 1.1m) // ITM
                delta = 0.7m * timeAdjustment;
            else // Deep ITM
                delta = 0.9m * timeAdjustment;
        }

        return Math.Round(delta, 3);
    }

    private decimal CalculateSyntheticTheta(
        decimal spot, decimal strike, int daysToExpiry, decimal iv, bool isPut)
    {
        // Simplified theta calculation (time decay)
        var timeToExpiry = Math.Max(1, daysToExpiry) / 365m;
        var moneyness = spot / strike;

        // Base theta (always negative - options lose value over time)
        var baseTheta = -spot * iv / (8 * (decimal)Math.Sqrt((double)timeToExpiry));

        // Adjust for moneyness (ATM options have highest theta)
        var moneynessMultiplier = 1 - Math.Abs(1 - moneyness);

        return Math.Round(baseTheta * moneynessMultiplier / 365, 4); // Daily theta
    }

    #endregion
}