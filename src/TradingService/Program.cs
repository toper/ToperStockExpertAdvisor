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

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

logger.Debug("Init TradingService");

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
        })
        .ConfigureServices((hostContext, services) =>
        {
            // Konfiguracja
            var configuration = hostContext.Configuration;
            var appSettings = new AppSettings();
            configuration.GetSection("AppSettings").Bind(appSettings);
            services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

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

            // Repositories
            services.AddScoped<IRecommendationRepository, RecommendationRepository>();

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
