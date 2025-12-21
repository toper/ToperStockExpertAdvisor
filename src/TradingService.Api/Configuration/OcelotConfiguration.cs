using Ocelot.DependencyInjection;

namespace TradingService.Api.Configuration;

public static class OcelotConfiguration
{
    public static void AddOcelotGateway(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
        builder.Services.AddOcelot(builder.Configuration);
    }
}
