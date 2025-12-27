using Consul;
using LinqToDB.Data;
using TradingService.Api.Services;
using TradingService.Configuration;
using TradingService.Data;
using TradingService.Services.Interfaces;
using TradingService.Services.Repositories;

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

        // Repositories
        builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();

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
    }
}
