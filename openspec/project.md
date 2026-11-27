# Project Context

## Purpose
Idefix is a local-first, privacy-focused desktop RAG (Retrieval-Augmented Generation) client.
Users keep full ownership of their knowledge: documents stay in ordinary folders on their disk,
the index lives next to them in a hidden folder, and nothing leaves the machine except the
prompts sent to the language model provider the user configured.

## Tech Stack
- .NET 11 (`net11.0`, `net11.0-windows10.0.26100`), C# latest, nullable enabled
- Uno Platform (`Uno.Sdk`, single project, Windows App SDK head), CommunityToolkit.Mvvm
- Microsoft Semantic Kernel (orchestration, tool calling) with the OpenAI connector against
  OpenAI or any OpenAI-compatible endpoint (Ollama exposes one at `/v1`)
- UglyToad.PdfPig (PDF) and DocumentFormat.OpenXml (DOCX) with line coordinates
- Lucene.NET for an in-memory keyword index over the Markdown sections; no embeddings, nothing written to disk
- SkiaSharp for the celestial background, Inter as the only typeface
- Velopack for installers and auto-update from GitHub Releases
- xUnit v3 on Microsoft.Testing.Platform

## Architecture (CUPID)
The codebase follows the CUPID properties rather than layered ceremony:

- **Composable** – small types with one purpose and few dependencies. `IdefixRuntime` wires
  them together; nothing else knows how the graph is built.
- **Unix philosophy** – each type does one thing: `DocumentLibrary` reads and caches,
  `LuceneSectionSearch` finds sections, `SectionCollector` keeps them within the budget,
  `SectionTools` lets the model ask for more, `AnswerConversation` streams,
  `DocumentContext` formats, `CitationLocator` finds cited lines.
- **Predictable** – pure functions in `Idefix.Core` (context building, citation lines, scoping, sky math)
  are deterministic and covered by tests. Side effects live in `Idefix.Infrastructure`.
- **Idiomatic** – modern C# (records, primary constructors, collection expressions), MVVM with
  source generators, `x:Bind` in XAML, analyzers at `latest-all` with warnings as errors.
- **Domain-based** – names come from the domain: knowledge plugin, citation, trace step,
  session, project. Folders are organised by domain concept, not by technical layer.

Projects:
- `src/Idefix.Core` – domain records, interfaces and pure logic. No third-party dependencies.
- `src/Idefix.Infrastructure` – parsing, indexing, Semantic Kernel engine, DPAPI settings,
  JSON session store, Velopack updates.
- `src/Idefix.App` – Uno Platform UI (views, view models, celestial layer).
- `tests/Idefix.ApiTests` – tests against the public API of Core and Infrastructure with real
  files and scripted chat completions.

## Project Conventions

### Code Style
- `.editorconfig` follows the bbvch/DotNetTemplates baseline: file-scoped namespaces,
  `this.` qualification, UTF-8 with BOM for C# files, StyleCop + Sonar analyzers.
- `TreatWarningsAsErrors` everywhere. Suppress a rule only with a `Justification`.
- Comments explain *why* only (a constraint or quirk the code cannot show). No XML doc
  summaries that restate names.
- One type per file; the file name matches the type.

### Testing Strategy
- API-level tests: exercise public types end to end with temporary folders, generated PDF and
  DOCX files and real parsing.
- No network in tests. LLM behaviour is simulated with a scripted `IChatCompletionService`.

### Git Workflow
- `develop` is the integration branch; a nightly pre-release is built from it.
- `main` holds released code; a `vMAJOR.MINOR.PATCH` tag on `main` produces a stable release.
- Feature branches merge into `develop` via pull request; CI must be green.
- Rename files with `git mv`, committing the pure rename separately from content changes.

### Commit Authorship
- Commits are authored as `Alan Keller <alan.meile@gmail.com>`.
- Commit messages carry no co-author trailers and no tool-generated footers.

## Domain Context
- **Root directory** – chosen in the profile; every immediate subfolder is a knowledge plugin.
- **Knowledge plugin** – a folder of `.pdf` and `.docx` files (recursive). Enabled globally,
  optionally narrowed per project, and toggled per chat session.
- **Document library** – the parsed text of all documents, cached in memory and re-read when a
  file's size or modification time changes.
- **Section** – a Markdown part of a document with title and pages; the unit of search and context.
- **Round** – one step of collecting sections: the local search first, then the model's own requests.
- **Context window** – the model's token budget; the active documents must fit into it.
- **Citation** – `[File.pdf, p. 4]`, resolved to plugin, page and line range for highlighting.

## Important Constraints
- Original user files are opened read-only and never modified.
- Profile settings (including API keys) are encrypted with Windows DPAPI (current user).
- The app must remain usable offline with a local Ollama instance.

## External Dependencies
- An OpenAI-compatible chat endpoint (OpenAI, Ollama, an organisation's own server or a local router).
- GitHub Releases as the Velopack update feed.
