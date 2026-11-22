using System.Globalization;
using Idefix.Core.Settings;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Idefix.App.Services;

public static class Strings
{
    private static ResourceMap? map;
    private static ResourceContext? context;

    public static string? LanguageTag { get; private set; }

    public static void Initialize(AppLanguage language)
    {
        LanguageTag = language switch
        {
            AppLanguage.English => "en",
            AppLanguage.GermanSwitzerland => "de-CH",
            _ => null,
        };

        var manager = new ResourceManager();
        context = manager.CreateResourceContext();
        if (LanguageTag is not null)
        {
            context.QualifierValues["Language"] = LanguageTag;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(LanguageTag);
        }

        map = manager.MainResourceMap.GetSubtree("Resources");
    }

    public static string Get(string key) => map?.TryGetValue(key, context)?.ValueAsString ?? key;

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
