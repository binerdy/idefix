using System.Globalization;

namespace Idefix.App.Services;

public static class CrashLog
{
    public static void Write(Exception exception) => Append("crash.log", exception.ToString());

    public static void Append(string fileName, string message)
    {
        var directory = Path.Combine(AppServices.DataDirectory, "logs");
        Directory.CreateDirectory(directory);
        var entry = string.Create(CultureInfo.InvariantCulture, $"[{TimeProvider.System.GetLocalNow():O}] {message}{Environment.NewLine}");
        File.AppendAllText(Path.Combine(directory, fileName), entry);
    }
}
