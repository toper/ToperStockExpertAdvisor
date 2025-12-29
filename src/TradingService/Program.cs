using NLog;
using NLog.Web;
using TradingService;
using TradingService.Configuration;
using TradingService.Data;
using TradingService.Services;
using TradingService.Services.Integrations;
using TradingService.Services.Interfaces;
using TradingService.Services.Repositories;
using TradingService.Services.Strategies;
using TradingService.Services.Brokers;
using LinqToDB.Data;
using Winton.Extensions.Configuration.Consul;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

// Load environment variables from .env file
// Search for .env file in multiple locations (from most specific to most general)
var envPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),                           // Current working directory
    Path.Combine(Directory.GetCurrentDirectory(), "../../.env"),                     // Project root (when running from src/TradingService)
    Path.Combine(AppContext.BaseDirectory, ".env"),                                  // Binary directory
    Path.Combine(AppContext.BaseDirectory, "../../.env"),                            // Project root (when running from bin/Debug)
    Path.Combine(AppContext.BaseDirectory, "../../../.env"),                         // Project root (when running from bin/Debug/net10.0)
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.env")) // Project root (when running from bin/Debug/net10.0)
};

var envLoaded = false;
foreach (var envPath in envPaths)
{
    if (File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
        Console.WriteLine($"Loaded .env from: {envPath}");
        envLoaded = true;
        break;
    }
}

if (!envLoaded)
{
    Console.WriteLine("Warning: .env file not found in any expected location. Using system environment variables only.");
}

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

logger.Debug("Init TradingService");

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            // 1. Base configuration from appsettings.json
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);

            // 2. Environment variables (from .env file and system)
            config.AddEnvironmentVariables();

            // 3. Build temporary configuration to get Consul settings
            var tempConfig = config.Build();
            var consulHost = tempConfig["CONSUL_HOST"] ?? tempConfig["AppSettings:Consul:Host"] ?? "http://localhost:8500";

            // 4. Add Consul configuration source (with fallback if Consul is unavailable)
            try
            {
                config.AddConsul(
                    $"TradingService/{hostContext.HostingEnvironment.EnvironmentName}",
                    options =>
                    {
                        options.ConsulConfigurationOptions = cco => { cco.Address = new Uri(consulHost); };
                        options.Optional = true; // Don't fail if Consul is unavailable
                        options.ReloadOnChange = true;
                        options.OnLoadException = exceptionContext =>
                        {
                            logger.Warn(exceptionContext.Exception, "Failed to load configuration from Consul, using local config");
                            exceptionContext.Ignore = true; // Continue with local configuration
                        };
                    });

                logger.Info("Consul configuration source added: {ConsulHost}", consulHost);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Could not configure Consul, using local configuration only");
            }

            // 5. Override with environment variables again (highest priority)
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((hostContext, services) =>
        {
            // Configuration with automatic hierarchy: ENV vars > Consul > appsettings.json
            // Environment variables automatically override using .NET naming convention:
            // AppSettings__Broker__Exante__ApiKey overrides AppSettings:Broker:Exante:ApiKey
            var configuration = hostContext.Configuration;

            // Configure with validation
            services.AddOptions<AppSettings>()
                .Bind(configuration.GetSection("AppSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Get settings for logging and database initialization
            var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>()
                ?? throw new InvalidOperationException("AppSettings configuration is missing");

            logger.Info("Configuration loaded with hierarchy: ENV vars > Consul ({ConsulHost}) > appsettings.json",
                configuration["CONSUL_HOST"] ?? appSettings.Consul.Host);

            // Database - Linq2DB
            var connectionString = appSettings.Database.ConnectionString;
            if (DataConnection.DefaultSettings == null)
            {
                DataConnection.DefaultSettings = new DbConnectionSettings(connectionString);
            }
            services.AddSingleton<IDbContextFactory>(sp => new DbContextFactory(connectionString));

            // HTTP Client Factory with Polly retry policy
            services.AddHttpClient();

            // Configure Polly retry policy for Exante API
            var rateLimitSettings = appSettings.RateLimiting;

            services.AddHttpClient("ExanteApi", client =>
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
                // Allow enough time for all retry attempts (60s × 3 retries + buffers)
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);

                // Configure circuit breaker sampling duration (must be >= 2 × attempt timeout)
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(attemptTimeout.TotalSeconds * 2);

                // Configure retry strategy for HTTP 429 and transient errors
                options.Retry.MaxRetryAttempts = rateLimitSettings.MaxRetries;
                options.Retry.Delay = TimeSpan.FromSeconds(rateLimitSettings.InitialRetryDelaySeconds);
                options.Retry.BackoffType = rateLimitSettings.UseExponentialBackoff
                    ? Polly.DelayBackoffType.Exponential
                    : Polly.DelayBackoffType.Constant;

                options.Retry.OnRetry = args =>
                {
                    var attempt = args.AttemptNumber + 1;
                    var delay = args.RetryDelay;

                    if (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        var retryAfter = args.Outcome.Result.Headers.RetryAfter?.Delta ?? delay;
                        logger.Warn(
                            "HTTP 429 (Too Many Requests) - Retry {0}/{1} after {2}s",
                            attempt, rateLimitSettings.MaxRetries, retryAfter.TotalSeconds);
                    }
                    else if (args.Outcome.Exception != null)
                    {
                        logger.Warn(
                            "HTTP error - Retry {0}/{1} after {2}s: {3}",
                            attempt, rateLimitSettings.MaxRetries, delay.TotalSeconds,
                            args.Outcome.Exception.Message);
                    }

                    return default;
                };
            });

            logger.Info("Configured Polly retry policy: Enabled={0}, MaxRetries={1}, InitialDelay={2}s, Exponential={3}, AttemptTimeout={4}s",
                rateLimitSettings.EnableRetryOn429,
                rateLimitSettings.MaxRetries,
                rateLimitSettings.InitialRetryDelaySeconds,
                rateLimitSettings.UseExponentialBackoff,
                rateLimitSettings.AttemptTimeoutSeconds);

            // Exante Authentication Service (manages JWT token refresh)
            services.AddSingleton(sp =>
            {
                var serviceLogger = sp.GetRequiredService<ILogger<ExanteAuthService>>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new ExanteAuthService(appSettings.Broker.Exante, serviceLogger, httpClientFactory);
            });

            // Data Providers
            services.AddSingleton<IMarketDataProvider, YahooFinanceDataProvider>();
            services.AddSingleton<IOptionsDataProvider, ExanteDataProvider>();
            services.AddSingleton<IMarketDataAggregator, MarketDataAggregator>();

            // SimFin Data Provider
            services.AddSingleton<ISimFinDataProvider, SimFinDataProvider>();

            // Financial Health Service
            services.AddSingleton<IFinancialHealthService, FinancialHealthService>();

            // Options Discovery (with ExanteAuthService dependency)
            services.AddSingleton<IOptionsDiscoveryService>(sp =>
            {
                var serviceAppSettings = sp.GetRequiredService<IOptions<AppSettings>>();
                var serviceLogger = sp.GetRequiredService<ILogger<ExanteOptionsDiscoveryService>>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var authService = sp.GetRequiredService<ExanteAuthService>();
                return new ExanteOptionsDiscoveryService(serviceAppSettings, serviceLogger, httpClientFactory, authService);
            });

            // Repositories
            services.AddSingleton<IRecommendationRepository, RecommendationRepository>();

            // Strategies (register all strategies)
            services.AddSingleton<IStrategy, ShortTermPutStrategy>();
            services.AddSingleton<IStrategy, DividendMomentumStrategy>();
            services.AddSingleton<IStrategy, VolatilityCrushStrategy>();

            // Strategy Loader
            services.AddSingleton<IStrategyLoader, StrategyLoader>();

            // Core Services
            services.AddSingleton<IDailyScanService, DailyScanService>();

            // Broker Services
            services.AddSingleton<IBrokerFactory, BrokerFactory>();
            services.AddSingleton<IOrderExecutor, OrderExecutor>();

            // Worker
            services.AddHostedService<Worker>();
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        })
        .UseNLog()
        .UseWindowsService(options =>
        {
            options.ServiceName = "TradingService";
        })
        .Build();

    // Inicjalizacja bazy danych
    using (var scope = host.Services.CreateScope())
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        using var db = dbFactory.Create();
        await DatabaseInitializer.InitializeAsync(db);
        logger.Info("Database initialized successfully");
    }

    logger.Info("Starting TradingService...");
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "Stopped program because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}
