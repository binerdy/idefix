# knowledge-plugins Specification

## Purpose
Turn ordinary folders on the user's disk into searchable knowledge without taking ownership of the files.

## Requirements

### Requirement: Folder-based plugin discovery
Every immediate, non-hidden subdirectory of the configured root directory SHALL be registered as a knowledge plugin named after the folder.

#### Scenario: Subfolders become plugins
- **WHEN** the root contains `Recht/`, `Finanzen/` and `.git/`
- **THEN** the plugins `Finanzen` and `Recht` are listed and `.git` is ignored

### Requirement: Supported documents
A plugin SHALL include all `.pdf` and `.docx` files found recursively in its folder and SHALL ignore other files and Office lock files (`~$*`).

#### Scenario: Nested documents are found
- **WHEN** `Recht/sub/b.docx` and `Recht/notes.txt` exist
- **THEN** `Recht/sub/b.docx` is indexed and `notes.txt` is not

### Requirement: Originals are never modified
The system SHALL open user documents read-only and SHALL NOT write into plugin folders.

#### Scenario: Parsing leaves the file untouched
- **WHEN** a document is parsed and indexed
- **THEN** its bytes and last-write time are unchanged

### Requirement: Cached document reading
The system SHALL read each document once, keep its text in memory, and re-read it only when its size or modification time changes. Changes in the root directory SHALL trigger a background refresh, and the library SHALL report how many documents are ready and their estimated token count.

#### Scenario: Only changes are re-read
- **WHEN** one document is edited and the next question is asked
- **THEN** only that document is parsed again

#### Scenario: Broken documents do not stop reading
- **WHEN** a file with a `.pdf` extension cannot be parsed
- **THEN** the other documents are still used and the failure is reported in the UI

### Requirement: Markdown sections per plugin
The system SHALL split every document into Markdown sections and store them visibly in `<Plugin>/_markdown/<document path>/NN-<title>.md`, each with a front-matter header (source, section, title, pages, source fingerprint). DOCX sections start at Word heading styles; PDF sections start at lines set clearly larger than the body text; documents without headings are split per page. Very long sections are split, tiny ones merged. Files in `_markdown` are never treated as source documents.

#### Scenario: Sections are written next to the documents
- **WHEN** `Recht/Miete.docx` has the headings "Kündigung" and "Kaution"
- **THEN** `Recht/_markdown/Miete.docx/01-kuendigung.md` and `02-kaution.md` exist

#### Scenario: Edited sections are respected
- **WHEN** the user edits a section file and the original document is unchanged
- **THEN** the edited text is what Idefix sends to the model

#### Scenario: Changed or deleted originals
- **WHEN** the original document changes or is deleted
- **THEN** its sections are regenerated or removed

### Requirement: Rebuilding Markdown sections
Each section file SHALL record the splitter version and a hash of its generated body. When Idefix ships a newer splitter, unedited sections SHALL be regenerated automatically while edited ones are kept. The Library SHALL offer "Markdown neu erstellen" for all plugins and per plugin; it asks for confirmation and regenerates every section, discarding manual edits. Background refreshes and running questions SHALL never write the same section folder at the same time.

#### Scenario: Splitter update
- **WHEN** a new Idefix version changes how documents are split
- **THEN** unedited sections are rebuilt on the next refresh and edited ones stay as they are

#### Scenario: Manual rebuild
- **WHEN** the user confirms "Markdown neu erstellen" for plugin `Recht`
- **THEN** all sections of `Recht` are regenerated from the original documents

### Requirement: Plugin activation scopes
A plugin SHALL be searchable only if it is enabled globally, included in the project scope (when the project defines one), and not disabled for the current chat session.

#### Scenario: Session toggle narrows the search
- **WHEN** `Finanzen` is enabled globally but switched off with the `+` button of a chat
- **THEN** searches in that chat do not include `Finanzen`
