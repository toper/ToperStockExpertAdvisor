using Microsoft.OpenApi.Models;

namespace TradingService.Api.Configuration;

public static class SwaggerConfiguration
{
    public static void AddSwagger(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Trading Service API",
                Version = "v1",
                Description = "REST API for PUT option recommendations and trading strategies",
                Contact = new OpenApiContact
                {
                    Name = "Toper Stock Expert Advisor",
                    Email = "contact@example.com"
                }
            });

            // Add XML comments if available
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Add security definition for future authentication
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
        });
    }

    public static void ConfigureSwagger(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Trading Service API v1");
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "Trading Service API Documentation";
                options.DisplayRequestDuration();
                options.EnableDeepLinking();
                options.EnableFilter();
            });
        }
    }
}
