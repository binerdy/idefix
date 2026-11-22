using System.Text.RegularExpressions;
using Idefix.Core.Documents;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Documents;

namespace Idefix.App.Controls;

public sealed partial class CitedText : ContentControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CitedText), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty CitationsProperty = DependencyProperty.Register(
        nameof(Citations), typeof(IReadOnlyList<Citation>), typeof(CitedText), new PropertyMetadata(null, OnContentChanged));

    public static readonly DependencyProperty LinkForegroundProperty = DependencyProperty.Register(
        nameof(LinkForeground), typeof(Brush), typeof(CitedText), new PropertyMetadata(null, OnContentChanged));

    private readonly RichTextBlock block = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, LineHeight = 24 };

    public CitedText()
    {
        this.Content = this.block;
        this.IsTabStop = false;
    }

    public event EventHandler<CitationEventArgs>? CitationClicked;

    public string Text
    {
        get => (string)this.GetValue(TextProperty);
        set => this.SetValue(TextProperty, value);
    }

    public IReadOnlyList<Citation>? Citations
    {
        get => (IReadOnlyList<Citation>?)this.GetValue(CitationsProperty);
        set => this.SetValue(CitationsProperty, value);
    }

    public Brush? LinkForeground
    {
        get => (Brush?)this.GetValue(LinkForegroundProperty);
        set => this.SetValue(LinkForegroundProperty, value);
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CitedText)d).Render();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex BoldPattern();

    private static void AddFormatted(InlineCollection inlines, string text)
    {
        var parts = BoldPattern().Split(text);
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                continue;
            }

            var run = new Run { Text = parts[i] };
            if (i % 2 == 1)
            {
                var bold = new Bold();
                bold.Inlines.Add(run);
                inlines.Add(bold);
            }
            else
            {
                inlines.Add(run);
            }
        }
    }

    private void Render()
    {
        this.block.Blocks.Clear();
        foreach (var line in (this.Text ?? string.Empty).Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            var content = line;
            if (content.StartsWith("- ", StringComparison.Ordinal) || content.StartsWith("* ", StringComparison.Ordinal))
            {
                paragraph.Inlines.Add(new Run { Text = "•  " });
                content = content[2..];
            }
            else if (content.Length > 0 && content[0] == '#')
            {
                content = content.TrimStart('#').TrimStart();
                paragraph.FontWeight = FontWeights.SemiBold;
            }

            this.AddInlines(paragraph.Inlines, content);
            this.block.Blocks.Add(paragraph);
        }
    }

    private void AddInlines(InlineCollection inlines, string text)
    {
        var position = 0;
        foreach (var match in CitationParser.Find(text))
        {
            AddFormatted(inlines, text[position..match.Index]);
            inlines.Add(this.CreateLink(match));
            position = match.Index + match.Length;
        }

        AddFormatted(inlines, text[position..]);
    }

    private Hyperlink CreateLink(CitationMatch match)
    {
        var resolved = this.Citations?.FirstOrDefault(c =>
            string.Equals(c.FileName, match.Citation.FileName, StringComparison.OrdinalIgnoreCase) && c.Page == match.Citation.Page) ?? match.Citation;
        var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
        if (this.LinkForeground is { } brush)
        {
            link.Foreground = brush;
        }

        link.Inlines.Add(new Run { Text = resolved.ToString(), FontWeight = FontWeights.Medium });
        link.Click += (_, _) => this.CitationClicked?.Invoke(this, new CitationEventArgs(resolved));
        return link;
    }
}
