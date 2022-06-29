using System;

namespace SpeedTest
{
  internal sealed class SpeedCheck
  {
    public Guid Id { get; init; }

    public DateTime DateTime { get; init; }

    public long DownloadBytesPerSec { get; init; }

    public long UploadBytesPerSec { get; init; }

    public decimal DownloadMbits => Math.Round(this.DownloadBytesPerSec / 125000m, 2);

    public decimal UploadMbits => Math.Round(this.UploadBytesPerSec / 125000m, 2);

    public string Link => $"https://www.speedtest.net/result/c/{this.Id}";
  }
}