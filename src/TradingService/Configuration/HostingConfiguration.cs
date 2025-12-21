namespace TradingService.Configuration;

public static class HostingConfiguration
{
    public static void ConfigureHosting(this IHostBuilder builder)
    {
        builder.UseWindowsService(options =>
        {
            options.ServiceName = "TradingService";
        });
    }
}
