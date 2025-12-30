using Consul;
using LinqToDB.Data;
using TradingService.Api.Services;
using TradingService.Configuration;
using TradingService.Data;
using TradingService.Services;
using TradingService.Services.Brokers;
using TradingService.Services.Integrations;
using TradingService.Services.Interfaces;
using TradingService.Services.Repositories;
using TradingService.Services.Strategies;

namespace TradingService.Api.Configuration;

public static class ServicesConfiguration
{
    public static void AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();

        // Configuration with automatic hierarchy: ENV vars > Consul > appsettings.json
        // Environment variables automatically override using .NET naming convention:
        // AppSettings__Broker__Exante__ApiKey overrides AppSettings:Broker:Exante:ApiKey
        var configuration = builder.Configuration;

        // Configure with validation
        builder.Services.AddOptions<AppSettings>()
            .Bind(configuration.GetSection("AppSettings"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Get settings for database initialization
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>()
            ?? throw new InvalidOperationException("AppSettings configuration is missing");

        // Database - Linq2DB
        var connectionString = appSettings.Database.ConnectionString;
        if (DataConnection.DefaultSettings == null)
        {
            DataConnection.DefaultSettings = new DbConnectionSettings(connectionString);
        }
        builder.Services.AddSingleton<IDbContextFactory>(sp => new DbContextFactory(connectionString));

        // Repositories - changed to Singleton for use in background workers
        builder.Services.AddSingleton<IRecommendationRepository, RecommendationRepository>();
        builder.Services.AddSingleton<ICompanyFinancialRepository, CompanyFinancialRepository>();

        // Bulk Financial Data Processor
        builder.Services.AddSingleton<IBulkFinancialDataProcessor, BulkFinancialDataProcessor>();

        // Scan State Tracker (for tracking current scan progress)
        builder.Services.AddSingleton<ScanStateTracker>();

        // SignalR Progress Notifier
        builder.Services.AddSingleton<IScanProgressNotifier, SignalRScanProgressNotifier>();

        // Market Data Providers
        builder.Services.AddSingleton<IMarketDataProvider, YahooFinanceDataProvider>();
        builder.Services.AddSingleton<IOptionsDataProvider, ExanteDataProvider>();
        builder.Services.AddSingleton<IMarketDataAggregator, MarketDataAggregator>();

        // SimFin Data Provider for financial health metrics
        builder.Services.AddSingleton<ISimFinDataProvider, SimFinDataProvider>();

        // Financial Health Service
        builder.Services.AddSingleton<IFinancialHealthService, FinancialHealthService>();

        // HttpClient for services
        builder.Services.AddHttpClient();

        // Configure named HttpClient for Exante API with Polly retry policies
        var rateLimitSettings = appSettings.RateLimiting;
        builder.Services.AddHttpClient("ExanteApi", client =>
        {
            client.BaseAddress = new Uri(appSettings.Broker.Exante.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
        })
        .AddStandardResilienceHandler(options =>
        {
            // Configure timeout per attempt (default 10s is too short for Exante API)
            var attemptTimeout = TimeSpan.FromSeconds(rateLimitSettings.AttemptTimeoutSeconds);
            options.AttemptTimeout.Timeout = attemptTimeout;

            // Configure total request timeout (must be > attempt timeout)
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);

            // Configure circuit breaker sampling duration (must be >= 2 × attempt timeout)
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(attemptTimeout.TotalSeconds * 2);

            // Configure retry strategy for HTTP 429 and transient errors
            options.Retry.MaxRetryAttempts = rateLimitSettings.MaxRetries;
            options.Retry.Delay = TimeSpan.FromSeconds(rateLimitSettings.InitialRetryDelaySeconds);
            options.Retry.BackoffType = rateLimitSettings.UseExponentialBackoff
                ? Polly.DelayBackoffType.Exponential
                : Polly.DelayBackoffType.Constant;

            // Retry on HTTP 429 (Too Many Requests) and transient errors (5xx, timeouts)
            options.Retry.ShouldHandle = new Polly.PredicateBuilder<HttpResponseMessage>()
                .HandleResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .HandleResult(response => (int)response.StatusCode >= 500);
        });

        // Exante Authentication Service (JWT token generation)
        builder.Services.AddSingleton<ExanteAuthService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ExanteAuthService>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new ExanteAuthService(appSettings.Broker.Exante, logger, httpClientFactory);
        });

        // Options Discovery Service
        builder.Services.AddSingleton<IOptionsDiscoveryService, ExanteOptionsDiscoveryService>();

        // Trading Strategies
        builder.Services.AddSingleton<IStrategy, ShortTermPutStrategy>();
        builder.Services.AddSingleton<IStrategy, DividendMomentumStrategy>();
        builder.Services.AddSingleton<IStrategy, VolatilityCrushStrategy>();
        builder.Services.AddSingleton<IStrategyLoader, StrategyLoader>();

        // Daily Scan Service
        builder.Services.AddSingleton<IDailyScanService, DailyScanService>();

        // Broker Services
        builder.Services.AddSingleton<IBrokerFactory, BrokerFactory>();
        builder.Services.AddSingleton<IOrderExecutor, OrderExecutor>();

        // Background Worker for daily scans
        builder.Services.AddHostedService<ScanWorker>();

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // Consul
        builder.Services.AddSingleton<IConsulClient>(sp =>
        {
            return new ConsulClient(cfg =>
            {
                cfg.Address = new Uri(appSettings.Consul.Host);
            });
        });

        // Consul Registration (runs on startup/shutdown)
        builder.Services.AddHostedService<ConsulRegistrationService>();

        // Health Checks
        builder.Services.AddHealthChecks()
            // TODO: Re-enable SQLite health check after verifying .NET 10 compatibility
            // .AddSqlite(
            //     connectionString,
            //     name: "database",
            //     failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
            //     tags: new[] { "db", "sqlite" })
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "api" });

        // SignalR for real-time scan progress updates
        builder.Services.AddSignalR(options =>
        {
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        });
    }
}
