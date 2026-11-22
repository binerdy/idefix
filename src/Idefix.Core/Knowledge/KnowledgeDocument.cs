namespace Idefix.Core.Knowledge;

public sealed record KnowledgeDocument(string PluginName, string RelativePath, string FullPath, long Length, DateTimeOffset LastWriteUtc)
{
    public string Key => KnowledgeLayout.DocumentKey(this.PluginName, this.RelativePath);

    public string FileName => Path.GetFileName(this.RelativePath);

    public DocumentFingerprint Fingerprint => new(this.Length, this.LastWriteUtc);
}
