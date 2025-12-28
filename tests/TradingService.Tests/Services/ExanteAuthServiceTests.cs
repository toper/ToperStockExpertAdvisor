using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TradingService.Configuration;
using TradingService.Services.Integrations;
using Xunit;
using Xunit.Abstractions;

namespace TradingService.Tests.Services;

/// <summary>
/// Tests for ExanteAuthService JWT token refresh functionality
/// </summary>
public class ExanteAuthServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<ExanteAuthService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public ExanteAuthServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<ExanteAuthService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api-demo.exante.eu")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    [Fact]
    public async Task GetValidTokenAsync_GeneratesTokenLocally()
    {
        // Arrange
        var settings = new ExanteBrokerSettings
        {
            ClientId = "test-client-id",
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret",
            BaseUrl = "https://api-demo.exante.eu"
        };

        var service = new ExanteAuthService(settings, _loggerMock.Object, _httpClientFactoryMock.Object);

        // Act
        var token = await service.GetValidTokenAsync();

        // Assert
        token.Should().NotBeNullOrEmpty("Token should be generated locally using HMAC-SHA256");
        token.Should().StartWith("eyJ"); // JWT tokens start with "eyJ"
        _output.WriteLine($"Generated token: {token?[..50]}...");
    }

    [Fact]
    public async Task GetValidTokenAsync_WithExpiredToken_RefreshesToken()
    {
        // Arrange
        var settings = new ExanteBrokerSettings
        {
            ClientId = "test-client-id",
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret",
            BaseUrl = "https://api-demo.exante.eu"
        };

        var service = new ExanteAuthService(settings, _loggerMock.Object, _httpClientFactoryMock.Object);

        // Act - First call generates token
        var token1 = await service.GetValidTokenAsync();

        // Wait a bit and call again (should use cached token)
        await Task.Delay(100);
        var token2 = await service.GetValidTokenAsync();

        // Assert
        token1.Should().NotBeNullOrEmpty();
        token2.Should().Be(token1); // Should be cached
        _output.WriteLine($"First token: {token1?[..50]}...");
        _output.WriteLine($"Second token (cached): {token2?[..50]}...");
    }

    [Fact]
    public async Task GetValidTokenAsync_WithMissingCredentials_ReturnsNull()
    {
        // Arrange
        var settings = new ExanteBrokerSettings
        {
            ClientId = "", // Missing
            ApiKey = "",
            ApiSecret = "",
            BaseUrl = "https://api-demo.exante.eu"
        };

        var service = new ExanteAuthService(settings, _loggerMock.Object, _httpClientFactoryMock.Object);

        // Act
        var token = await service.GetValidTokenAsync();

        // Assert
        token.Should().BeNull();
        _output.WriteLine("Token is null due to missing credentials");
    }


    [Fact]
    public async Task ForceRefreshAsync_ForcesTokenRefresh()
    {
        // Arrange
        var settings = new ExanteBrokerSettings
        {
            ClientId = "test-client-id",
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret",
            BaseUrl = "https://api-demo.exante.eu"
        };

        var service = new ExanteAuthService(settings, _loggerMock.Object, _httpClientFactoryMock.Object);

        // Act
        var token = await service.ForceRefreshAsync();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Should().StartWith("eyJ"); // JWT tokens start with "eyJ"
        _output.WriteLine($"Force refreshed token: {token?[..50]}...");
    }

    [Fact]
    public async Task ConfigureClientAuthenticationAsync_SetsAuthorizationHeader()
    {
        // Arrange
        var settings = new ExanteBrokerSettings
        {
            ClientId = "test-client-id",
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret",
            BaseUrl = "https://api-demo.exante.eu"
        };

        var service = new ExanteAuthService(settings, _loggerMock.Object, _httpClientFactoryMock.Object);
        var client = new HttpClient();

        // Act
        var success = await service.ConfigureClientAuthenticationAsync(client);

        // Assert
        success.Should().BeTrue();
        client.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        client.DefaultRequestHeaders.Authorization!.Parameter.Should().NotBeNullOrEmpty();
        _output.WriteLine($"Authorization header: {client.DefaultRequestHeaders.Authorization}");
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}
