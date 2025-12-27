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

// Load environment variables from .env file
DotNetEnv.Env.Load();

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

            // Data Providers
            services.AddSingleton<IMarketDataProvider, YahooFinanceDataProvider>();
            services.AddSingleton<IOptionsDataProvider, ExanteDataProvider>();
            services.AddSingleton<IMarketDataAggregator, MarketDataAggregator>();

            // Options Discovery
            services.AddSingleton<IOptionsDiscoveryService, ExanteOptionsDiscoveryService>();

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
