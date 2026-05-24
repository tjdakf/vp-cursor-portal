using System.IO;
using H2CursorRouter.App;
using Xunit;

namespace H2CursorRouter.App.Tests;

public sealed class FileLogServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"h2-log-tests-{Guid.NewGuid():N}");

    [Fact]
    public void ConstructorDeletesExpiredLogFiles()
    {
        Directory.CreateDirectory(_tempDirectory);
        var oldLog = Path.Combine(_tempDirectory, "old.log");
        var recentLog = Path.Combine(_tempDirectory, "recent.log");
        File.WriteAllText(oldLog, "old");
        File.WriteAllText(recentLog, "recent");
        File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-31));
        File.SetLastWriteTimeUtc(recentLog, DateTime.UtcNow.AddDays(-1));

        _ = new FileLogService(_tempDirectory);

        Assert.False(File.Exists(oldLog));
        Assert.True(File.Exists(recentLog));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
