using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingService.Configuration;
using TradingService.Models;
using TradingService.Services.Interfaces;
using TradingService.Services.Integrations;

namespace TradingService.Services.Brokers;

/// <summary>
/// Exante broker implementation for executing PUT option trades.
/// Supports both demo and live environments with automatic JWT token refresh.
/// When API credentials are not configured, operates in simulation mode.
/// </summary>
public class ExanteBroker : IBroker
{
    private readonly ExanteBrokerSettings _settings;
    private readonly ILogger<ExanteBroker> _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _isSimulationMode;
    private readonly ExanteAuthService? _authService;

    public string Name => "Exante";

    public ExanteBroker(
        ExanteBrokerSettings settings,
        ILogger<ExanteBroker> logger,
        ExanteAuthService? authService = null)
    {
        _settings = settings;
        _logger = logger;
        _authService = authService;
        _isSimulationMode = string.IsNullOrEmpty(settings.ApiKey);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };

        _logger.LogInformation(
            "ExanteBroker initialized - Mode: {Mode}, Environment: {Env}, Auth: {Auth}",
            _isSimulationMode ? "Simulation" : "Live",
            settings.Environment,
            _authService != null ? "Managed" : "Manual");
    }

    private async Task ConfigureAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        if (_isSimulationMode)
            return;

        // Use ExanteAuthService for automatic token management if available
        if (_authService != null)
        {
            var success = await _authService.ConfigureClientAuthenticationAsync(_httpClient, cancellationToken);
            if (!success)
            {
                _logger.LogWarning("Failed to configure authentication via ExanteAuthService");
            }
        }
        else
        {
            // Fallback to manual Basic Auth (legacy)
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.ApiSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> IsConnectedAsync()
    {
        if (_isSimulationMode)
        {
            _logger.LogDebug("Simulation mode - returning connected");
            return true;
        }

        try
        {
            await ConfigureAuthenticationAsync();
            var response = await _httpClient.GetAsync("/md/3.0/version");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check Exante connection");
            return false;
        }
    }

    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        if (_isSimulationMode)
        {
            return new AccountInfo
            {
                AccountId = "SIMULATION",
                Balance = 100000m,
                AvailableMargin = 80000m,
                UsedMargin = 20000m,
                Currency = "USD"
            };
        }

        try
        {
            await ConfigureAuthenticationAsync();
            var response = await _httpClient.GetAsync($"/md/3.0/accounts/{_settings.AccountId}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var accountData = JsonSerializer.Deserialize<ExanteAccountResponse>(content);

            return new AccountInfo
            {
                AccountId = _settings.AccountId,
                Balance = accountData?.Balance ?? 0m,
                AvailableMargin = accountData?.AvailableMargin ?? 0m,
                UsedMargin = accountData?.UsedMargin ?? 0m,
                Currency = accountData?.Currency ?? "USD"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account info from Exante");
            throw;
        }
    }

    public async Task<OrderResult> PlacePutSellOrderAsync(PutSellOrder order)
    {
        _logger.LogInformation(
            "Placing PUT sell order: {Symbol} @ ${Strike}, Qty: {Qty}, Limit: ${Limit}",
            order.UnderlyingSymbol, order.Strike, order.Quantity, order.LimitPrice);

        if (_isSimulationMode)
        {
            return SimulatePutSellOrder(order);
        }

        try
        {
            await ConfigureAuthenticationAsync();

            var optionSymbol = BuildExanteOptionSymbol(order);
            var orderRequest = new ExanteOrderRequest
            {
                AccountId = _settings.AccountId,
                SymbolId = optionSymbol,
                Side = "sell",
                Quantity = order.Quantity.ToString(),
                OrderType = "limit",
                LimitPrice = order.LimitPrice.ToString("F2"),
                Duration = "day"
            };

            var json = JsonSerializer.Serialize(orderRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/trade/3.0/orders", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var orderResponse = JsonSerializer.Deserialize<ExanteOrderResponse>(responseContent);

                _logger.LogInformation(
                    "Order placed successfully: {OrderId}, Status: {Status}",
                    orderResponse?.OrderId, orderResponse?.Status);

                return new OrderResult
                {
                    Success = true,
                    OrderId = orderResponse?.OrderId,
                    Message = $"Order placed: {orderResponse?.Status}",
                    FilledPrice = orderResponse?.AvgPrice,
                    FilledQuantity = orderResponse?.FilledQuantity
                };
            }
            else
            {
                _logger.LogWarning("Order rejected: {Response}", responseContent);
                return new OrderResult
                {
                    Success = false,
                    Message = $"Order rejected: {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place PUT sell order");
            return new OrderResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<IEnumerable<Position>> GetPositionsAsync()
    {
        if (_isSimulationMode)
        {
            return [];
        }

        try
        {
            var response = await _httpClient.GetAsync($"/md/3.0/accounts/{_settings.AccountId}/positions");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var positions = JsonSerializer.Deserialize<List<ExantePositionResponse>>(content) ?? [];

            return positions.Select(p => new Position
            {
                PositionId = p.Id ?? string.Empty,
                Symbol = p.SymbolId ?? string.Empty,
                Quantity = p.Quantity,
                AveragePrice = p.AvgPrice,
                CurrentPrice = p.CurrentPrice,
                UnrealizedPnL = p.Pnl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get positions from Exante");
            return [];
        }
    }

    public async Task<OrderResult> ClosePositionAsync(string positionId)
    {
        _logger.LogInformation("Closing position: {PositionId}", positionId);

        if (_isSimulationMode)
        {
            return new OrderResult
            {
                Success = true,
                OrderId = $"SIM-CLOSE-{Guid.NewGuid():N}",
                Message = "Position closed (simulation)"
            };
        }

        try
        {
            var response = await _httpClient.DeleteAsync($"/trade/3.0/positions/{positionId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Position closed successfully: {PositionId}", positionId);
                return new OrderResult
                {
                    Success = true,
                    OrderId = positionId,
                    Message = "Position closed"
                };
            }
            else
            {
                _logger.LogWarning("Failed to close position: {Response}", responseContent);
                return new OrderResult
                {
                    Success = false,
                    Message = $"Failed to close: {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close position {PositionId}", positionId);
            return new OrderResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private OrderResult SimulatePutSellOrder(PutSellOrder order)
    {
        var orderId = $"SIM-{Guid.NewGuid():N}";

        _logger.LogInformation(
            "[SIMULATION] PUT sell order executed: {OrderId}, {Symbol} @ ${Strike}",
            orderId, order.UnderlyingSymbol, order.Strike);

        return new OrderResult
        {
            Success = true,
            OrderId = orderId,
            Message = "Order simulated successfully",
            FilledPrice = order.LimitPrice,
            FilledQuantity = order.Quantity
        };
    }

    private string BuildExanteOptionSymbol(PutSellOrder order)
    {
        // Exante option symbol format: UNDERLYING.EXCHANGE_YYMMDD_P_STRIKE
        // Example: AAPL.NASDAQ_250117_P_180
        var expiryStr = order.Expiry.ToString("yyMMdd");
        return $"{order.UnderlyingSymbol}.NASDAQ_{expiryStr}_P_{order.Strike:0}";
    }

    #region Exante API Response Models

    private class ExanteAccountResponse
    {
        public decimal Balance { get; set; }
        public decimal AvailableMargin { get; set; }
        public decimal UsedMargin { get; set; }
        public string Currency { get; set; } = "USD";
    }

    private class ExanteOrderRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public string SymbolId { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string LimitPrice { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
    }

    private class ExanteOrderResponse
    {
        public string? OrderId { get; set; }
        public string? Status { get; set; }
        public decimal? AvgPrice { get; set; }
        public int? FilledQuantity { get; set; }
    }

    private class ExantePositionResponse
    {
        public string? Id { get; set; }
        public string? SymbolId { get; set; }
        public int Quantity { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal Pnl { get; set; }
    }

    #endregion
}
