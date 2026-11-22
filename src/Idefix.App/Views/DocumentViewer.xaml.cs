using System.Collections.Specialized;
using Idefix.App.ViewModels;
using Microsoft.UI.Xaml.Shapes;

namespace Idefix.App.Views;

public sealed partial class DocumentViewer : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(DocumentViewerViewModel), typeof(DocumentViewer), new PropertyMetadata(null, OnViewModelChanged));

    public DocumentViewer()
    {
        this.InitializeComponent();
        this.ActualThemeChanged += (_, _) => this.DrawHighlights();
    }

    public DocumentViewerViewModel ViewModel
    {
        get => (DocumentViewerViewModel)this.GetValue(ViewModelProperty);
        set => this.SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DocumentViewer)d;
        if (e.NewValue is DocumentViewerViewModel model)
        {
            model.Highlights.CollectionChanged += view.OnHighlightsChanged;
        }
    }

    private static SolidColorBrush HighlightBrush(ElementTheme theme) => theme == ElementTheme.Dark
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(90, 0xFF, 0xB3, 0x00))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(190, 0xFF, 0xF5, 0x9D));

    private void OnHighlightsChanged(object? sender, NotifyCollectionChangedEventArgs e) => this.DrawHighlights();

    private void OnPageImageSizeChanged(object sender, SizeChangedEventArgs e) => this.DrawHighlights();

    private void OnBackdropTapped(object sender, TappedRoutedEventArgs e) => this.ViewModel.Close();

    private void DrawHighlights()
    {
        this.HighlightCanvas.Children.Clear();
        var width = this.PageImage.ActualWidth;
        var height = this.PageImage.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var brush = HighlightBrush(this.ActualTheme);
        this.HighlightCanvas.Width = width;
        this.HighlightCanvas.Height = height;
        double? firstTop = null;
        foreach (var bounds in this.ViewModel.Highlights)
        {
            var rectangle = new Rectangle
            {
                Width = (bounds.Width * width) + 6,
                Height = (bounds.Height * height) + 4,
                Fill = brush,
                RadiusX = 2,
                RadiusY = 2,
            };
            Canvas.SetLeft(rectangle, (bounds.Left * width) - 3);
            Canvas.SetTop(rectangle, (bounds.Top * height) - 2);
            this.HighlightCanvas.Children.Add(rectangle);
            firstTop ??= bounds.Top * height;
        }

        if (firstTop is { } top)
        {
            this.PageScroller.ChangeView(null, Math.Max(0, top - 120), null);
        }
    }
}
