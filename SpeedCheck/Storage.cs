using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpeedTest
{
  internal sealed class Storage
  {
    private const string DateTimeFormat = "yyyyMMddHHmmss";

    private readonly ILogger<Storage> logger;
    private readonly string connectionString;

    public Storage(ILogger<Storage> logger, IConfiguration configuration)
    {
      this.logger = logger;
      this.connectionString = configuration.GetConnectionString(nameof(this.connectionString));
      this.EnsureTableCreated().Wait();
    }

    private async Task EnsureTableCreated()
    {
      this.logger.LogTrace($"Start {nameof(this.EnsureTableCreated)}");

      await using (var connection = new SqliteConnection(this.connectionString))
      {
        await connection.OpenAsync();
        await using var selectTableCommand = connection.CreateCommand();
        selectTableCommand.CommandType = CommandType.Text;
        selectTableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'SpeedChecks';";
        using var reader = await selectTableCommand.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
          await using var createTableCommand = connection.CreateCommand();
          selectTableCommand.CommandType = CommandType.Text;
          createTableCommand.CommandText = "CREATE TABLE SpeedChecks (Id BLOB PRIMARY KEY, DateTime text, Download INTEGER, Upload INTEGER)";
          _ = createTableCommand.ExecuteNonQuery();
        }
      }

      this.logger.LogTrace($"End {nameof(this.EnsureTableCreated)}");
    }

    public async Task Write(SpeedCheck speedCheck)
    {
      this.logger.LogInformation("Start {method} ({id}).", nameof(this.Write), speedCheck.Id);

      await using (var connection = new SqliteConnection(this.connectionString))
      {
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "INSERT INTO SpeedChecks (Id, DateTime, Download, Upload) "
          + "VALUES (@id, @dateTime, @download, @upload);";
        _ = command.Parameters.AddWithValue("@id", speedCheck.Id);
        _ = command.Parameters.AddWithValue("@dateTime", speedCheck.DateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("@download", speedCheck.DownloadBytesPerSec);
        _ = command.Parameters.AddWithValue("@upload", speedCheck.UploadBytesPerSec);
        _ = await command.ExecuteNonQueryAsync();
      }

      this.logger.LogInformation("End {method} ({id}).", nameof(this.Write), speedCheck.Id);
    }

    public async Task<IDictionary<DateTime, IEnumerable<SpeedCheck>>> GetLastTenDays()
    {
      this.logger.LogInformation($"Start {nameof(this.GetLastTenDays)}");

      var tenDaysAgo = DateTime.Now.Date.AddDays(-9);
      var result = new Dictionary<DateTime, IEnumerable<SpeedCheck>>();
      await foreach (var entry in this.GetAll())
      {
        if (entry.DateTime < tenDaysAgo)
          break;

        if (!result.TryGetValue(entry.DateTime.Date, out var list))
        {
          list = new List<SpeedCheck>();
          result.Add(entry.DateTime.Date, list);
        }

        ((ICollection<SpeedCheck>)list).Add(entry);
      }

      this.logger.LogInformation($"End {nameof(this.GetLastTenDays)}");
      return result;
    }

    private async IAsyncEnumerable<SpeedCheck> GetAll()
    {
      this.logger.LogTrace($"Start {nameof(this.GetAll)}");

      await using (var connection = new SqliteConnection(this.connectionString))
      {
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT Id, DateTime, Download, Upload "
          + "FROM SpeedChecks "
          + "ORDER BY DateTime DESC;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
          yield return new SpeedCheck()
          {
            Id = reader.GetGuid(0),
            DateTime = DateTime.ParseExact(reader.GetString(1), DateTimeFormat, CultureInfo.InvariantCulture),
            DownloadBytesPerSec = reader.GetInt64(2),
            UploadBytesPerSec = reader.GetInt64(3)
          };
        }
      }

      this.logger.LogTrace($"End {nameof(this.GetAll)}");
    }
  }
}
