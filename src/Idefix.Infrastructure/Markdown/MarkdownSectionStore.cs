using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Idefix.Core.Knowledge;
using Idefix.Core.Markdown;

namespace Idefix.Infrastructure.Markdown;

public sealed class MarkdownSectionStore : ISectionStore, IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly SemaphoreSlim gate = new(1, 1);

    public int Writes { get; private set; }

    public static string FolderFor(KnowledgeDocument document) =>
        Path.Combine(PluginDirectory(document), KnowledgeLayout.MarkdownFolderName, document.RelativePath);

    public Task<IReadOnlyList<DocumentSection>> SynchronizeAsync(IReadOnlyList<LoadedDocument> documents, CancellationToken cancellationToken) =>
        this.ProcessAsync(documents, rebuild: false, cancellationToken);

    public Task<IReadOnlyList<DocumentSection>> RebuildAsync(IReadOnlyList<LoadedDocument> documents, CancellationToken cancellationToken) =>
        this.ProcessAsync(documents, rebuild: true, cancellationToken);

    public async Task PruneAsync(IReadOnlyList<KnowledgeDocument> present, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            Prune(present, cancellationToken);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public void Dispose() => this.gate.Dispose();

    private static void Prune(IReadOnlyList<KnowledgeDocument> present, CancellationToken cancellationToken)
    {
        var expected = present.Select(d => Path.GetFullPath(FolderFor(d))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var root in present.Select(d => Path.Combine(PluginDirectory(d), KnowledgeLayout.MarkdownFolderName)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var folder in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                .Where(f => KnowledgeLayout.IsSupportedDocument(f) && !expected.Contains(Path.GetFullPath(f)))
                .ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static string PluginDirectory(KnowledgeDocument document) =>
        document.FullPath[..^document.RelativePath.Length].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string FileName(DocumentSection section) =>
        string.Create(CultureInfo.InvariantCulture, $"{section.Index:00}-{Slug(section.Title)}.md");

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Lowercase slugs are file names, not comparisons.")]
    private static string Slug(string title)
    {
        var builder = new StringBuilder();
        foreach (var c in title.ToLowerInvariant())
        {
            builder.Append(c switch
            {
                'ä' => "ae",
                'ö' => "oe",
                'ü' => "ue",
                'ß' => "ss",
                _ when char.IsAsciiLetterOrDigit(c) => c.ToString(),
                _ => "-",
            });
        }

        var slug = string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "section" : slug[..Math.Min(slug.Length, 40)].TrimEnd('-');
    }

    private static string Render(DocumentSection section, KnowledgeDocument document) =>
        string.Join(
            '\n',
            "---",
            $"source: {document.RelativePath.Replace(Path.DirectorySeparatorChar, '/')}",
            string.Create(CultureInfo.InvariantCulture, $"section: {section.Index}"),
            $"title: {section.Title.ReplaceLineEndings(" ")}",
            string.Create(CultureInfo.InvariantCulture, $"pages: {section.FirstPage}-{section.LastPage}"),
            string.Create(CultureInfo.InvariantCulture, $"source-length: {document.Length}"),
            string.Create(CultureInfo.InvariantCulture, $"source-modified: {document.LastWriteUtc.UtcTicks}"),
            string.Create(CultureInfo.InvariantCulture, $"generator: {MarkdownSplitter.GeneratorVersion}"),
            $"body-hash: {Hash(section.Markdown)}",
            "---",
            string.Empty,
            section.Markdown.ReplaceLineEndings("\n"));

    private static DocumentSection? Read(string path, KnowledgeDocument document)
    {
        var text = File.ReadAllText(path, Utf8).ReplaceLineEndings("\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return null;
        }

        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var header = text[4..end].Split('\n')
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        var body = text[(end + 5)..].TrimStart('\n');
        var pages = header.GetValueOrDefault("pages", "1-1").Split('-');
        return new DocumentSection(
            document.Key,
            document.PluginName,
            document.FileName,
            int.TryParse(header.GetValueOrDefault("section"), CultureInfo.InvariantCulture, out var index) ? index : 0,
            header.GetValueOrDefault("title") ?? Path.GetFileNameWithoutExtension(path),
            int.TryParse(pages[0], CultureInfo.InvariantCulture, out var first) ? first : 1,
            int.TryParse(pages[^1], CultureInfo.InvariantCulture, out var last) ? last : first,
            body);
    }

    private static string Hash(string markdown) =>
        Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(markdown.ReplaceLineEndings("\n"))))[..16];

    private static Dictionary<string, string> Header(string path)
    {
        var lines = File.ReadLines(path, Utf8).ToList();
        return lines.Skip(1).TakeWhile(l => l != "---")
            .Select(l => l.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsEdited(string path)
    {
        var text = File.ReadAllText(path, Utf8).ReplaceLineEndings("\n");
        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        var body = end < 0 ? text : text[(end + 5)..].TrimStart('\n');
        return !string.Equals(Header(path).GetValueOrDefault("body-hash"), Hash(body), StringComparison.Ordinal);
    }

    private static bool NeedsRegeneration(string folder, KnowledgeDocument document)
    {
        var files = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.Ordinal).ToList() : [];
        if (files.Count == 0)
        {
            return true;
        }

        var header = Header(files[0]);
        var sourceChanged = header.GetValueOrDefault("source-length") != document.Length.ToString(CultureInfo.InvariantCulture)
            || header.GetValueOrDefault("source-modified") != document.LastWriteUtc.UtcTicks.ToString(CultureInfo.InvariantCulture);
        if (sourceChanged)
        {
            return true;
        }

        var outdated = header.GetValueOrDefault("generator") != MarkdownSplitter.GeneratorVersion.ToString(CultureInfo.InvariantCulture);
        return outdated && !files.Exists(IsEdited);
    }

    private async Task<IReadOnlyList<DocumentSection>> ProcessAsync(IReadOnlyList<LoadedDocument> documents, bool rebuild, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            var sections = new List<DocumentSection>();
            foreach (var loaded in documents)
            {
                sections.AddRange(await this.SynchronizeAsync(loaded, rebuild, cancellationToken));
            }

            return sections;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async Task<IReadOnlyList<DocumentSection>> SynchronizeAsync(LoadedDocument loaded, bool rebuild, CancellationToken cancellationToken)
    {
        var document = loaded.Document;
        var folder = FolderFor(document);
        if (rebuild || NeedsRegeneration(folder, document))
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            Directory.CreateDirectory(folder);
            foreach (var section in MarkdownSplitter.Split(loaded))
            {
                await File.WriteAllTextAsync(Path.Combine(folder, FileName(section)), Render(section, document), Utf8, cancellationToken);
            }

            this.Writes++;
        }

        return Directory.EnumerateFiles(folder, "*.md")
            .Order(StringComparer.Ordinal)
            .Select(path => Read(path, document))
            .OfType<DocumentSection>()
            .ToList();
    }
}
