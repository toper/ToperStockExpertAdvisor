using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TradingService.Configuration;

namespace TradingService.Tests.Services;

/// <summary>
/// Diagnostic tests to understand Exante API responses
/// </summary>
[Trait("Category", "Diagnostic")]
public class ExanteApiDiagnosticTests
{
    private readonly AppSettings _appSettings;
    private readonly HttpClient _httpClient;

    public ExanteApiDiagnosticTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .Build();

        _appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(_appSettings);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_appSettings.Broker.Exante.BaseUrl)
        };

        // Use Basic Auth for diagnostic tests (JWT tokens are generated via ExanteAuthService in production)
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{_appSettings.Broker.Exante.ApiKey}:{_appSettings.Broker.Exante.ApiSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    [Fact]
    public async Task DiagnoseExanteAPI_TestConnection()
    {
        // Test basic connectivity
        Console.WriteLine("=== Testing Exante API Connection ===");
        Console.WriteLine($"Base URL: {_appSettings.Broker.Exante.BaseUrl}");
        Console.WriteLine($"API Key: {_appSettings.Broker.Exante.ApiKey?.Substring(0, 8)}...");
        Console.WriteLine();

        try
        {
            var response = await _httpClient.GetAsync("/md/3.0/version");
            Console.WriteLine($"GET /md/3.0/version");
            Console.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response: {content}");
            }
            else
            {
                Console.WriteLine($"Error: {response.ReasonPhrase}");
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error details: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    [Fact]
    public async Task DiagnoseExanteAPI_FetchOptionTypes()
    {
        // Test fetching all option instruments
        Console.WriteLine("=== Fetching Option Types ===");

        try
        {
            var response = await _httpClient.GetAsync("/md/3.0/types/OPTION");
            Console.WriteLine($"GET /md/3.0/types/OPTION");
            Console.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response length: {content.Length} characters");
                Console.WriteLine($"First 1000 chars: {content.Substring(0, Math.Min(1000, content.Length))}");

                // Try to parse as JSON array
                try
                {
                    var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine($"Parsed as array with {doc.RootElement.GetArrayLength()} elements");

                        if (doc.RootElement.GetArrayLength() > 0)
                        {
                            Console.WriteLine("\nFirst 5 items:");
                            foreach (var item in doc.RootElement.EnumerateArray().Take(5))
                            {
                                Console.WriteLine(item.GetRawText());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"JSON parsing error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Error: {response.ReasonPhrase}");
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error details: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    [Fact]
    public async Task DiagnoseExanteAPI_FetchSymbols()
    {
        // Test fetching all symbols
        Console.WriteLine("=== Fetching All Symbols ===");

        try
        {
            var response = await _httpClient.GetAsync("/md/3.0/symbols");
            Console.WriteLine($"GET /md/3.0/symbols");
            Console.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response length: {content.Length} characters");
                Console.WriteLine($"First 2000 chars: {content.Substring(0, Math.Min(2000, content.Length))}");

                // Try to parse and filter for options
                try
                {
                    var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var optionCount = 0;
                        Console.WriteLine("\nSearching for option symbols (containing 'AAPL', 'MSFT', or 'SPY')...");

                        foreach (var item in doc.RootElement.EnumerateArray().Take(10000))
                        {
                            var symbolId = item.TryGetProperty("symbolId", out var sid) ? sid.GetString() : null;
                            var symbol = item.TryGetProperty("symbol", out var s) ? s.GetString() : null;
                            var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;

                            var id = symbolId ?? symbol;
                            if (id != null && (id.StartsWith("AAPL") || id.StartsWith("MSFT") || id.StartsWith("SPY")))
                            {
                                Console.WriteLine($"  {id} (type: {type})");
                                optionCount++;
                                if (optionCount >= 10) break;
                            }
                        }

                        if (optionCount == 0)
                        {
                            Console.WriteLine("No options found for AAPL, MSFT, or SPY");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"JSON parsing error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Error: {response.ReasonPhrase}");
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error details: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }
}
