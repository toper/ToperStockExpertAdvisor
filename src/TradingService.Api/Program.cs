using NLog;
using NLog.Web;
using TradingService.Api.Configuration;
using Winton.Extensions.Configuration.Consul;

// Load environment variables from .env file
DotNetEnv.Env.Load();

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

logger.Debug("Init TradingService.Api");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure hierarchical configuration: ENV vars > Consul > appsettings.json
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    // Add Consul configuration source (with fallback if unavailable)
    var consulHost = builder.Configuration["CONSUL_HOST"]
        ?? builder.Configuration["AppSettings:Consul:Host"]
        ?? "http://localhost:8500";

    try
    {
        builder.Configuration.AddConsul(
            $"TradingService.Api/{builder.Environment.EnvironmentName}",
            options =>
            {
                options.ConsulConfigurationOptions = cco => { cco.Address = new Uri(consulHost); };
                options.Optional = true; // Don't fail if Consul is unavailable
                options.ReloadOnChange = true;
                options.OnLoadException = exceptionContext =>
                {
                    logger.Warn(exceptionContext.Exception, "Failed to load configuration from Consul, using local config");
                    exceptionContext.Ignore = true;
                };
            });

        logger.Info("Consul configuration source added: {ConsulHost}", consulHost);
    }
    catch (Exception ex)
    {
        logger.Warn(ex, "Could not configure Consul, using local configuration only");
    }

    // Override with environment variables again (highest priority)
    builder.Configuration.AddEnvironmentVariables();

    // Single port architecture (5001):
    // - /api/* -> local controllers (pass through Ocelot)
    // - /gateway/* -> external microservices via Ocelot + Consul
    // - /health, /swagger -> local endpoints (pass through Ocelot)

    // NLog
    builder.AddNlog();

    // Ocelot Gateway - handles /gateway/* routes to external microservices
    // Unmatched routes pass through to local MapControllers
    builder.AddOcelotGateway();

    // Swagger
    builder.AddSwagger();

    // Services
    builder.AddServices();

    var app = builder.Build();

    // Initialize database
    using (var scope = app.Services.CreateScope())
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<TradingService.Data.IDbContextFactory>();
        using var db = dbFactory.Create();
        await TradingService.Data.DatabaseInitializer.InitializeAsync(db);
        logger.Info("Database initialized");
    }

    // Swagger
    app.ConfigureSwagger();

    // CORS
    app.UseCors();

    // Routing - required for attribute routing in controllers
    app.UseRouting();

    // Ocelot Gateway - conditional routing
    // ONLY /gateway/* paths go through Ocelot (external microservices via Consul)
    // All other paths (/api/*, /health, /swagger) go directly to MapControllers
    app.UseOcelotGateway();

    // Authorization - currently no auth configured
    app.UseAuthorization();

    // Local endpoints - served directly when Ocelot doesn't match
    app.MapGet("/", () => Results.Redirect("/swagger"));
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health/live");

    logger.Info("Starting TradingService.Api...");
    await app.RunAsync();
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
