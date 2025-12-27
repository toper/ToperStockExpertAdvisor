using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;

namespace TradingService.Api.Configuration;

public static class OcelotConfiguration
{
    public static void AddOcelotGateway(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
        builder.Services
            .AddOcelot(builder.Configuration)
            .AddConsul();
    }

    public static void UseOcelotGateway(this WebApplication app)
    {
        // Ocelot ONLY handles /gateway/* paths (external microservices)
        // All other paths (/api/*, /health, /swagger) go directly to MapControllers
        app.MapWhen(
            context => context.Request.Path.StartsWithSegments("/gateway"),
            gatewayApp =>
            {
                gatewayApp.UseOcelot().Wait();
            });
    }
}
