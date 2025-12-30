using Microsoft.AspNetCore.SignalR;
using TradingService.Api.Services;

namespace TradingService.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time scan progress updates
/// </summary>
public class ScanProgressHub : Hub
{
    private readonly ILogger<ScanProgressHub> _logger;
    private readonly ScanStateTracker _stateTracker;

    public ScanProgressHub(
        ILogger<ScanProgressHub> logger,
        ScanStateTracker stateTracker)
    {
        _logger = logger;
        _stateTracker = stateTracker;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to ScanProgressHub: {ConnectionId}", Context.ConnectionId);

        // Send current scan state to newly connected client
        var currentState = _stateTracker.GetCurrentState();
        if (currentState != null)
        {
            await Clients.Caller.SendAsync("ScanStarted", currentState);
            _logger.LogDebug("Sent current scan state to newly connected client: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected from ScanProgressHub with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected from ScanProgressHub: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join the scan listeners group to receive updates
    /// </summary>
    public async Task JoinScanListeners()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "scan-listeners");
        _logger.LogInformation("Client {ConnectionId} joined scan-listeners group", Context.ConnectionId);
    }

    /// <summary>
    /// Leave the scan listeners group
    /// </summary>
    public async Task LeaveScanListeners()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "scan-listeners");
        _logger.LogInformation("Client {ConnectionId} left scan-listeners group", Context.ConnectionId);
    }
}
