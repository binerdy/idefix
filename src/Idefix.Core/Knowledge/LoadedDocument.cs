using Idefix.Core.Documents;

namespace Idefix.Core.Knowledge;

public sealed record LoadedDocument(KnowledgeDocument Document, ParsedDocument Content);
