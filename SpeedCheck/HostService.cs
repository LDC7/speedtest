using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpeedTest
{
  internal sealed class HostService : IHostedService, IAsyncDisposable
  {
    private readonly ILogger<HostService> logger;
    private readonly Storage storage;
    private readonly SpeedCheckService speedCheckService;
    private readonly int mbitsThreshold;
    private Timer? timer = null;

    public HostService(
      ILogger<HostService> logger,
      Storage storage,
      SpeedCheckService speedCheckService,
      IConfiguration configuration)
    {
      this.logger = logger;
      this.storage = storage;
      this.speedCheckService = speedCheckService;
      this.mbitsThreshold = configuration.GetRequiredSection(nameof(this.mbitsThreshold)).Get<int>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      this.logger.LogInformation("Hosted Service is starting.");

      var isCurrentHourEntryExist = false;
      var currentDate = DateTime.Now.Date;
      var currentHour = currentDate.AddHours(DateTime.Now.Hour);

      var previousForegroundColor = Console.ForegroundColor;
      foreach (var dayData in (await this.storage.GetLastTenDays()).OrderBy(p => p.Key))
      {
        var maxDownload = dayData.Value.Max(v => v.DownloadMbits);
        var maxUpload = dayData.Value.Max(v => v.UploadMbits);
        Console.ForegroundColor = maxDownload < this.mbitsThreshold || maxUpload < this.mbitsThreshold ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine($"[{dayData.Key.ToString("dd.MM.yy")}] Download: {maxDownload} Upload: {maxUpload}");
        Console.ForegroundColor = previousForegroundColor;

        if (dayData.Key.Equals(currentDate) && dayData.Value.Any(v => v.DateTime >= currentHour))
          isCurrentHourEntryExist = true;
      }

      var dueTime = TimeSpan.Zero;
      if (!isCurrentHourEntryExist)
      {
        this.DoWork(null);
        dueTime = currentHour.AddHours(2).Subtract(DateTime.Now);
      }

      this.timer = new Timer(this.DoWork, null, dueTime, TimeSpan.FromHours(1));
    }

    // It's ok to be 'async void'.
    private async void DoWork(object? _)
    {
      this.logger.LogTrace(nameof(DoWork));
      var check = await this.speedCheckService.Check();
      if (check != null)
      {
        Console.WriteLine($"[{DateTime.Now.ToString("dd.MM.yy HH:mm")}] Download: {check.DownloadMbits} Upload: {check.UploadMbits}");
        await this.storage.Write(check);
      }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      this.logger.LogInformation("Hosted Service is stopping.");
      _ = this.timer?.Change(Timeout.Infinite, 0);

      return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
      if (this.timer != null)
      {
        await this.timer.DisposeAsync();
        this.timer = null;
      }
    }
  }
}