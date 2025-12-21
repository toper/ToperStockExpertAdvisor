using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;
using TradingService.Data.Entities;
using TradingService.Models;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Brokers;

/// <summary>
/// Executes PUT sell orders based on recommendations.
/// Handles position sizing, risk management, and order creation.
/// </summary>
public class OrderExecutor : IOrderExecutor
{
    private readonly IBrokerFactory _brokerFactory;
    private readonly BrokerSettings _brokerSettings;
    private readonly ILogger<OrderExecutor> _logger;

    // Margin requirement for naked PUT options (typically 20% of strike price)
    private const decimal MarginRequirementPercent = 0.20m;

    // Minimum premium to consider (avoid dust trades)
    private const decimal MinimumPremium = 0.10m;

    public OrderExecutor(
        IBrokerFactory brokerFactory,
        IOptions<AppSettings> appSettings,
        ILogger<OrderExecutor> logger)
    {
        _brokerFactory = brokerFactory;
        _brokerSettings = appSettings.Value.Broker;
        _logger = logger;
    }

    public async Task<OrderResult> ExecuteRecommendationAsync(
        PutRecommendation recommendation,
        decimal investmentAmount,
        string? brokerName = null)
    {
        var actualBroker = brokerName ?? _brokerSettings.DefaultBroker;

        _logger.LogInformation(
            "Executing recommendation: {Symbol} PUT @ ${Strike}, Expiry: {Expiry}, " +
            "Investment: ${Amount}, Broker: {Broker}",
            recommendation.Symbol,
            recommendation.StrikePrice,
            recommendation.Expiry.ToShortDateString(),
            investmentAmount,
            actualBroker);

        try
        {
            // Validate recommendation
            if (!ValidateRecommendation(recommendation))
            {
                return new OrderResult
                {
                    Success = false,
                    Message = "Recommendation validation failed"
                };
            }

            // Calculate position size
            var contracts = CalculateContracts(recommendation, investmentAmount);

            if (contracts <= 0)
            {
                _logger.LogWarning(
                    "Insufficient capital for {Symbol}: Investment ${Amount} < Margin ${Margin}",
                    recommendation.Symbol,
                    investmentAmount,
                    CalculateMarginPerContract(recommendation));

                return new OrderResult
                {
                    Success = false,
                    Message = "Insufficient capital for minimum position size"
                };
            }

            // Create order
            var order = new PutSellOrder
            {
                OptionSymbol = BuildOptionSymbol(recommendation),
                UnderlyingSymbol = recommendation.Symbol,
                Strike = recommendation.StrikePrice,
                Expiry = recommendation.Expiry,
                Quantity = contracts,
                LimitPrice = recommendation.Premium
            };

            // Get broker and execute
            var broker = _brokerFactory.CreateBroker(actualBroker);

            // Check connection
            var isConnected = await broker.IsConnectedAsync();
            if (!isConnected)
            {
                _logger.LogError("Broker {Broker} is not connected", actualBroker);
                return new OrderResult
                {
                    Success = false,
                    Message = $"Broker {actualBroker} is not connected"
                };
            }

            // Execute order
            var result = await broker.PlacePutSellOrderAsync(order);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Order executed successfully: {OrderId}, {Contracts} contracts @ ${Price}",
                    result.OrderId,
                    contracts,
                    result.FilledPrice ?? recommendation.Premium);
            }
            else
            {
                _logger.LogWarning(
                    "Order execution failed for {Symbol}: {Message}",
                    recommendation.Symbol,
                    result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute recommendation for {Symbol}",
                recommendation.Symbol);

            return new OrderResult
            {
                Success = false,
                Message = $"Execution error: {ex.Message}"
            };
        }
    }

    public int CalculateContracts(PutRecommendation recommendation, decimal investmentAmount)
    {
        var marginPerContract = CalculateMarginPerContract(recommendation);

        if (marginPerContract <= 0)
            return 0;

        var contracts = (int)(investmentAmount / marginPerContract);

        _logger.LogDebug(
            "Position sizing: Investment ${Amount} / Margin ${Margin} = {Contracts} contracts",
            investmentAmount,
            marginPerContract,
            contracts);

        return contracts;
    }

    public string BuildOptionSymbol(PutRecommendation recommendation)
    {
        // OCC option symbol format: ROOT + EXPIRY (YYMMDD) + TYPE (P/C) + STRIKE (8 digits with leading zeros)
        // Example: AAPL250117P00180000 (AAPL January 17, 2025 $180 PUT)
        var root = recommendation.Symbol.PadRight(6);
        var expiry = recommendation.Expiry.ToString("yyMMdd");
        var strike = ((int)(recommendation.StrikePrice * 1000)).ToString("D8");

        return $"{root}{expiry}P{strike}";
    }

    private decimal CalculateMarginPerContract(PutRecommendation recommendation)
    {
        // Naked PUT margin = Strike * 100 shares * Margin Requirement
        // Minus premium received (reduces margin requirement)
        var grossMargin = recommendation.StrikePrice * 100m * MarginRequirementPercent;
        var premiumReceived = recommendation.Premium * 100m;

        return grossMargin - premiumReceived;
    }

    private bool ValidateRecommendation(PutRecommendation recommendation)
    {
        // Check expiry is in the future
        if (recommendation.Expiry <= DateTime.Today)
        {
            _logger.LogWarning(
                "Recommendation {Symbol} has expired (Expiry: {Expiry})",
                recommendation.Symbol,
                recommendation.Expiry);
            return false;
        }

        // Check minimum premium
        if (recommendation.Premium < MinimumPremium)
        {
            _logger.LogWarning(
                "Recommendation {Symbol} premium too low (${Premium} < ${Min})",
                recommendation.Symbol,
                recommendation.Premium,
                MinimumPremium);
            return false;
        }

        // Check confidence level
        if (recommendation.Confidence < 0.5m)
        {
            _logger.LogWarning(
                "Recommendation {Symbol} confidence too low ({Confidence:P})",
                recommendation.Symbol,
                recommendation.Confidence);
            return false;
        }

        // Check strike is OTM (strike should be below current price for PUTs)
        if (recommendation.StrikePrice >= recommendation.CurrentPrice)
        {
            _logger.LogWarning(
                "Recommendation {Symbol} is ITM (Strike ${Strike} >= Price ${Price})",
                recommendation.Symbol,
                recommendation.StrikePrice,
                recommendation.CurrentPrice);
            return false;
        }

        return true;
    }
}
