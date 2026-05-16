using System.IO;

namespace H2CursorRouter.App;

public sealed class FileLogService
{
    private readonly string _logDirectory;

    public FileLogService(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
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
}
