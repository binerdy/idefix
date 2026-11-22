namespace Idefix.Core.Indexing;

public sealed record DocumentChunk(
    string Id,
    string DocumentKey,
    string PluginName,
    string FileName,
    int Page,
    int FirstLine,
    int LastLine,
    string Text);
