using TradingService.Data.Entities;
using TradingService.Models;

namespace TradingService.Services.Interfaces;

/// <summary>
/// Executes trading orders based on PUT recommendations.
/// Handles position sizing, order creation, and execution.
/// </summary>
public interface IOrderExecutor
{
    /// <summary>
    /// Executes a PUT sell order based on a recommendation.
    /// </summary>
    /// <param name="recommendation">The PUT recommendation to execute</param>
    /// <param name="investmentAmount">Maximum capital to allocate for this trade</param>
    /// <param name="brokerName">Optional broker name (uses default if not specified)</param>
    /// <returns>Order execution result</returns>
    Task<OrderResult> ExecuteRecommendationAsync(
        PutRecommendation recommendation,
        decimal investmentAmount,
        string? brokerName = null);

    /// <summary>
    /// Calculates the number of contracts based on investment amount and margin requirements.
    /// </summary>
    int CalculateContracts(PutRecommendation recommendation, decimal investmentAmount);

    /// <summary>
    /// Builds the option symbol for a given recommendation.
    /// </summary>
    string BuildOptionSymbol(PutRecommendation recommendation);
}
