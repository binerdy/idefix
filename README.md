<img src="docs/brand/idefix-icon-512.png" alt="Idefix icon" width="96" align="right">

# Idefix

Idefix is a local-first, privacy-focused desktop RAG client. Point it at a folder of PDFs and Word documents, ask questions, and get answers with citations you can click to see the exact highlighted lines in the original.

## Your data stays yours

- Every subfolder of your knowledge folder (default `%USERPROFILE%\IdefixKnowledge`) is a **knowledge plugin**. Add, edit or delete files and Idefix re-indexes in the background.
- Your files are opened read-only and never modified. Idefix splits them into Markdown sections in a visible `_markdown` folder inside each plugin, which you can read and edit.
- Profile and API keys are encrypted with Windows DPAPI; chats are plain JSON in `%LOCALAPPDATA%\Idefix.Data`, separate from the program folder, so uninstalling never deletes them.
- Works with any OpenAI-compatible chat model, including a local [Ollama](https://ollama.com) or your organisation's own server. No embedding model is needed.

## Features

- **Local search, no embeddings**: for each question a local keyword search (Lucene.NET, German stemming) finds the best sections; only those are sent, without an extra model call.
- **Ping-pong** ("Nachdenken"): the model answers right away if the sections are enough; otherwise it may search again or open the table of contents, for up to two more rounds. Each round shows up live in the conversation.
- **Markdown stays in sync**: sections follow added, changed and deleted documents automatically; "Markdown neu erstellen" in the Library regenerates them on demand.
- **Citations** like `[Mietrecht.pdf, p. 4]` open an in-app viewer with the cited lines highlighted, plus "Open original file".
- **Projects and sessions** in the sidebar; plugins can be scoped per project and toggled per chat with `+`.
- **Context window**: set the model's context size in the profile as stated by your provider. If the active documents don't fit, Idefix says so instead of sending them; switch plugins off for that chat with `+`.
- **English and Swiss German**, Inter typography; light by day and dark after sunset, following local sunrise and sunset through summer and winter time.
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
| `src/Idefix.Core` | Domain records, interfaces and pure logic (document context, citation lines, plugin scopes, sky math, mascot) |
| `src/Idefix.Infrastructure` | PdfPig/OpenXml parsing, cached document library, Lucene.NET section search, Semantic Kernel answer engine, DPAPI settings, Velopack |
| `src/Idefix.App` | Uno Platform app (Windows App SDK head) |
| `tests/Idefix.ApiTests` | API-level tests with real files and a real index |
| `openspec/` | Project conventions and capability specs |
| `.github/workflows/` | CI, nightly pre-releases, SemVer releases |

## Releases

`<Version>` in `Directory.Build.props` is the single source of truth.

- **Merge into `main`** → the Release workflow publishes `v<Version>` if that tag does not exist yet. Pull requests into `main` fail when `<Version>` is already released.
- **`develop`** → a nightly pre-release `v<Version>-nightly.<run>` when `develop` changed in the last 24 hours.
- **After a release**, bump `<Version>` on `develop` to the next version; nightlies refuse to build an already released version because updaters would ignore them.
- Set the `SIGNING_CERTIFICATE` (base64 PFX) and `SIGNING_PASSWORD` secrets to sign binaries.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Only release artifacts built by the GitHub Actions workflows in this repository from `main` (stable) or `develop` (nightly) are signed.
- Committers and reviewers: [binerdy](https://github.com/binerdy)
- Approvers: [binerdy](https://github.com/binerdy)

Every change reaches `main` through a pull request; each signed release is approved by an approver.

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. In detail:

- **Questions and document sections** go to the language model endpoint you configure in your profile (for example a local Ollama or your organisation's server), and only when you ask a question.
- **The model list** is requested from that same endpoint when you click "Modelle suchen".
- **Update checks** contact GitHub Releases at startup and every four hours to download new versions of Idefix. No personal data or document content is sent.

Your documents, Markdown sections, profile and chats stay on your computer. Idefix collects no telemetry.

## Contributing

Read [`openspec/project.md`](openspec/project.md) first. Analyzers run at `latest-all` with warnings as errors, so build locally before opening a pull request against `develop`.
