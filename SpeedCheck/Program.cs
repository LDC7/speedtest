using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SpeedTest
{
  public sealed class Program
  {
    public static Task Main(string[] args)
    {
      var builder = new HostBuilder();
      _ = builder
        .ConfigureDefaults(args)
        .ConfigureServices(ConfigureServices)
        .UseConsoleLifetime()
        .UseSerilog(ConfigureLogging);

      var host = builder.Build();
      return host.RunAsync();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
      _ = services
        .AddHostedService<HostService>()
        .AddTransient<Storage>()
        .AddTransient<SpeedCheckService>();
    }

    private static void ConfigureLogging(HostBuilderContext context, IServiceProvider serviceProvider, LoggerConfiguration configuration)
    {
      var logsFolder = context.Configuration.GetSection("Logging").GetSection("logsFolder").Value;
      var logFileName = $"{DateTime.Now.ToString("dd.MM.yyyy")}.log";
      var logsPath = Path.Combine(logsFolder, logFileName);

      _ = configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(serviceProvider)
        .Enrich.FromLogContext()
        .WriteTo.File(logsPath);
    }
  }
}
