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

        // Configuration
        var configuration = builder.Configuration;
        var appSettings = new AppSettings();
        configuration.GetSection("AppSettings").Bind(appSettings);
        builder.Services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

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
    }
}
