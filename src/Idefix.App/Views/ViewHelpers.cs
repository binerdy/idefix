namespace Idefix.App.Views;

public static class ViewHelpers
{
    public static bool Not(bool value) => !value;

    public static bool HasText(string? value) => !string.IsNullOrEmpty(value);

    public static Visibility CollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
}
