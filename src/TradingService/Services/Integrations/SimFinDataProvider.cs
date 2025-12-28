using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingService.Configuration;

namespace TradingService.Services.Integrations;

/// <summary>
/// Service for fetching financial data from SimFin Bulk Download API
/// Downloads CSV files in ZIP format and parses them locally
/// </summary>
public class SimFinDataProvider
{
    private readonly SimFinSettings _settings;
    private readonly ILogger<SimFinDataProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _cacheDir;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly SemaphoreSlim _requestTimeLock = new(1, 1);

    // Dataset names from SimFin bulk download API
    private const string DATASET_INCOME = "income";
    private const string DATASET_BALANCE = "balance";
    private const string DATASET_CASHFLOW = "cashflow";
    private const string VARIANT_QUARTERLY = "quarterly";
    private const string MARKET_US = "us";

    public SimFinDataProvider(
        IOptions<AppSettings> appSettings,
        ILogger<SimFinDataProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = appSettings.Value.SimFin;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        // Cache directory for downloaded ZIP files and extracted CSVs
        _cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "simfin");
        Directory.CreateDirectory(_cacheDir);

        _logger.LogInformation(
            "SimFinDataProvider initialized - Cache: {CacheDir}, Rate limit: 2 requests/second (FREE account)",
            _cacheDir);
    }

    /// <summary>
    /// Ensures we don't exceed SimFin's rate limit of 2 requests/second
    /// </summary>
    private async Task RateLimitAsync()
    {
        await _requestTimeLock.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var minDelay = TimeSpan.FromMilliseconds(500); // 500ms = 2 requests/second

            if (timeSinceLastRequest < minDelay)
            {
                var delay = minDelay - timeSinceLastRequest;
                await Task.Delay(delay);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _requestTimeLock.Release();
        }
    }

    /// <summary>
    /// Fetches fundamental data for a given ticker symbol
    /// </summary>
    public async Task<SimFinCompanyData?> GetCompanyDataAsync(
        string ticker,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching SimFin data for {Ticker}", ticker);

            // Ensure we have the latest bulk data downloaded
            await EnsureBulkDataDownloadedAsync(cancellationToken);

            // Parse CSV files and extract data for the ticker
            var incomeData = await GetLatestDataFromCsvAsync<IncomeStatementCsv>(
                DATASET_INCOME, ticker, cancellationToken);
            var balanceData = await GetLatestDataFromCsvAsync<BalanceSheetCsv>(
                DATASET_BALANCE, ticker, cancellationToken);
            var cashflowData = await GetLatestDataFromCsvAsync<CashFlowCsv>(
                DATASET_CASHFLOW, ticker, cancellationToken);

            if (incomeData == null && balanceData == null)
            {
                _logger.LogWarning("No financial data found for {Ticker} in SimFin bulk data", ticker);
                return null;
            }

            return new SimFinCompanyData
            {
                CompanyInfo = new SimFinCompanyResponse
                {
                    Ticker = ticker,
                    CompanyName = ticker // CSV doesn't have company name in statements
                },
                BalanceSheet = MapBalanceSheet(balanceData),
                IncomeStatement = MapIncomeStatement(incomeData),
                CashFlow = MapCashFlow(cashflowData)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SimFin data for {Ticker}", ticker);
            return null;
        }
    }

    /// <summary>
    /// Ensures bulk data files are downloaded and not stale
    /// </summary>
    private async Task EnsureBulkDataDownloadedAsync(CancellationToken cancellationToken)
    {
        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            var refreshAfterDays = 7; // Re-download after 7 days
            var datasets = new[] { DATASET_INCOME, DATASET_BALANCE, DATASET_CASHFLOW };

            foreach (var dataset in datasets)
            {
                var csvPath = GetCsvPath(dataset);
                var needsDownload = !File.Exists(csvPath) ||
                                    (DateTime.UtcNow - File.GetLastWriteTimeUtc(csvPath)).TotalDays > refreshAfterDays;

                if (needsDownload)
                {
                    _logger.LogInformation("Downloading {Dataset} bulk data from SimFin...", dataset);
                    await DownloadAndExtractBulkDataAsync(dataset, cancellationToken);
                }
                else
                {
                    _logger.LogDebug("{Dataset} data is cached (age: {Days} days)", dataset,
                        (DateTime.UtcNow - File.GetLastWriteTimeUtc(csvPath)).TotalDays);
                }
            }
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    /// <summary>
    /// Downloads and extracts bulk data ZIP file from SimFin
    /// </summary>
    private async Task DownloadAndExtractBulkDataAsync(string dataset, CancellationToken cancellationToken)
    {
        await RateLimitAsync();

        // SimFin bulk download URL format
        var url = $"https://prod.simfin.com/api/bulk-download/s3?dataset={dataset}&variant={VARIANT_QUARTERLY}&market={MARKET_US}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"api-key {_settings.ApiKey}"); // Note: "api-key " prefix with space!
        request.Headers.Add("accept", "application/zip");

        _logger.LogDebug("Downloading: {Url}", url);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "SimFin bulk download failed: {StatusCode} - {Error}",
                response.StatusCode, errorBody);
            throw new HttpRequestException($"SimFin download failed: {response.StatusCode}");
        }

        // Download ZIP to memory
        var zipBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        _logger.LogInformation("Downloaded {Dataset} ({Size} KB)", dataset, zipBytes.Length / 1024);

        // Extract CSV from ZIP
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // Find the CSV file in the ZIP (usually named like "us-income-quarterly.csv")
        var csvEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv"));
        if (csvEntry == null)
        {
            throw new InvalidOperationException($"No CSV file found in {dataset} ZIP archive");
        }

        // Extract to cache directory
        var csvPath = GetCsvPath(dataset);
        using (var csvFile = csvEntry.Open())
        using (var fileStream = File.Create(csvPath))
        {
            await csvFile.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Extracted {Dataset} to {Path}", dataset, csvPath);
    }

    /// <summary>
    /// Gets latest data from CSV file for a specific ticker
    /// </summary>
    private async Task<T?> GetLatestDataFromCsvAsync<T>(
        string dataset,
        string ticker,
        CancellationToken cancellationToken) where T : CsvRecordBase
    {
        var csvPath = GetCsvPath(dataset);
        if (!File.Exists(csvPath))
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";", // SimFin uses semicolon delimiter
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            });

            var records = new List<T>();
            await foreach (var record in csv.GetRecordsAsync<T>(cancellationToken))
            {
                if (string.Equals(record.Ticker, ticker, StringComparison.OrdinalIgnoreCase))
                {
                    records.Add(record);
                }
            }

            // Return the most recent record (latest report date)
            return records.OrderByDescending(r => r.ReportDate).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing {Dataset} CSV for {Ticker}", dataset, ticker);
            return null;
        }
    }

    private string GetCsvPath(string dataset) =>
        Path.Combine(_cacheDir, $"{MARKET_US}-{dataset}-{VARIANT_QUARTERLY}.csv");

    private SimFinBalanceSheet? MapBalanceSheet(BalanceSheetCsv? csv)
    {
        if (csv == null) return null;

        // Calculate Total Debt = Short Term Debt + Long Term Debt
        var totalDebt = (csv.ShortTermDebt ?? 0) + (csv.LongTermDebt ?? 0);

        return new SimFinBalanceSheet
        {
            TotalAssets = csv.TotalAssets,
            TotalCash = csv.CashEquivalentsShortTermInvestments,
            TotalDebt = totalDebt > 0 ? totalDebt : null,
            CurrentAssets = csv.TotalCurrentAssets,
            CurrentLiabilities = csv.TotalCurrentLiabilities,
            TotalEquity = csv.TotalEquity
        };
    }

    private SimFinIncomeStatement? MapIncomeStatement(IncomeStatementCsv? csv)
    {
        if (csv == null) return null;

        return new SimFinIncomeStatement
        {
            Revenue = csv.Revenue,
            OperatingIncome = csv.OperatingIncome,
            NetIncome = csv.NetIncome,
            SharesOutstanding = csv.SharesBasic
        };
    }

    private SimFinCashFlow? MapCashFlow(CashFlowCsv? csv)
    {
        if (csv == null) return null;

        return new SimFinCashFlow
        {
            OperatingCashFlow = csv.NetCashFromOperatingActivities,
            InvestingCashFlow = csv.NetCashFromInvestingActivities,
            FinancingCashFlow = csv.NetCashFromFinancingActivities
        };
    }
}

// CSV Record classes for CsvHelper
public abstract class CsvRecordBase
{
    public string? Ticker { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Report Date")]
    public DateTime ReportDate { get; set; }
}

public class IncomeStatementCsv : CsvRecordBase
{
    public decimal? Revenue { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Operating Income (Loss)")]
    public decimal? OperatingIncome { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Net Income")]
    public decimal? NetIncome { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Shares (Basic)")]
    public decimal? SharesBasic { get; set; }
}

public class BalanceSheetCsv : CsvRecordBase
{
    [CsvHelper.Configuration.Attributes.Name("Total Assets")]
    public decimal? TotalAssets { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Cash, Cash Equivalents & Short Term Investments")]
    public decimal? CashEquivalentsShortTermInvestments { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Short Term Debt")]
    public decimal? ShortTermDebt { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Long Term Debt")]
    public decimal? LongTermDebt { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Total Current Assets")]
    public decimal? TotalCurrentAssets { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Total Current Liabilities")]
    public decimal? TotalCurrentLiabilities { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Total Equity")]
    public decimal? TotalEquity { get; set; }
}

public class CashFlowCsv : CsvRecordBase
{
    [CsvHelper.Configuration.Attributes.Name("Net Cash from Operating Activities")]
    public decimal? NetCashFromOperatingActivities { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Net Cash from Investing Activities")]
    public decimal? NetCashFromInvestingActivities { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Net Cash from Financing Activities")]
    public decimal? NetCashFromFinancingActivities { get; set; }
}

// DTOs for SimFin API responses (same as before)
public class SimFinCompanyData
{
    public SimFinCompanyResponse CompanyInfo { get; set; } = new();
    public SimFinBalanceSheet? BalanceSheet { get; set; }
    public SimFinIncomeStatement? IncomeStatement { get; set; }
    public SimFinCashFlow? CashFlow { get; set; }
}

public class SimFinCompanyResponse
{
    public long? SimFinId { get; set; }
    public string? Ticker { get; set; }
    public string? CompanyName { get; set; }
    public int? IndustryId { get; set; }
    public int? FiscalYearEnd { get; set; }
}

public class SimFinBalanceSheet
{
    public decimal? TotalAssets { get; set; }
    public decimal? TotalCash { get; set; }
    public decimal? TotalDebt { get; set; }
    public decimal? CurrentAssets { get; set; }
    public decimal? CurrentLiabilities { get; set; }
    public decimal? TotalEquity { get; set; }
}

public class SimFinIncomeStatement
{
    public decimal? Revenue { get; set; }
    public decimal? OperatingIncome { get; set; }
    public decimal? NetIncome { get; set; }
    public decimal? SharesOutstanding { get; set; }
}

public class SimFinCashFlow
{
    public decimal? OperatingCashFlow { get; set; }
    public decimal? InvestingCashFlow { get; set; }
    public decimal? FinancingCashFlow { get; set; }
}
