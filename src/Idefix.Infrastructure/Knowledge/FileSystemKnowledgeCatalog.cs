using Idefix.Core.Knowledge;

namespace Idefix.Infrastructure.Knowledge;

public sealed class FileSystemKnowledgeCatalog : IKnowledgeCatalog
{
    private static readonly EnumerationOptions TopLevel = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    public IReadOnlyList<KnowledgePlugin> DiscoverPlugins(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(rootDirectory, "*", TopLevel)
            .Select(d => new KnowledgePlugin(Path.GetFileName(d), d))
            .Where(p => KnowledgeLayout.IsPluginDirectoryName(p.Name))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<KnowledgeDocument> ScanDocuments(KnowledgePlugin plugin)
    {
        if (!Directory.Exists(plugin.DirectoryPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(plugin.DirectoryPath, "*", Recursive)
            .Where(KnowledgeLayout.IsSupportedDocument)
            .Select(path => new FileInfo(path))
            .Select(file => new KnowledgeDocument(
                plugin.Name,
                Path.GetRelativePath(plugin.DirectoryPath, file.FullName),
                file.FullName,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)))
            .ToList();
    }
}
