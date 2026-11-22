using System.Globalization;

namespace Idefix.App.Services;

public static class CrashLog
{
    public static void Write(Exception exception)
    {
        var directory = Path.Combine(AppServices.DataDirectory, "logs");
        Directory.CreateDirectory(directory);
        var entry = string.Create(CultureInfo.InvariantCulture, $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        File.AppendAllText(Path.Combine(directory, "crash.log"), entry);
    }
}
