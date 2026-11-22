using Windows.Foundation;

namespace Idefix.App.Controls;

public sealed partial class CenteredColumn : Panel
{
    public static readonly DependencyProperty ColumnWidthProperty = DependencyProperty.Register(
        nameof(ColumnWidth), typeof(double), typeof(CenteredColumn), new PropertyMetadata(760d, OnColumnWidthChanged));

    public double ColumnWidth
    {
        get => (double)this.GetValue(ColumnWidthProperty);
        set => this.SetValue(ColumnWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(this.ColumnWidth, availableSize.Width);
        var height = 0d;
        foreach (var child in this.Children)
        {
            child.Measure(new Size(width, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Min(this.ColumnWidth, finalSize.Width);
        var left = (finalSize.Width - width) / 2;
        foreach (var child in this.Children)
        {
            child.Arrange(new Rect(left, 0, width, finalSize.Height));
        }

        return finalSize;
    }

    private static void OnColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CenteredColumn)d).InvalidateMeasure();
}
