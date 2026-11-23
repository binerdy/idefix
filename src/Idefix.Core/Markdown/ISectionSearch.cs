namespace Idefix.Core.Markdown;

public interface ISectionSearch
{
    IReadOnlyList<DocumentSection> Search(string query, IReadOnlyList<DocumentSection> sections, int limit);
}
