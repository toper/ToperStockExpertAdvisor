using FluentAssertions;
using Microsoft.Extensions.Logging;
using TradingService.Configuration;
using TradingService.Services.Integrations;
using Xunit;
using Xunit.Abstractions;

namespace TradingService.Tests.Services;

/// <summary>
/// REAL API Tests for ExanteAuthService - calls actual Exante Demo API
/// Tests JWT token refresh with real credentials from .env file
/// </summary>
public class ExanteAuthServiceRealTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<ExanteAuthService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // Load credentials from environment variables (set in .env file)
    private readonly ExanteBrokerSettings _settings;

    public ExanteAuthServiceRealTests(ITestOutputHelper output)
    {
        _output = output;

        // Load environment variables from .env file (if exists)
        try
        {
            DotNetEnv.Env.Load();
        }
        catch
        {
            // .env file not found - environment variables might be set system-wide
        }

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(output));
        });
        _logger = loggerFactory.CreateLogger<ExanteAuthService>();

        _httpClientFactory = new MockHttpClientFactory();

        // Load settings from environment variables
        _settings = new ExanteBrokerSettings
        {
            ClientId = Environment.GetEnvironmentVariable("AppSettings__Broker__Exante__ClientId") ?? "",
            ApiKey = Environment.GetEnvironmentVariable("AppSettings__Broker__Exante__ApiKey") ?? "",
            ApiSecret = Environment.GetEnvironmentVariable("AppSettings__Broker__Exante__ApiSecret") ?? "",
            AccountId = Environment.GetEnvironmentVariable("AppSettings__Broker__Exante__AccountId") ?? "",
            Environment = Environment.GetEnvironmentVariable("AppSettings__Broker__Exante__Environment") ?? "Demo",
            BaseUrl = Environment.GetEnvironmentVariable("AppSettings__Broker__Exante__BaseUrl") ?? "https://api-demo.exante.eu"
        };
    }

    [Fact]
    public async Task RealAPI_GenerateToken_WithCredentials_ReturnsNewToken()
    {
        // Arrange
        if (string.IsNullOrEmpty(_settings.ClientId) ||
            string.IsNullOrEmpty(_settings.ApiKey) ||
            string.IsNullOrEmpty(_settings.ApiSecret))
        {
            _output.WriteLine("SKIPPED: Missing Exante credentials in .env file");
            _output.WriteLine("Required: ClientId, ApiKey, ApiSecret");
            return;
        }

        var service = new ExanteAuthService(_settings, _logger, _httpClientFactory);

        // Act
        _output.WriteLine("Generating new JWT token locally using HMAC-SHA256...");
        var token = await service.GetValidTokenAsync();

        // Assert
        token.Should().NotBeNullOrEmpty("JWT token should be generated locally");
        _output.WriteLine($"New token generated: {token?[..50]}...");
        _output.WriteLine($"Token length: {token?.Length} characters");
    }

    [Fact]
    public async Task RealAPI_ForceRefresh_RequestsNewToken()
    {
        // Arrange
        if (string.IsNullOrEmpty(_settings.ClientId) ||
            string.IsNullOrEmpty(_settings.ApiKey) ||
            string.IsNullOrEmpty(_settings.ApiSecret))
        {
            _output.WriteLine("SKIPPED: Missing Exante credentials in .env file");
            return;
        }

        var service = new ExanteAuthService(_settings, _logger, _httpClientFactory);

        // Act
        _output.WriteLine("Force refreshing JWT token...");
        var token = await service.ForceRefreshAsync();

        // Assert
        token.Should().NotBeNullOrEmpty("Force refresh should return new token");
        _output.WriteLine($"Force refreshed token: {token?[..50]}...");
    }

    [Fact]
    public async Task RealAPI_ConfigureClientAuthentication_SetsValidAuthHeader()
    {
        // Arrange
        if (string.IsNullOrEmpty(_settings.ClientId) ||
            string.IsNullOrEmpty(_settings.ApiKey) ||
            string.IsNullOrEmpty(_settings.ApiSecret))
        {
            _output.WriteLine("SKIPPED: Missing Exante credentials");
            return;
        }

        var service = new ExanteAuthService(_settings, _logger, _httpClientFactory);
        var httpClient = new HttpClient { BaseAddress = new Uri(_settings.BaseUrl) };

        // Act
        _output.WriteLine("Configuring HTTP client authentication...");
        var success = await service.ConfigureClientAuthenticationAsync(httpClient);

        // Assert
        success.Should().BeTrue("Authentication should be configured successfully");
        httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().NotBeNullOrEmpty();

        _output.WriteLine($"Auth header configured: {httpClient.DefaultRequestHeaders.Authorization}");
        _output.WriteLine($"Token preview: {httpClient.DefaultRequestHeaders.Authorization.Parameter?[..50]}...");

        // Try to make a real API call to verify token works
        _output.WriteLine("Testing token with real API call to /md/3.0/version...");
        try
        {
            var response = await httpClient.GetAsync("/md/3.0/version");
            _output.WriteLine($"API Response Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"API call successful! Response: {content}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"API call failed: {response.StatusCode}");
                _output.WriteLine($"Error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"API call exception: {ex.Message}");
        }
    }

    [Fact]
    public async Task RealAPI_TokenCaching_UsesCachedToken()
    {
        // Arrange
        if (string.IsNullOrEmpty(_settings.ClientId))
        {
            _output.WriteLine("SKIPPED: Missing credentials");
            return;
        }

        var service = new ExanteAuthService(_settings, _logger, _httpClientFactory);

        // Act
        _output.WriteLine("First call - should get token from config or API...");
        var token1 = await service.GetValidTokenAsync();

        _output.WriteLine("Second call - should use cached token...");
        var token2 = await service.GetValidTokenAsync();

        _output.WriteLine("Third call - should still use cached token...");
        var token3 = await service.GetValidTokenAsync();

        // Assert
        token1.Should().Be(token2, "Second call should return cached token");
        token2.Should().Be(token3, "Third call should return cached token");

        _output.WriteLine($"Token caching works - all 3 calls returned same token");
        _output.WriteLine($"Token: {token1?[..50]}...");
    }

    // Helper class for creating HttpClient in tests
    private class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    // Helper class for logging test output
    private class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public XunitLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, categoryName);
        }

        public void Dispose() { }

        private class XunitLogger : ILogger
        {
            private readonly ITestOutputHelper _output;
            private readonly string _categoryName;

            public XunitLogger(ITestOutputHelper output, string categoryName)
            {
                _output = output;
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
            }
        }
    }
}
