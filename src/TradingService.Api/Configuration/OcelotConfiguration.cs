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

    public static async Task UseOcelotGateway(this WebApplication app)
    {
        await app.UseOcelot();
    }
}
