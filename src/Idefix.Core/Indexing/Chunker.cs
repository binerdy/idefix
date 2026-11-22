using System.Globalization;
using Idefix.Core.Documents;
using Idefix.Core.Knowledge;

namespace Idefix.Core.Indexing;

public static class Chunker
{
    public static IReadOnlyList<DocumentChunk> Chunk(KnowledgeDocument document, ParsedDocument parsed, ChunkingOptions options)
    {
        var chunks = new List<DocumentChunk>();
        foreach (var page in parsed.Pages)
        {
            AddPageChunks(document, page, options, chunks);
        }

        return chunks;
    }

    private static void AddPageChunks(KnowledgeDocument document, DocumentPage page, ChunkingOptions options, List<DocumentChunk> chunks)
    {
        var lines = page.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
        var start = 0;
        while (start < lines.Count)
        {
            var end = start;
            var length = lines[start].Text.Length;
            while (end + 1 < lines.Count && length + lines[end + 1].Text.Length + 1 <= options.MaxCharacters)
            {
                end++;
                length += lines[end].Text.Length + 1;
            }

            var window = lines.GetRange(start, end - start + 1);
            var id = string.Create(CultureInfo.InvariantCulture, $"{document.Key}#p{page.Number}:{window[0].Number}-{window[^1].Number}");
            chunks.Add(new DocumentChunk(
                id,
                document.Key,
                document.PluginName,
                document.FileName,
                page.Number,
                window[0].Number,
                window[^1].Number,
                string.Join('\n', window.Select(l => l.Text))));

            if (end + 1 >= lines.Count)
            {
                break;
            }

            start = Math.Max(start + 1, end + 1 - options.OverlapLines);
        }
    }
}
