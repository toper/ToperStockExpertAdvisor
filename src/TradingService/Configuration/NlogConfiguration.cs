using NLog.Web;
using Microsoft.Extensions.Logging;

namespace TradingService.Configuration;

public static class NlogConfiguration
{
    public static void AddNlog(this IHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
        })
        .UseNLog();
    }
}
