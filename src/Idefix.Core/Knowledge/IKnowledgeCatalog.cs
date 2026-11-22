namespace Idefix.Core.Knowledge;

public interface IKnowledgeCatalog
{
    IReadOnlyList<KnowledgePlugin> DiscoverPlugins(string rootDirectory);

    IReadOnlyList<KnowledgeDocument> ScanDocuments(KnowledgePlugin plugin);
}
