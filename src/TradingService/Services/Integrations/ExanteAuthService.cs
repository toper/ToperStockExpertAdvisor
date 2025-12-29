using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TradingService.Configuration;

namespace TradingService.Services.Integrations;

/// <summary>
/// Service for managing Exante API authentication with automatic JWT token refresh.
/// JWT tokens expire after 6 hours and need to be refreshed.
/// </summary>
public class ExanteAuthService
{
    private readonly ExanteBrokerSettings _settings;
    private readonly ILogger<ExanteAuthService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);

    private string? _currentJwtToken;
    private DateTime _tokenExpiresAt;
    private const int TokenValidityHours = 6;
    private const int RefreshBufferMinutes = 30; // Refresh 30 minutes before expiry

    public ExanteAuthService(
        ExanteBrokerSettings settings,
        ILogger<ExanteAuthService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _logger = logger;

        // Use named HttpClient with Polly retry policies
        _httpClient = httpClientFactory.CreateClient("ExanteApi");
    }

    /// <summary>
    /// Gets a valid JWT token, refreshing it if necessary.
    /// Token is always generated locally and never loaded from configuration.
    /// </summary>
    public async Task<string?> GetValidTokenAsync(CancellationToken cancellationToken = default)
    {
        // Check if current token is still valid
        if (!string.IsNullOrEmpty(_currentJwtToken) &&
            DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-RefreshBufferMinutes))
        {
            _logger.LogDebug("Using cached JWT token (expires in {Minutes} minutes)",
                (_tokenExpiresAt - DateTime.UtcNow).TotalMinutes);
            return _currentJwtToken;
        }

        // Token expired or about to expire - refresh it
        await _tokenRefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock (another thread might have refreshed)
            if (!string.IsNullOrEmpty(_currentJwtToken) &&
                DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-RefreshBufferMinutes))
            {
                return _currentJwtToken;
            }

            _logger.LogInformation("JWT token expired or missing - requesting new token from Exante API");
            return await RefreshTokenAsync(cancellationToken);
        }
        finally
        {
            _tokenRefreshLock.Release();
        }
    }

    /// <summary>
    /// Generates a new JWT token locally using HMAC-SHA256 signing.
    /// Exante requires client-side token generation (not via /auth endpoint).
    /// </summary>
    private Task<string?> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.ClientId) ||
            string.IsNullOrEmpty(_settings.ApiKey) ||
            string.IsNullOrEmpty(_settings.ApiSecret))
        {
            _logger.LogWarning("Missing Exante credentials (ClientId, ApiKey, or ApiSecret) - cannot generate JWT token");
            return Task.FromResult<string?>(null);
        }

        try
        {
            _logger.LogDebug("Generating JWT token using HMAC-SHA256");

            // Exante JWT structure according to their documentation
            // https://developers.exante.eu/tutorials/auth-basics/
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_settings.ApiSecret);
            var now = DateTime.UtcNow;
            var expires = now.AddHours(TokenValidityHours);

            // Build claims including 'aud' as array (Exante requires array of scopes)
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, _settings.ApiKey), // Application ID
                new Claim(JwtRegisteredClaimNames.Iat, ((DateTimeOffset)now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            };

            // Add audience claims as array (required by Exante)
            var scopes = new[] { "symbols", "feed", "change", "ohlc", "crossrates", "summary", "accounts", "orders", "transactions" };
            foreach (var scope in scopes)
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Aud, scope));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _settings.ClientId,  // Client ID (iss claim)
                Subject = new ClaimsIdentity(claims),
                IssuedAt = now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            _currentJwtToken = tokenString;
            _tokenExpiresAt = expires;

            _logger.LogInformation(
                "Successfully generated JWT token (valid until {ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC)",
                _tokenExpiresAt);
            _logger.LogDebug("Token preview: {TokenPreview}...", tokenString[..Math.Min(50, tokenString.Length)]);

            return Task.FromResult<string?>(tokenString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating JWT token with HMAC-SHA256");
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Configures HttpClient with current authentication (JWT or Basic Auth fallback).
    /// </summary>
    public async Task<bool> ConfigureClientAuthenticationAsync(
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        // Try to get JWT token first (preferred method for market data)
        var jwtToken = await GetValidTokenAsync(cancellationToken);

        if (!string.IsNullOrEmpty(jwtToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            _logger.LogDebug("Configured HttpClient with JWT Bearer authentication");
            return true;
        }

        // Fallback to Basic Auth if JWT is not available
        if (!string.IsNullOrEmpty(_settings.ApiKey) && !string.IsNullOrEmpty(_settings.ApiSecret))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.ApiSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            _logger.LogDebug("Configured HttpClient with Basic authentication (JWT not available)");
            return true;
        }

        _logger.LogWarning("No authentication credentials available");
        return false;
    }

    /// <summary>
    /// Forces immediate token refresh.
    /// </summary>
    public async Task<string?> ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Forcing JWT token refresh");
        _currentJwtToken = null;
        _tokenExpiresAt = DateTime.MinValue;
        return await GetValidTokenAsync(cancellationToken);
    }

    // Request/Response DTOs for Exante /auth endpoint
    private class ExanteAuthRequest
    {
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("applicationId")]
        public string ApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("sharedKey")]
        public string SharedKey { get; set; } = string.Empty;
    }

    private class ExanteAuthResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }
    }
}
