using System.Reflection;
using Idefix.App.Services;

namespace Idefix.App.ViewModels;

public sealed partial class SplashViewModel : ObservableObject
{
    public SplashViewModel()
    {
        this.Status = string.Empty;
    }

    public static string Version
    {
        get
        {
            var informational = typeof(SplashViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
            var metadata = informational.IndexOf('+', StringComparison.Ordinal);
            return "v" + (metadata < 0 ? informational : informational[..metadata]);
        }
    }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    public void Report(double progress, string key, params object[] arguments)
    {
        this.Progress = progress;
        this.Status = arguments.Length == 0 ? Strings.Get(key) : Strings.Format(key, arguments);
    }
}
