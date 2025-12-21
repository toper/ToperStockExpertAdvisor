using Consul;
using Microsoft.Extensions.Options;
using TradingService.Configuration;

namespace TradingService.Api.Services;

/// <summary>
/// Hosted service that registers/deregisters the API with Consul on startup/shutdown.
/// Enables service discovery for the API gateway and other services.
/// </summary>
public class ConsulRegistrationService : IHostedService
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulSettings _consulSettings;
    private readonly ILogger<ConsulRegistrationService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private string? _registrationId;

    public ConsulRegistrationService(
        IConsulClient consulClient,
        IOptions<AppSettings> appSettings,
        ILogger<ConsulRegistrationService> logger,
        IHostApplicationLifetime lifetime)
    {
        _consulClient = consulClient;
        _consulSettings = appSettings.Value.Consul;
        _logger = logger;
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _registrationId = $"{_consulSettings.ServiceName}-{Guid.NewGuid():N}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = _consulSettings.ServiceName,
            Address = GetHostAddress(),
            Port = _consulSettings.ServicePort,
            Tags = ["api", "trading", "v1"],
            Check = new AgentServiceCheck
            {
                HTTP = $"http://localhost:{_consulSettings.ServicePort}/health",
                Interval = TimeSpan.FromSeconds(30),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(5)
            }
        };

        try
        {
            await _consulClient.Agent.ServiceRegister(registration, cancellationToken);
            _logger.LogInformation(
                "Registered service with Consul: {ServiceName} (ID: {Id}) at {Address}:{Port}",
                _consulSettings.ServiceName,
                _registrationId,
                registration.Address,
                registration.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to register with Consul - service discovery may not work. " +
                "Ensure Consul is running at {Host}",
                _consulSettings.Host);
        }

        // Register deregistration on application stopping
        _lifetime.ApplicationStopping.Register(() =>
        {
            DeregisterAsync().GetAwaiter().GetResult();
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DeregisterAsync();
    }

    private async Task DeregisterAsync()
    {
        if (string.IsNullOrEmpty(_registrationId))
            return;

        try
        {
            await _consulClient.Agent.ServiceDeregister(_registrationId);
            _logger.LogInformation(
                "Deregistered service from Consul: {ServiceName} (ID: {Id})",
                _consulSettings.ServiceName,
                _registrationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deregister from Consul");
        }
    }

    private string GetHostAddress()
    {
        // In container environments, use hostname
        // For local development, use localhost
        var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
        if (!string.IsNullOrEmpty(hostname))
            return hostname;

        return "localhost";
    }
}
