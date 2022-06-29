using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace SpeedTest
{
  internal sealed class SpeedCheckService
  {
    private readonly ILogger<SpeedCheckService> logger;
    private readonly long serverId;

    public SpeedCheckService(ILogger<SpeedCheckService> logger, IConfiguration configuration)
    {
      this.logger = logger;
      this.serverId = configuration.GetSection(nameof(SpeedCheckService)).GetSection(nameof(this.serverId)).Get<long>();
    }

    public Task<SpeedCheck?> Check(CancellationToken cancellationToken = default)
    {
      this.logger.LogInformation($"Start {nameof(this.Check)}");

      try
      {
        string? data = null;
        using (var process = new Process())
        {
          process.EnableRaisingEvents = true;
          process.StartInfo = new ProcessStartInfo()
          {
            Arguments = $"--format=json --progress=no -u Mibps -s {this.serverId}",
            CreateNoWindow = true,
            FileName = "speedtest.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
          };
          process.OutputDataReceived += (sender, args) =>
          {
            if (!string.IsNullOrWhiteSpace(args.Data))
              data = args.Data;
          };

          _ = process.Start();
          process.BeginOutputReadLine();
          process.BeginErrorReadLine();
          process.WaitForExit();

          var speedTestData = JObject.Parse(data);
          var result = new SpeedCheck()
          {
            Id = Guid.Parse(speedTestData["result"]["id"].Value<string>()),
            DateTime = DateTime.Now,
            DownloadBytesPerSec = speedTestData["download"]["bandwidth"].Value<long>(),
            UploadBytesPerSec = speedTestData["upload"]["bandwidth"].Value<long>()
          };

          this.logger.LogInformation($"End {nameof(this.Check)}");
          return Task.FromResult<SpeedCheck?>(result);
        }
      }
      catch (Exception ex)
      {
        this.logger.LogError(ex, null);
        return Task.FromResult<SpeedCheck?>(null);
      }
    }
  }
}
