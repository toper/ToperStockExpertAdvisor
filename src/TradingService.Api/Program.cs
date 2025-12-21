using NLog;
using NLog.Web;
using TradingService.Api.Configuration;

var logger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

logger.Debug("Init TradingService.Api");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // NLog
    builder.AddNlog();

    // Ocelot Gateway
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

    app.UseRouting();
    app.UseAuthorization();

    // Map controllers
    app.MapControllers();

    // Map health checks
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health/live");

    // Use Ocelot middleware
    await app.UseOcelotGateway();

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
