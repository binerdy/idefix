# agent-ping-pong Specification

## Purpose
Answer questions from the user's knowledge with a transparent, agentic retrieval loop.

## Requirements

### Requirement: Search tool
The answer engine SHALL expose a `search_plugins` tool that searches only the active plugins and returns passages labelled with file name, page and line range.

#### Scenario: Tool output carries sources
- **WHEN** the model calls `search_plugins` with a query
- **THEN** it receives passages labelled like `[Mietrecht.pdf, p. 2]`

### Requirement: Deep thinking (ping-pong)
When "Nachdenken" is on (the default), the model SHALL be allowed to call the search tool repeatedly, up to four rounds, before answering.

#### Scenario: Round limit
- **WHEN** the model requests a fifth search
- **THEN** the tool tells it to answer with the passages already retrieved

### Requirement: Fast lookup
When "Nachdenken" is off, the system SHALL perform exactly one retrieval for the question and answer from those passages without tool calls.

#### Scenario: Works with models without tool support
- **WHEN** a local model without function calling is selected and Nachdenken is off
- **THEN** the question is still answered from retrieved passages

### Requirement: Live trace
Each search round SHALL appear in the conversation while the answer is being produced, showing the query, the number of passages and their sources. Failed searches SHALL appear with their error.

#### Scenario: Failed search is visible
- **WHEN** the embedding endpoint is unavailable
- **THEN** the trace shows the failed search and the model is told that the knowledge could not be searched

### Requirement: Context mode
The profile SHALL offer "Chunks (RAG)" and "Full files"; in full-file mode the text of the best-matching documents is sent with page markers instead of individual passages.

#### Scenario: Full files
- **WHEN** context mode is "Full files"
- **THEN** the model receives whole documents with `[File.pdf, p. N]` page markers

### Requirement: Streaming answer with citations
The answer SHALL stream into the conversation; citations in the form `[Document.pdf, p. 4]` SHALL be resolved to the retrieved document, page and lines.

#### Scenario: Citation resolution
- **WHEN** the answer cites `[Mietrecht.pdf, p. 2]` and lines 1–3 of page 2 were retrieved
- **THEN** the citation links to `Recht/Mietrecht.pdf`, page 2, lines 1–3
