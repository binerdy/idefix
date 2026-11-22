namespace Idefix.Core.Knowledge;

public static class KnowledgeLayout
{
    public const string MarkdownFolderName = "_markdown";

    private static readonly string[] SupportedExtensions = [".pdf", ".docx"];

    public static IReadOnlyList<string> Extensions => SupportedExtensions;

    public static bool IsSupportedDocument(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.StartsWith("~$", StringComparison.Ordinal))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return SupportedExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsInMarkdownFolder(string relativePath) =>
        relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Contains(MarkdownFolderName, StringComparer.OrdinalIgnoreCase);

    public static bool IsPluginDirectoryName(string directoryName) =>
        !string.IsNullOrWhiteSpace(directoryName) && directoryName[0] != '.';

    public static string DocumentKey(string pluginName, string relativePath) =>
        $"{pluginName}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
}
