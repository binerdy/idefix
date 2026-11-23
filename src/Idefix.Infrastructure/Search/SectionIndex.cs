using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Idefix.Core.Markdown;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace Idefix.Infrastructure.Search;

internal sealed class SectionIndex : IDisposable
{
    private const string PositionField = "position";
    private const string TitleField = "title";
    private const string TextField = "text";
    private const string GramsField = "grams";
    private const float TitleBoost = 2f;

    // A pasted page as a question must not exceed Lucene's limit of 1024 clauses.
    private const int MaxTerms = 400;

    private readonly RAMDirectory directory = new();
    private readonly DirectoryReader reader;
    private readonly IndexSearcher searcher;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposing the wrapper would close the shared analyzers.")]
    public SectionIndex(IReadOnlyList<DocumentSection> sections)
    {
        this.Sections = [.. sections];
        var similarity = new BM25Similarity();
        var analyzer = new PerFieldAnalyzerWrapper(
            SearchText.Words,
            new Dictionary<string, Analyzer>(StringComparer.Ordinal) { [GramsField] = SearchText.Grams });
        using (var writer = new IndexWriter(this.directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer) { Similarity = similarity }))
        {
            for (var i = 0; i < this.Sections.Count; i++)
            {
                var section = this.Sections[i];
                var title = $"{section.FileName.Replace('_', ' ')} {section.Title}";
                Document document =
                [
                    new StoredField(PositionField, i),
                    new TextField(TitleField, title, Field.Store.NO),
                    new TextField(TextField, section.Markdown, Field.Store.NO),
                    new TextField(GramsField, $"{title} {section.Markdown}", Field.Store.NO),
                ];
                writer.AddDocument(document);
            }
        }

        this.reader = DirectoryReader.Open(this.directory);
        this.searcher = new IndexSearcher(this.reader) { Similarity = similarity };
    }

    public IReadOnlyList<DocumentSection> Sections { get; }

    public Dictionary<int, float> MatchWords(string query) =>
        this.Match(SearchText.Terms(SearchText.Words, query), [(TitleField, TitleBoost), (TextField, 1f)]);

    public Dictionary<int, float> MatchGrams(string query) =>
        this.Match(SearchText.Terms(SearchText.Grams, query), [(GramsField, 1f)]);

    public void Dispose()
    {
        this.reader.Dispose();
        this.directory.Dispose();
    }

    private Dictionary<int, float> Match(List<string> terms, (string Field, float Boost)[] fields)
    {
        if (terms.Count == 0 || this.Sections.Count == 0)
        {
            return [];
        }

        var query = new BooleanQuery();
        foreach (var term in terms.Take(MaxTerms))
        {
            foreach (var (field, boost) in fields)
            {
                query.Add(new TermQuery(new Term(field, term)) { Boost = boost }, Occur.SHOULD);
            }
        }

        return this.searcher.Search(query, this.Sections.Count).ScoreDocs.ToDictionary(
            hit => int.Parse(this.searcher.Doc(hit.Doc).Get(PositionField, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            hit => hit.Score);
    }
}
