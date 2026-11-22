# Idefix

Idefix is a local-first, privacy-focused desktop RAG client. Point it at a folder of PDFs and Word documents, ask questions, and get answers with citations you can click to see the exact highlighted lines in the original.

![Idefix mockup](docs/mockup/idefix-mockup.png)

## Your data stays yours

- Every subfolder of your knowledge folder (default `%USERPROFILE%\IdefixKnowledge`) is a **knowledge plugin**. Add, edit or delete files and Idefix re-indexes in the background.
- Your files are opened read-only. The index lives next to them in a hidden `.idefix-index` folder.
- Profile and API keys are encrypted with Windows DPAPI; chats are plain JSON in `%LOCALAPPDATA%\Idefix`.
- Works fully offline with a local [Ollama](https://ollama.com) (chat model plus an embedding model such as `nomic-embed-text`).

## Features

- **Ping-pong retrieval** ("Nachdenken"): the model searches your plugins repeatedly through a `search_plugins` tool; every search appears live in the conversation. Switch it off for a single fast lookup.
- **Citations** like `[Mietrecht.pdf, p. 4]` open an in-app viewer with the cited lines highlighted, plus "Open original file".
- **Projects and sessions** in the sidebar; plugins can be scoped per project and toggled per chat with `+`.
- **Context mode**: send retrieved chunks (RAG) or whole files.
- **English and Swiss German**, light and dark themes, Inter typography.
- A quiet celestial background: a sun halo by day; dim stars, a moon and the occasional shooting star at night.
- Automatic updates via Velopack, with an opt-in for nightly builds.

## Building

Requirements: Windows 10 19041+ (Windows 11 recommended) and the .NET 11 SDK (`global.json` pins `11.0.100`).

```powershell
dotnet build Idefix.slnx
dotnet test --solution Idefix.slnx
dotnet run --project src/Idefix.App
```

Set `IDEFIX_DATA_DIR` to keep a development profile separate from your real one:

```powershell
$env:IDEFIX_DATA_DIR = "$env:TEMP\idefix-dev"; dotnet run --project src/Idefix.App
```

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/Idefix.Core` | Domain records, interfaces and pure logic (planning, chunking, plugin scopes, sky math) |
| `src/Idefix.Infrastructure` | PdfPig/OpenXml parsing, SQLite vector index, Semantic Kernel answer engine, DPAPI settings, Velopack |
| `src/Idefix.App` | Uno Platform app (Windows App SDK head) |
| `tests/Idefix.ApiTests` | API-level tests with real files and a real index |
| `openspec/` | Project conventions and capability specs |
| `.github/workflows/` | CI, nightly pre-releases, SemVer releases |

## Releases

- `develop` → nightly pre-release `vX.Y.Z-nightly.<run>` (only when `develop` changed).
- Tag `vX.Y.Z` on `main` → stable release. Set the `SIGNING_CERTIFICATE` (base64 PFX) and `SIGNING_PASSWORD` secrets to sign binaries.

## Contributing

Read [`openspec/project.md`](openspec/project.md) first. Analyzers run at `latest-all` with warnings as errors, so build locally before opening a pull request against `develop`.
