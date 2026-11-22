namespace Idefix.Core.Documents;

public readonly record struct TextBounds(double Left, double Top, double Width, double Height)
{
    public static TextBounds Union(IEnumerable<TextBounds> bounds)
    {
        var list = bounds.ToList();
        if (list.Count == 0)
        {
            return default;
        }

        var left = list.Min(b => b.Left);
        var top = list.Min(b => b.Top);
        var right = list.Max(b => b.Left + b.Width);
        var bottom = list.Max(b => b.Top + b.Height);
        return new TextBounds(left, top, right - left, bottom - top);
    }
}
