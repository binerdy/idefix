using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Idefix.App.Services;
using Idefix.Core.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using WinPdfDocument = Windows.Data.Pdf.PdfDocument;

namespace Idefix.App.ViewModels;

public sealed partial class DocumentViewerViewModel(AppServices services) : ObservableObject
{
    private ParsedDocument? document;
    private WinPdfDocument? pdf;
    private Citation? citation;

    public ObservableCollection<DocumentLineViewModel> Lines { get; } = [];

    public ObservableCollection<TextBounds> Highlights { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FilePath { get; set; }

    [ObservableProperty]
    public partial bool IsPdf { get; set; }

    [ObservableProperty]
    public partial BitmapImage? PageImage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    public partial int PageNumber { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    public partial int PageCount { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    public string PageLabel => Strings.Format("PageFormat", this.PageNumber, this.PageCount);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unreadable files are reported inside the viewer.")]
    public async Task OpenAsync(Citation target)
    {
        this.citation = target;
        this.Title = target.FileName;
        this.Error = null;
        this.IsOpen = true;
        this.FilePath = this.Locate(target);
        if (this.FilePath is null)
        {
            this.Error = Strings.Get("DocumentMissing");
            return;
        }

        try
        {
            var path = this.FilePath;
            this.document = await Task.Run(() => (services.Runtime?.Parser ?? Infrastructure.Parsing.CompositeDocumentParser.CreateDefault()).Parse(path));
            this.IsPdf = string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
            this.pdf = this.IsPdf ? await WinPdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(path)) : null;
            this.PageCount = this.document.Pages.Count;
            await this.ShowPageAsync(Math.Clamp(target.Page, 1, Math.Max(1, this.PageCount)));
        }
        catch (Exception ex)
        {
            this.Error = ex.Message;
        }
    }

    [RelayCommand]
    public void Close()
    {
        this.IsOpen = false;
        this.PageImage = null;
        this.pdf = null;
        this.document = null;
    }

    [RelayCommand]
    public void OpenOriginal()
    {
        if (this.FilePath is not null)
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = this.FilePath, UseShellExecute = true });
        }
    }

    private static async Task<BitmapImage> RenderAsync(WinPdfDocument pdf, int index)
    {
        using var page = pdf.GetPage((uint)index);
        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, new Windows.Data.Pdf.PdfPageRenderOptions { DestinationWidth = (uint)Math.Min(2000, page.Size.Width * 2) });
        stream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }

    private bool CanGoNext() => this.PageNumber < this.PageCount;

    private bool CanGoPrevious() => this.PageNumber > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextPageAsync() => this.ShowPageAsync(this.PageNumber + 1);

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousPageAsync() => this.ShowPageAsync(this.PageNumber - 1);

    private async Task ShowPageAsync(int number)
    {
        if (this.document is null)
        {
            return;
        }

        this.PageNumber = number;
        var page = this.document.Pages[number - 1];
        var highlighted = page.Lines.Where(this.IsCited).ToList();

        this.Lines.Clear();
        foreach (var line in page.Lines)
        {
            this.Lines.Add(new DocumentLineViewModel(line.Text, highlighted.Contains(line)));
        }

        this.Highlights.Clear();
        foreach (var bounds in highlighted.Select(l => l.Bounds).OfType<TextBounds>())
        {
            this.Highlights.Add(bounds);
        }

        this.PageImage = this.pdf is null ? null : await RenderAsync(this.pdf, number - 1);
    }

    private bool IsCited(TextLine line) =>
        this.citation is { FirstLine: > 0 } c && c.Page == this.PageNumber && line.Number >= c.FirstLine && line.Number <= c.LastLine;

    private string? Locate(Citation target)
    {
        var root = services.Settings.RootDirectory;
        if (target.DocumentKey is not null)
        {
            var path = Path.Combine(root, target.DocumentKey.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                return path;
            }
        }

        return services.Catalog.DiscoverPlugins(root)
            .SelectMany(services.Catalog.ScanDocuments)
            .FirstOrDefault(d => string.Equals(d.FileName, target.FileName, StringComparison.OrdinalIgnoreCase))?.FullPath;
    }
}
