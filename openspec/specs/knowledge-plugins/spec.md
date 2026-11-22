# knowledge-plugins Specification

## Purpose
Turn ordinary folders on the user's disk into searchable knowledge without taking ownership of the files.

## Requirements

### Requirement: Folder-based plugin discovery
Every immediate, non-hidden subdirectory of the configured root directory SHALL be registered as a knowledge plugin named after the folder.

#### Scenario: Subfolders become plugins
- **WHEN** the root contains `Recht/`, `Finanzen/` and `.idefix-index/`
- **THEN** the plugins `Finanzen` and `Recht` are listed and `.idefix-index` is ignored

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

### Requirement: Incremental background re-indexing
The system SHALL watch the root directory and, after changes settle, index new or changed documents and remove deleted ones without re-processing unchanged files.

#### Scenario: Only changes are processed
- **WHEN** one document is edited and another deleted
- **THEN** exactly one document is re-indexed and one is removed

#### Scenario: Broken documents do not stop indexing
- **WHEN** a file with a `.pdf` extension cannot be parsed
- **THEN** the other documents are indexed and the failure is reported in the UI

### Requirement: Local hidden index
Index metadata and embeddings SHALL be stored in `<Root>/.idefix-index/`, marked hidden, and SHALL be rebuilt when the embedding model changes.

#### Scenario: Embedding model switch
- **WHEN** the embedding model in the profile changes
- **THEN** the existing vectors are discarded and all documents are re-indexed

### Requirement: Plugin activation scopes
A plugin SHALL be searchable only if it is enabled globally, included in the project scope (when the project defines one), and not disabled for the current chat session.

#### Scenario: Session toggle narrows the search
- **WHEN** `Finanzen` is enabled globally but switched off with the `+` button of a chat
- **THEN** searches in that chat do not include `Finanzen`
