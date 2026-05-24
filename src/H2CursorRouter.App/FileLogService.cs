using System.IO;

namespace H2CursorRouter.App;

public sealed class FileLogService
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);
    private readonly string _logDirectory;

    public FileLogService(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
        DeleteExpiredLogs();
    }

    public void Append(string message)
    {
        try
        {
            var path = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never interfere with emergency unlock or routing control.
        }
    }

    private void DeleteExpiredLogs()
    {
        try
        {
            var cutoff = DateTime.UtcNow - RetentionPeriod;
            foreach (var path in Directory.EnumerateFiles(_logDirectory, "*.log"))
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // Retention cleanup must not block startup.
        }
    }
}
