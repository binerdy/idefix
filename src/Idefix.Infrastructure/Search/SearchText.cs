using System.Diagnostics.CodeAnalysis;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.De;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.NGram;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;

namespace Idefix.Infrastructure.Search;

internal static class SearchText
{
    private const LuceneVersion Version = LuceneVersion.LUCENE_48;
    private const int GramLength = 3;
    private const int MinGramWordLength = 4;
    private const int MaxWordLength = 255;

    // Rare in regulations, so BM25 would otherwise weigh them like the actual subject of a question.
    private static readonly string[] QuestionWords =
    [
        "bitte", "brauche", "braucht", "brauchen", "darf", "dürfen", "gibt", "kann", "können", "muss", "müssen", "sag", "sage", "sagt",
        "steht", "viel", "viele", "wieviel", "wieviele", "welche", "welcher", "welches", "wann", "warum", "wer", "wo", "wozu",
        "can", "could", "do", "does", "have", "how", "i", "many", "may", "much", "please", "should", "tell", "what", "when", "where", "which", "who", "why",
    ];

    private static readonly CharArraySet StopWords = CreateStopWords();

    public static Analyzer Words { get; } = Analyzer.NewAnonymous((_, reader) =>
    {
        var source = new StandardTokenizer(Version, reader);
        return new TokenStreamComponents(source, new GermanLightStemFilter(Normalize(source)));
    });

    public static Analyzer Grams { get; } = Analyzer.NewAnonymous((_, reader) =>
    {
        var source = new StandardTokenizer(Version, reader);
        var words = new LengthFilter(Version, Normalize(source), MinGramWordLength, MaxWordLength);
        return new TokenStreamComponents(source, new NGramTokenFilter(Version, words, GramLength, GramLength));
    });

    public static List<string> Terms(Analyzer analyzer, string text)
    {
        var terms = new List<string>();
        using var stream = analyzer.GetTokenStream(string.Empty, text);
        var term = stream.AddAttribute<ICharTermAttribute>();
        stream.Reset();
        while (stream.IncrementToken())
        {
            terms.Add(term.ToString());
        }

        stream.End();
        return terms.Distinct(StringComparer.Ordinal).ToList();
    }

    // Stop words go first: the list contains umlauts, which the normalization filter rewrites.
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The outermost filter owns the chain; Lucene reuses and disposes it.")]
    private static GermanNormalizationFilter Normalize(TokenStream source) =>
        new(new StopFilter(Version, new LowerCaseFilter(Version, source), StopWords));

    private static CharArraySet CreateStopWords()
    {
        var words = new CharArraySet(Version, GermanAnalyzer.DefaultStopSet, ignoreCase: true);
        words.UnionWith(StopAnalyzer.ENGLISH_STOP_WORDS_SET);
        words.UnionWith(QuestionWords);
        return words;
    }
}
